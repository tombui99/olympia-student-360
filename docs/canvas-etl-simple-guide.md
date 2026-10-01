# Canvas ETL: Simple Source-of-Truth Guide

Canvas LMS is the source of truth for Canvas academic data. Olympia Student 360 stores the values
Canvas returns and calculates reports from them. It does not alter Canvas-owned course fields,
academic-year labels, grade levels, periods, names, statuses, scores, or IDs. Transcripts may attach
a Student 360-owned canonical reporting subject, but that label is kept separate and is never
presented as a Canvas field or written back to Canvas.

SIS remains the source of truth for official student identity and demographics.

## The flow

```text
GitHub Action or local CLI
  -> runs the durable Canvas ETL process
  -> ETL calls Canvas APIs
  -> backend validates relationships
  -> backend upserts Canvas values into MySQL
  -> reports join and calculate from those stored values
```

An upsert inserts a record the first time its Canvas ID is seen and updates the same record on later
runs. The watermark advances only after the entire run succeeds.

## APIs and destinations

This is the current implemented flow. The ETL does not write an Olympia subject, school year, or
semester into Canvas-owned records. It derives a reporting grade only from an explicit Canvas
grade-account ancestor such as `LH_KHOI08`; it never parses a course title to guess a grade. The
canonical subject resolver is a downstream transcript concern, not an ETL mutation.

| Canvas request | What we read | What the ETL actually does | Destination |
|---|---|---|---|
| `GET /api/v1/accounts/:account_id/sub_accounts?recursive=true` | Exact account hierarchy, names, states, and SIS account IDs | Preserves account records and identifies explicit grade-account ancestors. | `CanvasAccounts` |
| `GET /api/v1/accounts/:account_id/users?enrollment_type=student` | Canvas user identity fields | Preserves the Canvas directory. When SIS is absent, creates a minimal linked profile whose display name is the exact `sis_user_id` (or `canvas-user-ID` when absent), without copying/guessing demographics. | `CanvasUsers`, link/fallback in `StudentProfiles` |
| `GET /api/v1/accounts/:account_id/courses?include[]=term` | Course `id`, `sis_course_id`, `name`, `course_code`, `workflow_state`, term ID/name, and start/end dates | Copies those Canvas fields directly. Course filters vary by sync mode and configured enrollment terms. A course returned by Canvas as `deleted` causes its matching warehouse course and dependent Canvas rows to be removed. | `CanvasCourses` |
| `GET /api/v1/courses/:course_id/sections` | Exact section/class IDs, names, SIS references, dates, and student totals | Preserves Canvas classes separately; future SIS classes remain an independent SIS-owned dimension. | `CanvasCourseSections` |
| `GET /api/v1/courses/:course_id/assignment_groups` | Assignment-group `id` and `name` | Uses the group ID to attach the exact Canvas group name to assignments. There is no separate assignment-group table and no Olympia-generated group name. | `AssignmentGroupId` and `AssignmentGroupName` on `CanvasAssignments` |
| `GET /api/v1/courses/:course_id/assignments` | Assignment `id`, `name`, `points_possible`, `due_at`, `workflow_state`, `assignment_group_id`, and `grading_type` | Copies the values directly and relates the assignment to the Canvas course from the request path. | `CanvasAssignments` |
| `GET /api/v1/courses/:course_id/grading_periods` | Period `id`, `title`, start/end dates, and weight | Does not invent semester labels. The exact Canvas period metadata is attached to score facts returned by the period-specific enrollment request. There is no standalone grading-period table. | Grading-period columns on `CanvasCourseScores` |
| `GET /api/v1/courses/:course_id/enrollments?type[]=StudentEnrollment&state[]=active&state[]=completed&include[]=current_points` | Enrollment, user, and course IDs; type; state; SIS user ID; `grades.current_score`; `grades.final_score`; and both Canvas override-score fields | Upserts the Canvas enrollment and creates one overall score fact for **each** Canvas enrollment. It preserves nulls and `final_score=0`; it does not choose a “best” enrollment or calculate a replacement score. | `CanvasEnrollments` and overall rows in `CanvasCourseScores` |
| `GET /api/v1/courses/:course_id/enrollments?...&grading_period_id=:grading_period_id` | The enrollment grade values Canvas returns for that grading period | Creates a score fact for that Canvas enrollment and period, copying the grade values and the period metadata above. | Grading-period rows in `CanvasCourseScores` |
| `GET /api/v1/courses/:course_id/students/submissions?student_ids[]=all&include[]=user` | Submission, assignment, and user IDs; score; grade; status flags; and Canvas submission dates | Upserts exact Canvas submission fields and relates them to the Canvas assignment/course. A record without a Canvas submission ID, or for an assignment absent from the Canvas assignment response, is skipped rather than given a made-up ID. No subject, academic year, semester, or grade level is copied onto the submission. | `CanvasSubmissions` |

All list requests are paginated. During an incremental run, the submissions endpoint is called with
both `submitted_since=<watermark-with-overlap>` and `graded_since=<watermark-with-overlap>`, then the
two result sets are merged by Canvas submission identity. Canvas does not offer one universal
`updated_after` filter for all of these resources, so courses, assignments, grading periods,
enrollments, and current grade totals are refreshed for the configured active-course scope. A
full/reconciliation run reads the broader configured course scope without the submission cutoff.

## Exact mappings

### Courses

| Canvas field | Database column | Handling |
|---|---|---|
| `id` | `CanvasCourseId` | Exact; upsert key |
| `sis_course_id` | `SisCourseId` | Exact |
| `name` | `Name` | Exact; remains null if Canvas returns null |
| `course_code` | `CourseCode` | Exact |
| `workflow_state` | `WorkflowState` | Exact; no default is invented |
| `enrollment_term_id` / `term.id` | `EnrollmentTermId` | Exact |
| `term.name` | `EnrollmentTermName` | Exact |
| `start_at` | `StartAt` | Exact |
| `end_at` | `EndAt` | Exact |

The ETL does not overwrite these fields with a reporting subject. `CanvasGradeLevel` and its source
account ID are derived only from the stored Canvas account hierarchy. Canonical transcript subjects
come from separate Student 360 configuration and controlled aliases; they are not Canvas columns.

### Assignments

| Canvas field | Database column | Handling |
|---|---|---|
| `id` | `CanvasAssignmentId` | Exact; upsert key |
| course context | `CanvasCourseId` | Exact Canvas course ID |
| `name` | `Name` | Exact |
| `points_possible` | `PointsPossible` | Exact |
| `due_at` | `DueAt` | Exact |
| `workflow_state` | `WorkflowState` | Exact |
| `assignment_group_id` | `AssignmentGroupId` | Exact |
| related group `name` | `AssignmentGroupName` | Exact value from Canvas assignment-groups API |
| `grading_type` | `GradingType` | Exact |

### Enrollments and grades

| Canvas field | Database column | Handling |
|---|---|---|
| enrollment `id` | `CanvasEnrollmentId` | Exact; enrollment upsert key |
| `user_id` | `CanvasUserId` | Exact |
| `course_id` | `CanvasCourseId` | Exact |
| `type` | `Type` | Exact |
| `enrollment_state` | `EnrollmentState` | Exact |
| `sis_user_id` | `SisUserId` | Exact Canvas/SIS cross-reference |
| `grades.current_score` | `CurrentScore` | Exact |
| `grades.final_score` | `FinalScore` | Exact, including `0` |
| enrollment `override_score` | `EnrollmentOverrideScore` | Exact top-level Canvas value |
| `grades.override_score` | `GradesOverrideScore` | Exact value from the Canvas `grades` object |
| grading-period `id` | `GradingPeriodId` | Exact |
| grading-period `title` | `GradingPeriodTitle` | Exact |
| grading-period dates/weight | `GradingPeriodStartDate`, `GradingPeriodEndDate`, `GradingPeriodWeight` | Exact |

Canvas does not provide an ID for the embedded enrollment-grade object. Olympia therefore uses
`WarehouseScoreFactId`, a clearly named deterministic warehouse key. It is not presented as a
Canvas ID. Every Canvas enrollment keeps its own score fact; the ETL does not select a “best”
enrollment or discard duplicates.

### Submissions

| Canvas field | Database column | Handling |
|---|---|---|
| `id` | `CanvasSubmissionId` | Exact; records without a Canvas ID are skipped rather than assigned a fake Canvas ID |
| `assignment_id` | `CanvasAssignmentId` | Exact |
| course request context | `CanvasCourseId` | Exact Canvas course ID |
| `user_id` | `CanvasUserId` | Exact |
| `score` | `Score` | Exact |
| `grade` | `Grade` | Exact |
| `workflow_state` | `WorkflowState` | Exact |
| `submission_type` | `SubmissionType` | Exact |
| `submitted_at` | `SubmittedAt` | Exact |
| `graded_at` | `GradedAt` | Exact |
| `cached_due_date` | `CachedDueDate` | Exact; no assignment-date fallback is stored |
| `late` | `Late` | Exact |
| `missing` | `Missing` | Exact |
| `excused` | `Excused` | Exact and nullable; Canvas null stays null |
| `seconds_late` | `SecondsLate` | Exact |

`PointsPossible` is read from the related `CanvasAssignment` when a report needs a percentage. An
effective due date may be calculated in a report as `cached_due_date ?? assignment.due_at`, but that
calculation is not written back as Canvas source data.

## Warehouse-only fields

These fields are not Canvas facts and are deliberately named or documented as warehouse metadata:

- Base `Id`, plus Canvas-table columns `WarehouseCreatedAt` and `WarehouseUpdatedAt`: database row
  and load metadata, not Canvas timestamps.
- `WarehouseScoreFactId`: stable key for an embedded enrollment-grade fact.
- `StudentProfileId`: relationship to the SIS-owned student record.
- `CanvasSyncStates`: watermark, run status, mode, and error bookkeeping.

They do not replace or redefine a Canvas value.

## Reporting and transcripts

Reports join the raw relationships at query time:

- source-course label: exact Canvas course `name` (or Canvas `course_code` when the name is missing);
- academic-term label: Canvas `term.name`;
- grading period: Canvas grading-period `title`;
- grade level: the course's structured Canvas grade account, then official SIS grade, then the
  student's current Canvas grade fallback; homeroom/campus and official identity remain SIS-owned;
- assignment percentage: Canvas submission `score / assignment.points_possible`;
- effective due date for reporting: Canvas `cached_due_date ?? assignment.due_at`.

These are presentation calculations. They are not stored in Canvas tables as invented source data.
If Canvas does not provide a timestamp for a metric point, Olympia does not substitute the current
time. Undated course grades may contribute to the current average card but not a fabricated monthly
trend.

### Canonical transcript subjects

Grade and student transcripts group scores under a Student 360-owned canonical reporting subject.
This provides a stable `Math` row around class-specific Canvas courses while retaining the exact
Canvas course name, code, SIS course ID, and Canvas course ID as source detail. It does not turn the
canonical subject into a Canvas “master course” and does not change any Canvas record.

For every Canvas course, the resolver uses the first match in this order:

1. Explicit Canvas course ID from `TranscriptSubjects:CanvasCourseIds`.
2. Exact SIS course ID from `TranscriptSubjects:SisCourseIds`.
3. Exact Canvas course code from `TranscriptSubjects:CourseCodes`.
4. A controlled, application-owned set of bilingual English/Vietnamese subject aliases.
5. A per-course unmapped fallback.

The explicit mappings are deliberately higher priority than aliases. This lets an authoritative
exception override a general label without changing the source course. Alias matching is limited to
the controlled application list; it is not an unrestricted fuzzy match.

An empty, production-safe configuration is:

```json
{
  "TranscriptSubjects": {
    "CanvasCourseIds": {},
    "SisCourseIds": {},
    "CourseCodes": {}
  }
}
```

The following example uses fictitious identifiers and shows all three explicit mapping levels:

```json
{
  "TranscriptSubjects": {
    "CanvasCourseIds": {
      "123456": "Math"
    },
    "SisCourseIds": {
      "2627-MATH-5H1": "Math"
    },
    "CourseCodes": {
      "MATH-5": "Math"
    }
  }
}
```

Configuration keys should use authoritative values copied from Canvas or the SIS. Values are the
canonical subject labels shown on transcripts. A source course still appears by its original Canvas
name beneath that label and in transcript detail records.

If no configured mapping or controlled alias matches, the transcript uses an unmapped fallback
whose identity includes that Canvas course's ID. Two unknown courses therefore remain two distinct
groups even when their displayed names happen to be identical. This fail-safe preserves data and
makes missing mappings visible instead of silently combining unrelated courses.

The student grade journey groups Canvas `grades.final_score` by the exact Canvas enrollment-term
name and grading-period title. It does not infer the student's historical grade level from their
current SIS grade.

### Workspace Canvas metric variables

The workspace keeps Canvas fields separate instead of silently combining them:

| Metric variable | Canvas fields | Reporting calculation |
|---|---|---|
| Average Canvas `final_score` | `enrollment.grades.final_score` | Average of non-null values; `0` is included |
| Average Canvas `current_score` | `enrollment.grades.current_score` | Average of non-null values; `0` is included |
| Average Canvas submission score percentage | `submission.score`, `submission.workflow_state`, `assignment.points_possible` | For `workflow_state=graded`, average `score / points_possible × 100` when `points_possible > 0` |
| Canvas submissions with `submitted_at` | `submission.submitted_at` | Count records where `submitted_at` is non-null |
| Canvas submissions with `workflow_state=graded` | `submission.workflow_state` | Count records whose exact Canvas state is `graded` |
| Canvas submissions flagged missing | `submission.missing`, `submission.excused` | Count `missing=true` and `excused` is not true |
| Canvas submissions flagged late | `submission.late` | Count `late=true` |

`final_score` and `current_score` are never combined with a fallback in workspace metrics. Overall
enrollment scores have no Canvas grade timestamp, so they contribute to the current metric value but
not a monthly trend. A grading-period score appears in a monthly series only when Canvas supplied the
grading period's `end_date`. The workspace displays each metric's formula and source fields beside
the variable selector.

## Sync modes

- `sync-all`: manual replacement for the configured academic terms covering 2025-2026 and 2026-2027.
- `incremental`: daily watermark-based run with overlap; watermark advances only after success.
- `reconcile-days=N`: bounded replay used to catch late Canvas corrections.

Canvas source values are upserted in all modes. SIS links may be added later when SIS data arrives,
without changing the original Canvas academic values.
