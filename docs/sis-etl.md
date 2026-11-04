# SchoolOnline SIS API and ETL

SchoolOnline API v1 replaces the retired SIS Excel importer. SIS owns student names, student codes,
demographics, current grade/homeroom and parent/guardian contacts. Canvas supplies its submissions,
enrollment facts and grade fallback. The source is the supplied SchoolOnline API v1 manual
(31 October 2026); no credentials are included in this repository.

## Academic score ownership and current coverage

The school has confirmed that academic scores for **grades 1–4 belong to SIS**, while scores for
**grades 5–12 belong to Canvas**. This is separate from the SIS roster, which covers all grades.
The current academic score ingestion and report calculations are Canvas-based. There is no SIS
academic score importer or primary-school score read model yet; syncing primary-school profiles,
parents and health visits does not make their academic results available.

The supplied API v1 manual documents 14 data endpoints and no academic scores/results endpoint.
`/courses` provides class/membership metadata and `/comments` provides daily comments; neither is an
academic score feed. The vendor must provide the academic-results API contract (including grading
scales and assessment/term identifiers) before grades 1–4 can be integrated. Missing primary scores
must remain unavailable and must not be interpreted as zero or substituted with Canvas scores.

## Test in Postman

Import both files, select the imported environment, and fill **local/private** values for `client_id`
and `client_secret` from the issued credentials:

- `docs/postman/schoolonline.postman_collection.json`
- `docs/postman/schoolonline.postman_environment.json`

Keep these values local or use Postman Vault. Do not export or share a populated environment.
Live access was verified on 4 November 2026: the current school year is `278`, and accessible school IDs are `108`, `109`, and `110`. The environment includes the current year; the optional school filter remains disabled.

Run **01 - Get access token** first. It saves `access_token` in the selected environment.
Authentication uses HTTP Basic plus an `application/x-www-form-urlencoded` body:
`grant_type=client_credentials`. Do not simultaneously send client credentials in the body.
All data requests inherit Bearer authentication. Base URL:
`https://data-sync-api.schoolonline.edu.vn`.

| Method | Path | Scope | Filters beyond `limit`, `cursor` |
| --- | --- | --- | --- |
| POST | `/oauth/token` | Client credentials | `grant_type=client_credentials`; optional `scope` |
| GET | `/api/v1/schools` | `catalog.read` | U |
| GET | `/api/v1/students` | `students.read` | Y, S, U; includes father, mother, guardian |
| GET | `/api/v1/employees` | `employees.read` | S, U |
| GET | `/api/v1/homeroom-classes` | `classes.read` | Y, S, U |
| GET | `/api/v1/courses` | `courses.read` | Y, S, U, `subject_id` |
| GET | `/api/v1/timetable` | `timetable.read` | Y, S, D; **no U** |
| GET | `/api/v1/substitutions` | `substitutions.read` | Y, S, U |
| GET | `/api/v1/attendances` | `attendance.read` | Y, S, D, U, required `type` |
| GET | `/api/v1/comments` | `comments.read` | Y, S, D, U, required `type` |
| GET | `/api/v1/bus/attendances` | `bus.read` | Y, S, D, U |
| GET | `/api/v1/health/profiles` | `health.read` | Y, S, U |
| GET | `/api/v1/health/physical-checks` | `health.read` | Y, S, U |
| GET | `/api/v1/health/diseases` | `health.read` | U |
| GET | `/api/v1/health/visits` | `health-visits.read` | Y, S, U |

Y = optional `school_year_id` from `/schools`; S = optional `school_id`;
U = optional ISO 8601 `updated_since` with timezone; D = **required** `from_date` + `to_date`
in `YYYY-MM-DD`, at most 31 days inclusive. `type` is `HOMEROOM` or `COURSE`.
Health visits need their own scope; `health.read` alone does not grant access.

The collection leaves optional filters disabled. For daily feeds, set `from_date` and `to_date`;
for attendance/comments, select the type. To paginate, copy `meta.next_cursor` into `cursor` and enable
that parameter. Keep the same school/year/type/date filters. Clear the cursor before changing an
endpoint or filter. Continue until `meta.has_more=false`. Only then save that scan's `meta.watermark`
and use it as `updated_since` on the next scan of **that same endpoint and exact filters**.
Never reuse a watermark across feeds. Postman encodes `+` in timezone offsets automatically.
`limit` supports 1–500. Timetable results replace the complete requested date range.

The manual describes static IP allowlisting. The integration owner has confirmed client credentials
alone are sufficient for this client, so the workflow uses a normal GitHub-hosted runner, as Canvas does.
If the vendor changes that policy, use `SIS_RUNNER_LABEL` for a runner with approved egress.

## Pipeline and warehouse

`backend/OlympiaStudent360.SisEtl` is a .NET 10 console program using the API project's existing
EF Core/MySQL stack, migration recovery and database retry strategy. It connects directly to the same
RDS database as Canvas; no long-running backend HTTP request is involved.

The SIS migrations, including `EnforceSchoolOnlineSourceOwnership`, configure:

| Table / fields | Purpose |
| --- | --- |
| `SisWarehouseRecords` | Latest complete JSON source documents plus indexed school/year/student/type/date metadata, source timestamps and deletion/activity flags |
| `SisSyncStates` | Server-issued watermark per source/client, endpoint and canonical filter hash; `ProjectsCurrentRoster` distinguishes successful current-roster imports from historical backfills |
| `SisSyncRuns` | Running/completed/failed/cancelled history with counts and sanitized errors |
| `StudentProfiles.SchoolOnlineUserId`, `SchoolOnlineSchoolId`, `SchoolOnlineSchoolYearId`, `SISIsActive`, `SISIsDeleted` | Stable provider identity and current roster status alongside the existing normalized school username |
| `SISStudentParents.SISIsDeleted` | Hide withdrawn contacts while preserving staff-authored `StudentParentNotes` |
| `StudentHealthVisits.SisSourceKey`, `SisVisitId`, `SisIsDeleted` | Idempotent SIS health visits and source deletion state; legacy rows without source IDs are archived outside live queries |

Source keys hash the API origin and client ID. Secrets and tokens never enter these tables. Source
JSON contains student/parent data and must receive the same database access, backup and retention
controls as the existing student warehouse. The latest document replaces nested arrays wholesale,
so current class memberships do not accumulate departed members. Attendance keys use `(type,id)`;
all other endpoint keys use `id`, following the vendor contract. IDs stay strings, including IDs larger
than JavaScript's safe integer range. Unknown fields and enum values remain in JSON.

Default resources are `schools,students,health/visits`. They supply the current roster, student/parent
contacts, and the existing health visit pages and reports. Employees, classes, and courses are no longer
fetched by default because the application has no SIS read models for those feeds. Previously fetched
raw documents are retained as warehouse history and are not consumed by the application. All 14
adapters remain available for explicit future use through `SIS_RESOURCES`, using path names without
`/api/v1/`, e.g. `bus/attendances`. Health visits require `health-visits.read`.
A configured resource without its token scope fails the job; no feed is silently skipped.
`schools` and `students` are mandatory.

Only **current** school years discovered in the full school catalog project into `StudentProfiles`.
An explicit year allowlist can backfill older years into the warehouse without overwriting the current
student's grade, homeroom or contacts. Stable `student_user_id` is stored separately from `SISUserId`;
the latter keeps its existing school-email username meaning for Canvas joins. Matching considers the
provider ID, permanent SIS student code and normalized email/username. Disagreeing identities fail the
page transaction; names never establish identity. Canvas IDs/facts, manual student records and
school-authored transcript fields are preserved. Existing localized father/mother/guardian rows are
updated in place. A null contact is hidden without deleting its row or staff contact note.

The student feed updates status and demographic fields, including explicit nulls. Deleted/inactive
students already linked by stable SIS ID or permanent code remain as profiles with status flags so existing historical relationships survive. Unlinked deleted accounts stay in the warehouse and do not create application profiles or claim email aliases. This handles retired accounts that reuse an active student's email prefix. A completed
full roster scan also marks previously seen students missing from that exact scope as `OUT_OF_SCOPE`;
it does not delete profiles. Empty student snapshots fail by default before reconciliation. The override
`Sis__AllowEmptyStudents=true` is only for an intentionally empty school/year scope.

The existing imported-report change queue is written with profile and health changes. After ingestion,
the CLI refreshes imported reports and ERP meal views on the GitHub runner before declaring the job
successful. Background API workers continue to handle later application changes. The health-visits projection writes into the existing `StudentHealthVisits` table used by `/health-visits`, student details and health reports. The source endpoint JSON for attendance, comments, buses,
health profiles/examinations and timetables remains available for future read models. Those optional feeds do **not** write into bus exceptions or behaviour records, which have different semantics.
There is no new frontend UI for those optional feeds in this change.

## Health visits used by the application

`/api/v1/health/visits` is projected into `StudentHealthVisits` in the same page transaction as its
warehouse documents. `(SisSourceKey, SisVisitId)` is unique, so replay/full reloads update the same
application row. The student links through `student.user_id` to `SchoolOnlineUserId`; an existing
legacy profile may also link by its permanent student code when no conflicting provider identity exists.
Unmatched visits keep their source name/class snapshot and remain subject to the page's existing access rules.

| SIS field | Application field |
| --- | --- |
| `visited_at` | `ExamTime`, converted to Vietnam wall time for the existing page/calendar filters |
| `student`, `homeroom_class` | Linked profile and source name/gender/class snapshot |
| `symptoms`, `diagnosis.name` | Symptoms and diagnosis |
| `severity` | Classification, with Vietnamese labels for the documented values |
| `treatment`, `medicine`, `notes` | Treatment, medication and source notes |
| `parent_notified` | Notification status |

Health visits are source-owned. SIS updates replace symptoms, diagnosis, classification, treatment,
medication, notes and notification state. The application provides no local create/import, custom-field,
edit or delete controls for these records; corresponding write APIs reject requests. Corrections belong
in SchoolOnline. Examiner is not supplied by this API and is not populated. Old custom-field values
are retained only as archived database context, outside the health API read models.

The page identifies the source as **SchoolOnline SIS**. Source `is_deleted` hides a visit from the page,
student details and all report queries while retaining its row. Legacy manual/Excel visit rows are
retained in the table but excluded from live pages and report aggregates; no name/date matching guesses
that two visits are the same event. The ownership migration queues an aggregate refresh.

## Current roster and local ownership

Current student lists, overview student counts, manual-entry searches, form student selection,
homeroom administration and Canvas ETL membership use `CurrentRoster`. After a successful current-year
SIS scan, only profiles explicitly active in SIS and not deleted are current. Unmatched Excel profiles
stop appearing in current selections. Before the first successful current-year scan, the old roster
remains usable. Historical-only scans do not activate the new roster policy. Student records missing the required active flag or school/year references fail the page transaction
without advancing its watermark. A successful reconciliation
also deactivates profiles remaining in prior school years. Profiles, Canvas links, report facts and
historical student details are preserved.

Campus and programme are **manual Student 360 fields**, as requested. Existing values and filters remain;
Admin, SchoolAdministration and Principal can maintain them on a student's profile using the separate
Student 360 details form (`PUT /api/students/{id}/local-details`). SIS never updates these two fields.
Education level is derived from the SIS grade using Olympia's existing school-level mapping: grades
1–4 Primary, 5–9 LowerSecondary, 10–12 UpperSecondary. Parent age-group and employment-sector export
attributes are retired from read models because the API does not supply them; birth year and occupation
continue to sync. Parent contact notes remain local, while withdrawn source contacts are hidden.

Legacy dashboard metric definitions and observations with source `SIS` remain archived in their tables.
They are excluded from metric catalogs, filter dimensions, calculations and drilldowns. Existing saved
charts referencing them show an unavailable notice; new report definitions cannot select them. Canvas,
ERP meals and manually managed behaviour, support, counselling, bus exception and project modules keep
their own data sources.

## Scheduling, failures and recovery

`.github/workflows/sync-sis.yml` runs at **02:30 Vietnam time daily**. Monday performs a full
reconciliation; other days use incremental watermarks. Manual modes are `incremental`, `full`,
`reports-only`, and `check-access`. GitHub concurrency prevents overlapping SIS jobs. The CLI holds the
existing Canvas warehouse lock, then the imported-report and ERP meal publisher locks in a fixed order.
This prevents report publication transactions from holding student foreign-key locks during SIS updates.
Locks are session-scoped and released on completion or failure; acquisition waits are bounded.

Page lookups are preloaded and existing profiles, contacts, warehouse documents and health visits are
written in parameterized batches. Profile/contact updates include only modified columns, preserving
manual campus/programme, parent notes and Canvas-owned fields. New profiles and contacts use EF
relationship fix-up to allocate linked IDs. Report publication also uses batches inside its existing
atomic replacement transaction, reducing database round trips over remote RDS.

Each scan starts from its last successful server watermark. Page upserts and student/parent updates
commit together; the watermark only commits after all pages and reconciliation succeed. A failure
replays from the previous watermark. Previously completed endpoint/filter scans may advance even
if a later endpoint fails; each is independently restartable. No local wall clock is used as the source
watermark. Missing states bootstrap with a full snapshot, so no separate Excel bootstrap is needed.
Catalog discovery always uses a full scan to notice school-year rollover and access-scope changes.

Daily feeds use stable **whole calendar-month** windows, each at most 31 days, so filter keys stay the
same across daily runs. Normal runs revisit months intersecting the last 31 days. The first run of each daily feed/school/year/type scope and
weekly full runs revisit months from the selected school year's start. Timetable also includes the next
14 days and always fetches snapshots. A complete timetable scan is buffered before its range is
replaced in one transaction; partial/failed/empty page downloads cannot clear the existing range.
A successful empty timetable snapshot does clear its range, as required by the vendor.

OAuth tokens are reused until 60 seconds before expiry. A data 401 refreshes once; other 4xx errors
fail immediately except 429. Requests are sequential, spaced by 1.1 seconds by default (under the
vendor's default 60 requests/minute). 429/503 honor `Retry-After`; 500/network/timeouts use bounded
backoff. Rate-limit exhaustion headers pause the next request. HTTP redirects are disabled and
request timeouts are 90 seconds. Logs/errors omit response bodies, tokens, credentials and SQL
parameters; API failures identify the endpoint, HTTP status and safe `X-Request-Id`. Database failures
include only the exception type and MySQL numeric error code, without SQL text or provider messages.

Admin monitoring: `GET /api/sis-sync/status` returns the latest 50 runs and filter watermarks. Existing
`/api/sync-history` remains the Canvas history API. For a failed job, correct access/configuration and
rerun the same mode. If the CLI reports that SIS data is committed but report refresh failed, use
`reports-only`: it refreshes derived views without fetching SIS, changing source watermarks or replaying
the import. It needs the database connection but no SIS credentials. Report publication retries are
separate from ingestion retries, and failed publication retains the previous committed report snapshot.
For older daily corrections outside the incremental lookback, run `full` or a
month-range backfill. In a local backfill, set `Sis__FromDate` and `Sis__ToDate`; these select whole
calendar months intersecting the requested dates, including their complete first/last months.

## Production configuration and first run

In GitHub repository **Settings → Environments → production**, add these secrets:

| Secret | Value |
| --- | --- |
| `SIS_CLIENT_ID` | The exact issued client ID |
| `SIS_CLIENT_SECRET` | The exact issued client secret |
| `OLYMPIA_RDS_CONNECTION_STRING` | Existing Canvas/RDS connection, reused unchanged |

The workflow can instead load the database connection from App Runner via the same OIDC role and
Secrets Manager/SSM fallback used by Canvas (`AWS_DEPLOY_ROLE_ARN`, `AWS_REGION`,
`APP_RUNNER_SERVICE_ARN`). The runner must be able to reach both SchoolOnline HTTPS and RDS.
No SIS credentials are required by the running web backend.

Optional production repository/environment variables:

| Variable | Default |
| --- | --- |
| `SIS_BASE_URL` | `https://data-sync-api.schoolonline.edu.vn` |
| `SIS_SCHOOL_IDS` | Empty = all accessible schools |
| `SIS_SCHOOL_YEAR_IDS` | Empty = discover current year(s) from `/schools` |
| `SIS_RESOURCES` | `schools,students,health/visits` |
| `SIS_LOOKBACK_DAYS` | `31` |
| `SIS_TIMETABLE_FUTURE_DAYS` | `14` |
| `SIS_REQUEST_INTERVAL_MS` | `1100` |
| `SIS_RUNNER_LABEL` | `ubuntu-latest` |

After the code is published with the user's Git approval, wait for the backend deployment and migrations
to finish. Run workflow mode `check-access` to verify credentials and configured scopes without any
database writes. Then run `full`: it fetches the production dataset directly from SIS into the existing
production RDS database, including `StudentHealthVisits`; no local validation data is copied. Inspect
run history, student/parent mappings, health-page/report counts and linkage, then let the schedule continue.
Successful CLI runs print aggregate active-student and linked/unlinked health-visit counts without personal data.
Production creates its own watermarks and is independent of the isolated local validation database. Migrations run automatically using
the same mechanism as Canvas; the SIS schema and ownership cleanup are included in interrupted-DDL recovery. No production import is required merely to review this change.

For local use, configure .NET user secrets on `backend/OlympiaStudent360.SisEtl` or inject protected
environment variables. Configuration keys are `Sis:ClientId`, `Sis:ClientSecret`,
`ConnectionStrings:DefaultConnection`, and the options shown above in `Sis:*` form. Do not put actual
credentials in shell scripts, checked-in settings, command history or exported Postman files.

```bash
dotnet run --project backend/OlympiaStudent360.SisEtl -- --check-access
dotnet run --project backend/OlympiaStudent360.SisEtl -- --full
dotnet run --project backend/OlympiaStudent360.SisEtl -- --incremental
dotnet run --project backend/OlympiaStudent360.SisEtl -- --reports-only
```

The obsolete `backend/OlympiaStudent360.SisImport` folder contained only ignored build/cache artifacts
and is removed. It had no remaining tracked project or solution references. Other Excel importers
serve manually managed Student 360 modules or ERP meal exports and remain in place.
