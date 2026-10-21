# Learning and assignment reports

The four configurable reports under `/reports` use `CanvasDashboardMetricAggregates` as their
numeric source. A successful Canvas sync first commits its `CanvasSyncRun` completion and advances
the watermark. That completed run then triggers a rebuild of the student, course, assignment, and
month facts. The optional API worker watches completed runs and retries a missed or failed rebuild, including
the initial backfill after deployment when a completed Canvas run exists; it does not fetch Canvas
again. A legacy warehouse without run history needs the explicit manual rebuild command. The
`CanvasDashboardAggregateBuilds` row records both the schema version and the completed Canvas run
through which the summaries were built. Until an initial build commits, the report API returns an
unavailable state; it never falls back to raw Canvas submissions or scores. A database lock keeps
the rebuild from reading a partially imported warehouse or overlapping another rebuild. For a
manual operational rebuild, run
`dotnet run --project backend/OlympiaStudent360.CanvasEtl -- --rebuild-dashboard-aggregates-only`
in an environment with the configured database connection.

Normal syncs now save `CanvasReportCourseChanges` in the same transaction as changed course
facts. Refresh replaces only those courses' metric and leadership rows, then clears their
invalidations and advances the build cursor in one transaction. Timestamp-only polls and
enrollment activity changes do not rebuild reports. Historical SIS identity relinks also queue
their courses. A failed refresh retains both the previous readable snapshot and the pending
course changes, so retrying does not require another Canvas download.

The initial build, explicit manual rebuild, an academic-period configuration change, or a
completed import from an older version without change tracking requires a full refresh.
Deploying this version adopts an already compatible snapshot without rewriting its history.
Metric writes use parameterized multi-value batches; score-point joins explicitly use the
existing composite leadership index to avoid the poor MySQL 8.4 plan observed on RDS.
Use `--refresh-dashboard-aggregates-only` (or manual workflow mode `reports-only`) to retry pending
aggregation without reimporting Canvas or forcing a full historical rebuild.

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
workflow status and does not assert that written teacher feedback was provided.

The school administration and subject leadership reports read `CanvasLeadershipReportAggregates`,
a second materialization built alongside the dashboard facts. Each stored row combines one
student, course, reporting year, term, month, and exact Canvas assignment-group name, with
additive submitted/missing/late/graded counts, score sums/sample counts, and graded earned and
possible point totals. Canvas final-course
scores occupy separate rows so assessment categories cannot change them. This pivot happens
after a completed Canvas sync or during deployment backfill, not during a report HTTP request. A
rebuild atomically replaces both aggregate tables. A v4 deployment backfill adds point totals
from existing assignment-grain metric facts and their source submissions without rewriting the
dashboard metric table. Older v2/v3 builds are fully rebuilt because their lineage may be
incomplete. Both paths write version 5 readiness in their transaction; until that commit, the leadership reports show an
unavailable state rather than scanning raw Canvas data. If a later rebuild fails, the previous
compatible aggregate remains readable while materialization is retried. The build cursor still records
its actual freshness; a newer completed sync no longer makes an existing snapshot unavailable.

The two reports appear directly in `/reports` and use the page's academic year and term. Both
support grade, homeroom, Canvas subject, and the HS1, HS2, HS3, and GPA
assignment-group categories that are actually present in the user's visible Canvas data. Category matching uses only the
literal Canvas group label (for example `Hệ số 2`, `HS2`, or `HK1-HS1.2`); it does not infer weights from
assignment titles or course names. The assignment-score breakdowns therefore describe graded Canvas
submissions as percentages of possible points, not a calculated 4.2-scale GPA. Canvas subject choices
include only accounts directly assigned to visible courses, and the server applies student access
scope before grouping. Dynamic filters still require summing compact stored rows; the
server does not store cross-user response caches, which could reveal another user's students.

The administration report groups learning activity status and graded results by month, Canvas
subaccount, course, grade, and homeroom. The subject leadership report keeps each assessment
group separate in its student/course detail and emphasizes score comparisons. It also shows a
separate Canvas final-course-score section by course, grade, homeroom, and reporting month.
That section uses course-score aggregates and is not affected by assessment categories;
the aggregate does not preserve a named grading-period ID. Completion means
submitted divided by submitted plus non-excused Canvas-flagged missing work; grading coverage
means graded submitted work divided by submitted work. Neither asserts teacher comments or
independent assessment outcomes. Red/amber/green classroom rules and support-exit decisions
are not defined by the available structured data.

## School score views

The school's spreadsheet was used only to identify the questions users need answered. `/reports`
presents those questions as separate choices for Ban giám hiệu and Ban chuyên môn instead of
reproducing spreadsheet sheets, labels, rows, subjects, or values.

Every runtime value comes from the Canvas warehouse. The default 0–10 views calculate one result
per student and Canvas course from total earned points divided by total possible points for eligible
graded assignments, then divide by ten and round to one decimal. Ungraded and excused submissions
do not contribute. The highest and lowest lists average the available course results for each
student. A separate view shows Canvas's imported current course score on its native 0–100 scale.
That value is a changing Canvas course measure and is not an official GPA.

The letter distribution uses the application's established transcript scale, collapsed into the
five bands requested by these reports: **D < 5; C 5–<6.5; B 6.5–<8; A 8–<9.5; A+ ≥ 9.5**.
The C+D share is the count in C or D divided by all scored student-course results in the row. The
histogram has a 0–<5 bin followed by half-point bins through 9.5–10+, retaining extra credit in the
last bin.

HS1, HS2, HS3, and GPA categories are derived only from literal tokens in actual Canvas assignment
group names. Assignment, distribution, histogram, and ranking reports always use graded
assignment-group points and expose the category filter. The dedicated current-course-score report
always uses Canvas `current_score` and does not expose that category filter. Users do not select a
score source manually.

School-level and grade filters use stored course and student grade fields. The Canvas subject selector
groups grade-specific Canvas subaccounts directly assigned to reportable courses by their subject
name; it does not repeat structural school, division, or grade ancestors already represented by other
filters. Selecting a subject includes all matching grade-specific accounts and their descendants.
Display labels remove only a recognized leading grade code such as `G5_`, `G10_`, or `G10IB_`;
filtering still uses the original Canvas account IDs, while the
warehouse retains the exact Canvas account name. Each reportable Canvas course remains distinct and
displays its exact Canvas course name, so newly synced courses appear without code changes or a fixed
subject catalog. Homeroom views use the SIS student homeroom; class-subject views use actual Canvas
section memberships and names. Selecting a Canvas subject narrows the Canvas section choices to sections
whose courses belong to that account or one of its descendant accounts, and clears an incompatible
section selection. School level, grade, and homeroom choices are linked through stored
student grade–homeroom pairs, with the student grade taking precedence over a course-grade fallback.

The grade-distribution view requires a subject scope, supplied by one or more Canvas subjects or
an exact Canvas course. The homeroom-distribution view additionally requires a grade, and the
class-subject view requires a Canvas section name. Until those inputs are selected, the UI shows a
required-selection message instead of zero-valued summary cards. Selected-task completion similarly
requires a Canvas course followed by one or more actual Canvas assignments.

The score views read `CanvasLeadershipReportAggregates` and `CanvasDashboardMetricAggregates`.
Those tables are rebuilt from stored Canvas facts after a completed ETL run, so report requests do
not scan raw submissions. Canvas drop rules and weighted assignment groups can make the calculated
earned/possible result differ from Canvas's whole-course score; the UI identifies the source assigned
to each report.

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

For deployment, apply the API migrations and start the API. The Canvas ETL materializes reports
after each successful import. For an initial build or independent retry, run
`dotnet run --project backend/OlympiaStudent360.CanvasEtl -- --rebuild-dashboard-aggregates-only`
with the production database connection. Production API backfills are disabled by default to
avoid holding warehouse locks and transactions in CPU-throttled App Runner instances; always-on
hosts can opt in with `Canvas__EnableApiReportBackfill=true`. Rebuilds accumulate and write one
course at a time to bound API memory, while publishing
all facts and the build marker in one transaction. The first report snapshot remains unavailable
until that transaction commits; later refreshes keep the previous compatible snapshot readable. Compare
`CanvasDashboardAggregateBuilds.LastCompletedSyncRunId` with the highest completed
`CanvasSyncRuns.Id`; a lower build cursor means the aggregate is pending or failed and needs a CLI
retry (or the optional API worker). Then run the API once with
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
