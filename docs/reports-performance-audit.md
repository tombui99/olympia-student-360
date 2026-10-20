# Reports page data-loading audit

## Loading rules

- The `/reports` shell mounts only the selected report. It does not preload every report.
- Student, health, behaviour, meal, learning-review, and leadership detail lists use bounded page sizes. Normal UI requests are capped at 100 rows by the API; the UI currently requests 20 or fewer.
- Excel export is the only path that deliberately reads every result. It walks bounded pages and enforces the shared export limit.
- Summary cards and charts run `GROUP BY`, `SUM`, `COUNT`, and `DISTINCT` in the database. These queries must inspect the selected period to calculate correct totals, but do not return the underlying fact rows.

## Persisted aggregation rule

Report summaries never calculate from mutable Canvas, imported-record, or ERP source rows during
an HTTP request. They read persisted sufficient statistics and apply the user's filters to those
smaller tables. Request-time `SUM`/`GROUP BY` operations over persisted statistics are still needed
because the filter combinations are user-defined; precomputing every possible combination would be
unbounded.

| Data family | Persisted report source | Build marker | Refresh trigger |
| --- | --- | --- | --- |
| Canvas learning, score, and leadership | `CanvasDashboardMetricAggregates`, `CanvasLeadershipReportAggregates` | `CanvasDashboardAggregateBuilds` with the current schema version and completed sync run id | Immediately after a Canvas ETL run is durably marked complete; `CanvasReportBackfillWorker` retries interrupted or failed builds. |
| Imported behaviour, health, projects, mental health, bus, and student risk | `ImportedReportAggregates`, `StudentRiskSnapshots` | `ImportedReportBuilds` with the last source change id | `AppDbContext` writes an `ImportedReportChange` marker when source data changes; `ImportedReportAggregateWorker` coalesces changes and rebuilds outside the request path. A completed Canvas aggregate build also emits a change marker so Canvas-based risk snapshots refresh. |
| ERP meals | `ErpMealDailyAggregates`, `ErpMealReportDetails` | `ErpMealReportBuilds` with ERP sync and student change ids | `ErpMealReportAggregateWorker` rebuilds after a newer ERP sync or student change and retries failures. |

An API returns `503 Report aggregates are being prepared` (or an unavailable Canvas metric) when a
required build marker is missing. It does not fall back to an expensive raw-data calculation.
Canvas readers continue serving the last committed snapshot with a compatible schema while a
newer completed sync is materialized. The rebuild cursor still triggers retries until that
snapshot catches up. Canvas rebuilding holds only one course's metric keys in API memory and
writes entity batches without allocating a second warehouse-sized array.

Exact-record drilldowns are the deliberate exception to the persisted-summary source rule: health
visits and behaviour details read their source records because those records are the requested
output. Those endpoints use SQL `Count`, deterministic ordering, and bounded `Skip`/`Take`; they do
not calculate report cards or chart totals from those rows.

## Endpoint audit

| Report family | Source | Result handling |
| --- | --- | --- |
| Leadership | `CanvasLeadershipReportAggregates` | SQL summary/breakdowns plus SQL `Skip/Take` for detail rows. Later pages omit repeated summary payloads. |
| Score reports | `CanvasLeadershipReportAggregates` / `CanvasDashboardMetricAggregates` | SQL collapses facts to one student/course observation. Returned rows are bounded report summaries (matrix rows, score bands, or a selected top/bottom limit capped at 100), not a raw detail list. |
| Learning review | `CanvasDashboardMetricAggregates` | SQL grouping, count, order, `Skip/Take`; maximum 100 UI rows. |
| Health | `ImportedReportAggregates` / `StudentHealthVisits` | Month/year summaries group and paginate in SQL. Weekly summaries fold the much smaller daily aggregate table. Visit details paginate in SQL. |
| Behaviour | `ImportedReportAggregates` / `StudentBehaviours` | Summary reads the persisted aggregate table. Drilldown details paginate in SQL. |
| Meals | persisted meal measures | SQL grouping, count, order, `Skip/Take`; details paginate in SQL. |
| Supplemental reports | `ImportedReportAggregates` | Database summary with only the top 10 grouped values returned. |

## Repeated requests

Score, leadership, and learning-review filter catalogs are cached for the lifetime of the reports page. Moving between reports in the same family reuses the catalog instead of querying all filter choices again.

The leadership API accepts `includeSummary=false` for later pages and export pages. The first page returns cards and chart breakdowns; later pages return only their requested detail rows and paging metadata. The frontend retains the first-page summary while the user pages through the table.

## Local verification (2026-10-19 dataset)

- Leadership engagement: 8,485 matching detail records; page 1 returned 20 rows; page 2 returned 20 rows.
- Leadership page 2 with repeated summaries disabled: about 9 KB and 0.18 seconds locally.
- Health diagnosis and frequent-student month views returned no more than the requested 20 grouped rows.
- Backend and frontend production builds completed successfully, and `git diff --check` reported no whitespace errors.
