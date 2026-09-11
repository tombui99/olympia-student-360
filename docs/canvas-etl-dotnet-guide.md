# How the Canvas ETL Works in .NET

This guide explains the implementation of Olympia Student 360's Canvas ETL. It focuses on the
runtime flow, the responsibilities of each .NET component, the three synchronization modes, and
what happens when a run succeeds or fails.

For the exact Canvas-field-to-database-column mappings, see
[`canvas-etl-simple-guide.md`](canvas-etl-simple-guide.md).

## Short version

```text
GitHub Actions or a local terminal
  -> runs the OlympiaStudent360.CanvasEtl console application
  -> console validates its mode and configuration
  -> console sends one authenticated POST to the Olympia backend
  -> backend reads the current watermark
  -> backend calls the Canvas APIs and follows every pagination link
  -> backend validates, links, and upserts the returned Canvas records into MySQL
  -> backend advances the watermark only after the complete run succeeds
  -> console exits 0 on success or non-zero on failure
```

The GitHub runner does **not** connect directly to MySQL/RDS. Only the deployed Olympia backend
needs database access. The runner needs HTTPS access to the backend and Canvas credentials.

## The two .NET applications

### 1. `OlympiaStudent360.CanvasEtl`

This is a small command-line application. Its entry point is
`backend/OlympiaStudent360.CanvasEtl/Program.cs`.

It is responsible for:

1. Reading the requested mode: `--sync-all`, `--incremental`, or `--reconcile-days=N`.
2. Rejecting invalid argument combinations.
3. Reading Canvas and backend configuration from .NET user secrets or environment variables.
4. Selecting the configured Canvas enrollment-term IDs for the requested mode.
5. Calling `POST /api/canvas-sync` on the Olympia backend.
6. Returning exit code `0` on success, `1` on failure, or `2` when cancelled.

It deliberately does not contain the database connection string and does not write directly to
RDS.

### 2. `OlympiaStudent360.Api`

The backend owns the actual synchronization. Its entry point for a run is
`backend/OlympiaStudent360.Api/Controllers/CanvasSyncController.cs`.

The backend project compiles the following ETL implementation files from the ETL folder:

- `CanvasApiClient.cs`
- `CanvasDtos.cs`
- `CanvasEtlOptions.cs`
- `CanvasSyncService.cs`

This arrangement gives the console and backend one set of Canvas request models and options while
keeping MySQL writes inside the backend.

## Authentication and network boundary

The console sends two private headers to the backend:

```text
X-Olympia-Sync-Key: shared ETL/backend secret
X-Canvas-Access-Token: Canvas API access token
```

The backend compares the sync key using a fixed-time comparison. The endpoint rejects missing or
incorrect credentials before starting the ETL. The backend URL must use HTTPS, except for a
loopback URL such as `http://localhost:5279` during local testing.

Neither credential should be committed or printed. In GitHub Actions they come from the GitHub
`production` environment secrets. Locally they come from .NET user secrets.

## What happens at the beginning of a run

The backend performs these steps before contacting Canvas:

1. Validates the mode, enrollment-term IDs, overlap, and reconciliation window.
2. Applies pending Entity Framework migrations.
3. Captures the current UTC time as the candidate watermark.
4. Loads or creates the `CanvasSyncStates` row for the Canvas host and account.
5. Refuses an incremental run when no successful watermark exists.
6. Calculates the submission cutoff for incremental or reconciliation mode.
7. Records the run as `running`.

The candidate watermark is captured at the **start** of the run. This prevents records changed
while a long synchronization is running from falling into a gap between runs.

## The three synchronization modes

### `--sync-all`

Purpose: manually load or repair the complete configured academic scope.

- Uses `Canvas:SyncAllEnrollmentTermIds`.
- The production workflow includes every configured Canvas term covering academic years
  2025-2026 and 2026-2027: `227-232` and `333-340`.
- Includes available and completed courses.
- Does not apply a submission date cutoff.
- Reads every submission returned for those courses.
- Establishes or replaces the successful watermark after completion.

It is safe to run `sync-all` again because records are upserted by stable Canvas identities.

### `--incremental`

Purpose: perform the normal daily synchronization.

- Uses `Canvas:EnrollmentTermIds` supplied through `CANVAS_ENROLLMENT_TERM_IDS` in GitHub.
- Requires an existing successful watermark.
- Calculates `since = last successful watermark - overlap`.
- The production overlap is currently 120 minutes.
- Limits the course query to the configured active academic terms.
- Retrieves submissions changed in the overlapping window.
- Fully refreshes assignments, grading periods, enrollments, and enrollment-grade totals for the
  selected courses because Canvas does not provide one universal updated-after filter for them.

Incremental submission responses are partial, so an incremental run never deletes an older
submission merely because it was absent from that partial response.

### `--reconcile-days=N`

Purpose: catch late grading corrections and other changes that arrive outside the daily overlap.

- Accepts a window from 1 through 90 days.
- The workflow currently uses 30 days.
- Uses the union of the historical and current configured term IDs.
- Includes completed courses.
- Uses `now - N days` as the submission cutoff.

The reconciliation workflow is currently scheduled weekly at 02:00 Monday in Vietnam
(`19:00 Sunday UTC`).

## Canvas API reads

The backend uses `CanvasApiClient` to call:

```text
GET /api/v1/accounts/:account_id/users?enrollment_type=student
GET /api/v1/accounts/:account_id/courses?include[]=term
GET /api/v1/courses/:course_id/assignment_groups
GET /api/v1/courses/:course_id/assignments
GET /api/v1/courses/:course_id/grading_periods
GET /api/v1/courses/:course_id/enrollments
GET /api/v1/courses/:course_id/enrollments?grading_period_id=:id
GET /api/v1/courses/:course_id/students/submissions
```

For incremental and reconciliation submissions, Canvas requires two separate queries:

```text
submitted_since=<cutoff>
graded_since=<cutoff>
```

The ETL merges and deduplicates those responses. This captures both a newly submitted attempt and
a newly graded or regraded older attempt.

## Pagination, throttling, and parallelism

Canvas list endpoints are paginated. `CanvasApiClient` reads the HTTP `Link` header and follows the
`rel="next"` URL until no next page remains.

For security, every pagination URL must have the same scheme, host, and port as the configured
Canvas origin. A pagination link to another origin stops the run.

Transient timeouts, HTTP `429`, and server errors are retried up to six attempts. The client honors
Canvas `Retry-After` when present and otherwise uses exponential backoff.

The ETL avoids sending an unlimited burst of requests:

- Daily runs process courses in batches of 4.
- Full and reconciliation runs process courses in batches of 8.
- Within one course, independent assignment, group, grading-period, enrollment, and submission
  requests run concurrently.

All three GitHub workflows share the `canvas-production-sync` concurrency group, so two production
Canvas syncs do not run at the same time.

## Student identity linking

Canvas is not allowed to overwrite official SIS student identity or demographics.

The ETL reads Canvas account users only for their Canvas user ID and `sis_user_id`. It then:

1. Looks for an existing SIS-owned `StudentProfile` with the same `SISUserId`.
2. Stores the Canvas user ID as a cross-reference when the match is unambiguous.
3. Links enrollments, score facts, and submissions to that existing profile.
4. When enabled, creates a minimal fallback profile keyed and displayed by the exact Canvas
   `sis_user_id` (or an explicit `canvas-user-ID` key when Canvas has no SIS ID).

A later run relinks unmatched historical facts after SIS data becomes available. Conflicting Canvas
and SIS identities stop the run instead of guessing or merging students incorrectly.

The ETL does not copy Canvas names, homerooms, campuses, or demographics into SIS-owned fields. It
stores `CanvasGradeLevel` separately, derived only from an explicit Canvas grade-account ancestor;
official SIS `GradeLevel` remains distinct and takes precedence in reporting.

## Validation and transformation

The ETL validates structure and relationships but does not redefine Canvas values.

Examples:

- Canvas IDs remain the upsert identities for courses, assignments, enrollments, and submissions.
- `current_score`, `final_score`, and the two override-score locations remain separate.
- `final_score=0` is preserved as zero and is not converted to null.
- Submission status flags and timestamps are copied directly.
- Assignment-group names come from the Canvas assignment-groups API.
- A submission without a Canvas submission ID is skipped rather than assigned a fake Canvas ID.
- Course names are not parsed into invented subjects, years, semesters, or grade levels.

The only generated score identifier is `WarehouseScoreFactId`. Canvas enrollment grade objects do
not have their own Canvas ID, so the ETL deterministically creates a warehouse-only key from the
Canvas enrollment and grading-period identity. It is explicitly named as warehouse metadata and is
never presented as a Canvas ID.

## Upserts and transactions

Each course is saved in its own MySQL transaction.

Before processing courses, `CanvasSyncService` upserts the Canvas account hierarchy and Canvas user
directory. For a course, it then:

1. Inserts or updates `CanvasCourses` by `CanvasCourseId`.
2. Inserts or updates `CanvasCourseSections` by `CanvasSectionId`.
3. Inserts or updates `CanvasAssignments` by `CanvasAssignmentId`.
4. Inserts or updates `CanvasEnrollments` by `CanvasEnrollmentId`, including exact section membership.
5. Inserts or updates overall and grading-period rows in `CanvasCourseScores`.
6. Inserts or updates `CanvasSubmissions` by `CanvasSubmissionId`.
7. Removes stale authoritative children when the current response is complete.
8. Commits the course transaction.

Assignments, enrollments, and score totals are fully read for a selected course, so missing rows can
be removed. Submissions are pruned only during an unbounded full response; they are not pruned from
incremental or reconciliation responses.

Courses Canvas explicitly returns with `workflow_state=deleted` are removed with their dependent
Canvas records.

The whole run is not one giant database transaction. If course 20 fails after courses 1 through 19
were committed, those successful upserts remain. This is intentional: course-sized transactions
avoid holding a database transaction open for a multi-hour historical load. Because the failed run
does not advance the watermark and all writes are idempotent upserts, the next run safely processes
the window again.

## Watermark success and failure rules

The watermark advances only after all selected courses have completed successfully.

On success, the backend records:

- `LastSuccessfulWatermarkUtc = candidate watermark captured at start`
- completion time
- run mode
- status `completed`

On failure, the backend records:

- completion time
- status `failed`
- a bounded error message

It does **not** update `LastSuccessfulWatermarkUtc`. The next daily run therefore starts from the
previous successful watermark, including the configured overlap, and safely retries the missed
window.

## Replace mode

`--replace` is optional and destructive. It is accepted only together with `--sync-all`:

```bash
dotnet run --project backend/OlympiaStudent360.CanvasEtl/OlympiaStudent360.CanvasEtl.csproj -- --sync-all --replace
```

It deletes the existing Canvas warehouse rows before reloading the configured full scope. A later
failure can therefore leave a partially reloaded warehouse until the command is rerun successfully.
Do not use it for a normal repair; a regular `--sync-all` already upserts and reconciles historical
data.

## GitHub workflows

| Workflow | Trigger | .NET mode | Scope |
|---|---|---|---|
| `backend/sql/export-local-canvas-for-rds.sh` | Local command, followed by MySQL Workbench | Baseline SQL transfer | Verified local Canvas warehouse plus its successful watermark |
| `.github/workflows/sync-canvas.yml` | Manual; daily schedule template is currently commented | `--incremental` | Current configured terms since watermark minus overlap |
| `.github/workflows/reconcile-canvas.yml` | Manual; weekly schedule template is currently commented | `--reconcile-days=30` | Historical and current configured terms for the previous 30 days |

The generated baseline SQL is stored under `backend/sql/generated/` and is intentionally ignored by
Git because it contains private Canvas data. Run it with MySQL Workbench's **File > Run SQL Script**
command rather than opening the large file in an editor tab. Once it succeeds, normal imports use
the transferred `CanvasSyncStates.LastSuccessfulWatermarkUtc`; the one-time GitHub full-sync
workflow is not retained.

GitHub treats a non-zero console exit code as a failed job. The workflow log includes the backend's
final record counts but should never include either secret.

## Required configuration

### GitHub production environment

```text
Secrets:
CANVAS_ACCESS_TOKEN
AWS_DEPLOY_ROLE_ARN
OLYMPIA_RDS_CONNECTION_STRING (optional direct override)

Variables:
AWS_REGION
APP_RUNNER_SERVICE_ARN
CANVAS_BASE_URL (optional; defaults to Olympia's Canvas host)
CANVAS_ACCOUNT_ID (optional; defaults to Olympia's root account 1)
```

When `OLYMPIA_RDS_CONNECTION_STRING` is absent, the workflow assumes `AWS_DEPLOY_ROLE_ARN` and
reads `ConnectionStrings__DefaultConnection` from the deployed App Runner service. The role must
retain the same `apprunner:DescribeService` permission used by backend deployment and, when App
Runner references a secret instead of a literal runtime variable, read access to that secret.

The deployed backend separately requires these values for its optional HTTP sync endpoint:

```text
CanvasSync__ApiKey
ConnectionStrings__DefaultConnection
```

Any caller of that endpoint must send a key matching `CanvasSync__ApiKey`; the GitHub workflows use
the direct ETL path and do not depend on an HTTP request remaining open.

### Local execution

After configuring the user secrets described in the project README and starting the backend at
`http://localhost:5279`, run one of:

```bash
# Complete configured scope since September 2025
dotnet run --project backend/OlympiaStudent360.CanvasEtl/OlympiaStudent360.CanvasEtl.csproj -- --sync-all

# Daily watermark-driven behavior
dotnet run --project backend/OlympiaStudent360.CanvasEtl/OlympiaStudent360.CanvasEtl.csproj -- --incremental

# Reload the previous 30 days
dotnet run --project backend/OlympiaStudent360.CanvasEtl/OlympiaStudent360.CanvasEtl.csproj -- --reconcile-days=30
```

If no mode is supplied, the console defaults to incremental mode.

## Common failures

| Symptom | Meaning |
|---|---|
| HTTP `401` from the backend | The ETL sync key does not match the backend configuration |
| HTTP `409` on incremental mode | No successful watermark exists; run `--sync-all` first |
| “Canvas returned no courses” | Incorrect account/term IDs, insufficient Canvas permissions, or an unexpectedly empty scope |
| “Canvas returned no students” | The token cannot read account users or the account selection is wrong |
| Canvas HTTP `401`/`403` | Invalid token or missing Canvas API permissions |
| Identity conflict | Canvas user ID and `sis_user_id` point to different SIS profiles; resolve the source data before rerunning |
| Job timeout | The selected full scope is too large for the workflow timeout or Canvas is heavily throttling requests |

Do not bypass empty-data or identity-conflict guards merely to make a job green. They exist to stop a
bad configuration from deleting, mislinking, or misreporting warehouse data.

## Code map

| File | Responsibility |
|---|---|
| `backend/OlympiaStudent360.CanvasEtl/Program.cs` | CLI modes, configuration, backend request, process exit code |
| `backend/OlympiaStudent360.Api/Controllers/CanvasSyncController.cs` | Authentication, watermark, run status, backend orchestration |
| `backend/OlympiaStudent360.CanvasEtl/CanvasApiClient.cs` | Canvas requests, pagination, retry/backoff, incremental submission filters |
| `backend/OlympiaStudent360.CanvasEtl/CanvasDtos.cs` | Exact JSON fields read from Canvas |
| `backend/OlympiaStudent360.CanvasEtl/CanvasSyncService.cs` | Identity linking, validation, course transactions, upserts, pruning |
| `backend/OlympiaStudent360.CanvasEtl/CanvasEtlOptions.cs` | Configuration parsing and safety validation |
| `backend/OlympiaStudent360.Api/Data/AppDbContext.cs` | MySQL relationships, constraints, and indexes |
