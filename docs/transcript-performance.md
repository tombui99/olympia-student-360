# Transcript performance and workflow

The transcript workspace opens the student directory at `/transcripts/student`. It does not calculate an all-school transcript on entry. Individual transcripts and multi-year PDFs are loaded after selecting a student.

The school summary is available at `/transcripts/summary`. Staff choose an academic year and grade, then click **Load summary**. Semester and class controls operate within that selected cohort.

## API safeguards

- `GET /api/transcripts/overall?studentId=...` reads that student's records.
- School summaries require both `academicYear` and `gradeLevel`. Requests without these return HTTP 400 before querying marks. The API cannot be used to bypass the bounded UI workflow.
- `GET /api/transcripts/catalog` supplies year, grade and subject choices from course/enrollment metadata. It never reads `CanvasCourseScores` or `CanvasSubmissions`. Subject-exclusion settings use this endpoint.
- Course selection is resolved from directory metadata before score aggregation. Mark queries use indexed enrollment/course or student lookups.
- The submission-evidence check preserves grade validity and real zero marks. It uses a set of qualified score IDs and the composite `(StudentProfileId, CanvasCourseId)` index, rather than a student-wide correlated lookup for every score.
- Scoped, complete summaries are cached for approximately 30 seconds. Paging and exclusions project the cached summary without recalculating GPA or reading raw marks again. Cache keys include the year, grade and access scope; lightweight revision/build checks invalidate cached data when school-authored records, roster imports or ETL snapshots change.
- Concurrent requests for the same cold scope share one calculation. Database reads accept cancellation, and shared summary calculations have a 15-second limit.
- Transcript screens use their own loading states; their requests do not block the entire application with the navigation overlay.

## Local measurements

Measured on the existing local development data on 2 November 2026: approximately 45,000 score rows, 373,000 submissions and 885 students with results. These are action-processing times, including database queries and query compilation, rather than full browser/network timings.

| Operation | Measured time |
| --- | ---: |
| Previous unfiltered school request | Exceeded the 8-second query limit |
| Student directory, first query compilation | 205–214 ms |
| Metadata catalog, first query compilation | 270–289 ms |
| Metadata catalog, cached | Under 1 ms |
| Individual transcript, first call | 46–52 ms |
| Individual transcript, repeat call | 15–16 ms |
| One year/grade summary, cold cache | 322–354 ms |
| Same summary, cached | 3–4 ms |
| Subject pagination / exclusions | 1 ms |
| Rejected unfiltered summary | Under 1 ms, zero action database commands |

An additional read-only check exercised the running development API over HTTP, including authentication, access loading, JSON serialization and response transfer on loopback. All 17 year/grade choices exposed by the catalog were requested sequentially, followed immediately by a repeat of each request. First requests took 36–1,194 ms; immediate repeats took 4–7 ms. Current-year first requests took 77–526 ms, while several prior-year summaries took 915–1,194 ms. These measurements do not include browser rendering or establish production capacity. The reported three-second delay was not reproduced in this check.

MySQL EXPLAIN confirmed indexed access for the checked transcript score/submission/enrollment queries, including `IX_CanvasCourseScores_StudentProfileId`, `IX_CanvasCourseScores_CanvasEnrollmentId`, `IX_CanvasEnrollments_CanvasCourseId`, primary-key score lookups and `IX_CanvasSubmissions_TranscriptEvidence`. No full scans of those raw tables appeared in the checked plans. Small course/student-directory metadata reads are intentional.

The full summary originally returned about 199 KB in the benchmark. The selected grade/year response was approximately 14 KB.

## Deployment and regression checks

Apply migration `20261102094148_IndexTranscriptAssessmentEvidence` with the normal migration process before deploying the corresponding frontend/backend changes. It adds an index and does not alter student records. This migration has already been applied to the local development database; production has not been changed.

Database tests cover the API guard, catalog reads without marks, scope isolation, paging/exclusion reuse, refresh after edits from another request, cold-request coalescing and cancellation. Browser checks cover the directory redirect, zero automatic summary requests, explicit year/grade loading, transcript editing and PDF download.

Future all-school or cross-year bulk analytics should use ETL-maintained aggregates or an explicit background export job. Do not restore an unfiltered synchronous `/overall` request for that workflow. Production latency should be measured after deployment; local measurements are not a production latency guarantee.
