# Learning and assignment reports

The four configurable reports under `/reports` use `CanvasDashboardMetricAggregates` as their
numeric source. Every successful Canvas sync materializes student, course, assignment, and month
facts before requests use them. On a deployment where existing Canvas data has never had the
version 2 aggregate built, an API background worker performs a one-time backfill after migrations;
it does not fetch Canvas again. `CanvasDashboardAggregateBuilds` records the schema version of a
completed rebuild. Until the backfill commits, the report API returns an unavailable state; it
never falls back to raw Canvas submissions or scores. The worker retries failed builds and a
database lock prevents simultaneous backfill and ETL rebuilds across instances. For a manual
operational rebuild, run
`dotnet run --project backend/OlympiaStudent360.CanvasEtl -- --rebuild-dashboard-aggregates-only`
in an environment with the configured database connection.

| Report | Stored facts used | Teacher configuration |
| --- | --- | --- |
| Missing assignment frequency | submitted and non-excused missing counts | year, term, month, courses, Canvas assignment groups, maximum missing count |
| Homework achievement | sum of graded submission score percentages and sample count | year, term, courses, Canvas assignment groups, minimum average score |
| Selected task completion | submitted and non-excused missing counts | year, term, month, courses, selected assignment IDs, minimum completion rate |
| Grading coverage | submitted and submitted-with-graded-status counts | year, term, courses, minimum grading rate |

The request groups narrow persisted facts in SQL, applies configurable thresholds, counts matching
groups, and returns only the requested 15-row page. This small request-time arithmetic is needed because arbitrary combinations of
courses, assignment groups, assignments, and thresholds cannot all be materialized ahead of time.
The raw `CanvasSubmissions` and `CanvasCourseScores` tables are read during ETL, not by these
report requests. Assignment and course metadata are read only to populate selectors and labels.
Report-specific filters and the current page are encoded in the URL query parameters. They are
not stored as per-user database preferences; opening a report without those parameters uses its
default filters.

The selected-task completion rate is `submitted / (submitted + Canvas-flagged missing)`; it is a
status-based proxy and does not assert reading quality. Grading coverage uses Canvas `graded`
workflow status and does not assert that written teacher feedback was provided. Report 5's
HS2/HS3 assessment types and red/amber/green classroom rules are not represented by reliable
structured fields here. Report 6's support-exit decision rules are not defined. Neither was
added as a school report.

## Other `/reports` materializations

The other report families now use `ImportedReportAggregates`, a daily/student-grained table of
preclassified and counted behaviour, project, mental-health, bus, and health-visit facts.
`StudentRiskSnapshots` stores period-specific Canvas grade averages and both with-bus and
without-bus risk classifications, so access-restricted teachers see the appropriate result.
The dashboard overview, at-risk students, class drill-down student lists, imported dashboard
metrics, behaviour summary, health diagnoses/frequent-student summaries, and supplemental
cards consume these persisted facts. Supplemental totals and distributions cover the full
visible population, rather than the first 100 records. The underlying source records are
read off-request by the builder; record-level drill-down remains a paginated source read.

Source writes append an `ImportedReportChange` marker in the same save operation. The hosted
builder waits for a 30-second quiet period (or at most five minutes of continuous changes),
uses a database lock to serialize rebuilds, and
transactionally swaps the aggregates and snapshots. The Canvas aggregate completion marker,
classification settings, academic periods, and student identity changes also trigger this
builder. A daily rebuild advances rolling risk and 30-day windows even when no new data arrives.
On first deployment, summary endpoints return HTTP 503 until the initial build succeeds;
they never fall back to expensive raw calculations. Import/bulk writers that bypass EF
`SaveChangesAsync` must explicitly add an `ImportedReportChange` marker (or invoke the rebuild).

For deployment, apply the API migrations and start the API. The Canvas report backfill runs
automatically once if version 2 is missing; it can take time on a large warehouse, and the
report remains unavailable until the transaction commits. Then run the API once with
`dotnet run --project backend/OlympiaStudent360.Api -- --rebuild-imported-report-aggregates-only`
using the normal production connection and JWT configuration. The normal API hosted worker
subsequently refreshes imported aggregates after source changes and at least daily. Monitor
`ImportedReportBuilds.CompletedAtUtc` and compare `LastChangeId` with the latest
`ImportedReportChanges.Id`; a missing build, a lagging change ID, or a build older than a day
indicates that reports are stale. Check worker logs for rebuild failures. The import rebuild
is a full source scan performed off-request, so schedule large bulk imports outside peak hours.

Dynamic teacher filters still require summing/grouping already-aggregated rows in SQL or in
memory, and ratios are calculated from those stored numerators and denominators. This is
bounded work over report facts, not on-demand classification of operational rows. Risk
snapshots are refreshed after data changes and at least daily, so a moving-window risk flag
can lag its exact expiry until the next build. Record-detail pages use SQL count/pagination
and are intentionally not precomputed because they show individual source records.
