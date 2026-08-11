# Olympia Student 360

Olympia Student 360 is a full-stack starter platform for The Olympia Schools student analytics dashboard. It gives teachers, principals, and school leadership a secure login flow and a protected dashboard shell that can later connect to a MySQL data warehouse populated from Canvas LMS, student report files, and future school information sources.

This repository intentionally implements only the foundation:

- User authentication
- JWT login flow
- Protected dashboard route
- Professional admin dashboard layout
- Sidebar navigation
- Placeholder metric cards
- Backend/frontend structure ready for analytics modules

Canvas grade data can be synchronised into the warehouse by the daily GitHub Actions ETL job.

## Tech Stack

### Backend

- .NET 10 Web API
- MySQL
- Entity Framework Core code-first migrations
- ASP.NET Core Identity
- JWT bearer authentication
- OpenAPI / Swagger

### Frontend

- Angular 21
- Standalone Angular components
- TailwindCSS
- OpenAPI Generator workflow for Angular API services/models
- English and Vietnamese UI translation files

## Repository Structure

```txt
backend/
  OlympiaStudent360.sln
  OlympiaStudent360.Api/
    Controllers/
    Core/
      DTOs/
      Entities/
    Data/
    Migrations/
frontend/
  public/assets/i18n/
  scripts/
  src/app/
    api/
    auth/
    core/
    dashboard/
    main-layout/
README.md
docker-compose.yml
global.json
```

## Prerequisites

- .NET SDK 10
- Node.js 22 LTS or newer even-numbered LTS release
- npm
- MySQL 8+
- Optional: Docker Desktop, if you want to run MySQL from `docker-compose.yml`

Check local versions:

```bash
dotnet --version
node --version
npm --version
```

## Configuration

The backend reads the MySQL connection string from:

1. `ConnectionStrings__DefaultConnection` environment variable, when present
2. `backend/OlympiaStudent360.Api/appsettings.Development.json`
3. `backend/OlympiaStudent360.Api/appsettings.json`

For local development, prefer an environment variable so credentials are not committed:

```bash
export ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=olympia_student_360;User=root;Password=YOUR_PASSWORD;"
```

The committed development config uses a placeholder password:

```txt
Server=localhost;Port=3306;Database=olympia_student_360;User=root;Password=YourPassword;
```

JWT settings live under the `Jwt` section in `appsettings.json`. Replace the signing key for any deployed environment.

Workspace AI reports use the Gemini API. Keep the API key outside source control and provide it through configuration:

```bash
export GeminiApiKey="YOUR_GEMINI_API_KEY"
```

The default model is `gemini-flash-lite-latest`. It can be overridden without changing code:

```bash
export Gemini__Model="gemini-flash-lite-latest"
```

The workspace sends only the aggregated chart series currently visible after filters are applied; student-level drill-down rows are not included in the Gemini request.

## Database

The application uses EF Core migrations. On backend startup, `DbInitializer.Seed(app)` runs:

1. Applies pending migrations
2. Creates roles: `Admin`, `Principal`, `Teacher`
3. Seeds the default admin user if `SeedAdmin__Password` is configured and the user does not already exist

Default seeded username:

```txt
username: admin
```

The seeded admin password is intentionally not committed. Configure it with an environment variable before first startup:

```bash
export SeedAdmin__Password="CHANGE_ME_TO_A_STRONG_LOCAL_PASSWORD"
```

Optional seeded admin overrides:

```bash
export SeedAdmin__Username="admin"
export SeedAdmin__Email="admin@olympia.edu.vn"
export SeedAdmin__FullName="Olympia Administrator"
```

The default database name is:

```txt
olympia_student_360
```

To reset the local database:

```bash
mysql -h localhost -P 3306 -u root -p -e "DROP DATABASE IF EXISTS olympia_student_360;"
cd backend
dotnet run --project OlympiaStudent360.Api/OlympiaStudent360.Api.csproj
```

The next backend startup will recreate the database. It will seed the admin user only when `SeedAdmin__Password` is set.

## Canvas Daily ETL

The `OlympiaStudent360.CanvasEtl` command calls the authenticated backend endpoint
`POST /api/canvas-sync`. The backend then imports the account student directory, courses,
assignments, student enrollments and course grades, and submissions through the Canvas REST API.
It follows Canvas pagination, retries rate limits and transient server errors, and upserts through
the backend's EF Core data layer. The console job never connects to MySQL. The Olympia schema
remains the target contract: duplicate Canvas section
enrollments are collapsed into one student/course score, effective submission due dates are used
when available, and Canvas metadata is normalized into Olympia's reporting dimensions. Student
identity is resolved using the durable Canvas user id first, with SIS id as a fallback:

```txt
StudentProfile.CanvasUserId == Canvas user.id
StudentProfile.SISUserId == Canvas user.sis_user_id (when Canvas exposes it)
```

SIS is the source of truth for `StudentProfile`. Canvas never creates or deletes student profiles
and never updates names, grade levels, homerooms, campuses, or other SIS-owned fields. The Canvas
account user directory is used only as an identity lookup: `Canvas user.sis_user_id` must exactly
match an existing `StudentProfile.SISUserId`. After a successful match, the ETL may store
`CanvasUserId` as a cross-reference. Unmatched Canvas enrollments, scores, and submissions remain
in the warehouse with a null `StudentProfileId` and are relinked on a later run after SIS has
created the student profile. Ambiguous identity matches stop the job rather than merging records.

The ETL uses these Canvas API operations (all responses are paginated):

```txt
GET /api/v1/accounts/:account_id/users?enrollment_type=student
GET /api/v1/accounts/:account_id/courses
GET /api/v1/courses/:course_id/assignment_groups
GET /api/v1/courses/:course_id/assignments
GET /api/v1/courses/:course_id/grading_periods
GET /api/v1/courses/:course_id/enrollments
GET /api/v1/courses/:course_id/students/submissions
```

For a scoped Canvas developer key, allow the corresponding `url:GET|...` scopes, including
`url:GET|/api/v1/accounts/:account_id/users`. The Canvas user behind the token must also have
permission to read account users, courses, enrollments, grades, and submissions. Seeing
`sis_user_id` additionally requires permission to view SIS information.

Configure these GitHub `production` environment secrets:

```txt
CANVAS_BASE_URL          https://your-school.instructure.com
CANVAS_ACCESS_TOKEN      Canvas API token with account/course grade access
CANVAS_ACCOUNT_ID        Canvas root account id (often 1; verify in your instance)
CANVAS_ENROLLMENT_TERM_IDS  Comma-separated Canvas term ids selected for daily sync
OLYMPIA_API_BASE_URL     Public HTTPS URL of the Olympia backend
CANVAS_SYNC_API_KEY      Shared high-entropy key configured on the backend as CanvasSync:ApiKey
```

Run `.github/workflows/sync-canvas-all.yml` manually once for the initial import. Olympia's workflow
is deliberately bounded to enrollment terms 333, 334, 335, 337, and 338: the academic terms beginning
in 2026. It rereads assignments, submissions, enrollments, course grades, and grading-period grades
for those terms and establishes the first successful watermark.

After bootstrap, `.github/workflows/sync-canvas.yml` runs daily at 01:00 Vietnam time. The backend,
not the runner clock, reads the last successful watermark and subtracts the configured two-hour
overlap. Canvas submissions are queried separately with `submitted_since` and `graded_since`, then
deduplicated before upsert. The watermark advances to the run's captured start time only after every
course succeeds. A failed or cancelled run leaves the previous successful watermark unchanged, so
the next run safely repeats the window. Partial incremental responses never prune older submissions.

Canvas does not expose the same updated-since filter for courses, assignments, enrollments, or
enrollment/course-grade totals. Those dimensions and smaller totals are refreshed for available
courses in the configured current terms on each daily run. The weekly
`.github/workflows/reconcile-canvas.yml` job reloads the previous 30 days across the 2025 and current
terms to catch late grading corrections. All three workflows share one concurrency group.

For a local run, store credentials in the ETL project's .NET user-secret store. The hidden prompts
keep both keys out of shell history. Configure the same sync key in the backend and ETL projects:

```bash
dotnet user-secrets set --project backend/OlympiaStudent360.CanvasEtl "Canvas:BaseUrl" "https://lms.theolympiaschools.edu.vn"
dotnet user-secrets set --project backend/OlympiaStudent360.CanvasEtl "Canvas:AccountId" "1"
dotnet user-secrets set --project backend/OlympiaStudent360.CanvasEtl "Canvas:EnrollmentTermIds" "337"
dotnet user-secrets set --project backend/OlympiaStudent360.CanvasEtl "Canvas:SyncAllEnrollmentTermIds" "333,334,335,337,338"
dotnet user-secrets set --project backend/OlympiaStudent360.CanvasEtl "Canvas:WatermarkOverlapMinutes" "120"
dotnet user-secrets set --project backend/OlympiaStudent360.CanvasEtl "Backend:BaseUrl" "http://localhost:5279"

read -s "CANVAS_TOKEN?Canvas token: "; echo
dotnet user-secrets set --project backend/OlympiaStudent360.CanvasEtl "Canvas:AccessToken" "$CANVAS_TOKEN"
unset CANVAS_TOKEN

read -s "CANVAS_SYNC_KEY?Canvas sync API key: "; echo
dotnet user-secrets set --project backend/OlympiaStudent360.Api "CanvasSync:ApiKey" "$CANVAS_SYNC_KEY"
dotnet user-secrets set --project backend/OlympiaStudent360.CanvasEtl "Backend:SyncApiKey" "$CANVAS_SYNC_KEY"
unset CANVAS_SYNC_KEY

dotnet run --project backend/OlympiaStudent360.CanvasEtl/OlympiaStudent360.CanvasEtl.csproj -- --sync-all
```

Environment variables such as `Canvas__AccessToken`, `Backend__BaseUrl`, and
`Backend__SyncApiKey` override local user secrets, which is how the GitHub Actions job supplies
production credentials. The backend retains its own RDS connection configuration. Do not commit
or log either API credential.

Use `-- --sync-all` for a complete manual reconciliation; it requires
`Canvas:SyncAllEnrollmentTermIds`. Use `-- --incremental` for a watermark-driven local run or
`-- --reconcile-days=30` for a bounded correction replay.
Append `-- --replace` only to a full, non-incremental sync when intentionally replacing all Canvas
warehouse rows; the CLI rejects replacement with incremental or reconciliation responses.
Scheduled daily jobs use `Canvas__EnrollmentTermIds` and limit the account query to available,
published courses with student enrollments. Full and reconciliation runs additionally include
completed courses. Canvas warehouse tables store Canvas-aligned source fields only. There is no
manual course mapping file and no stored Olympia subject, year, period, or grade-level inference.
Reports join Canvas course names, enrollment-term names, grading-period titles, assignments, and
SIS-owned student dimensions at query time. See `docs/canvas-etl-simple-guide.md` for the exact
field-by-field contract and `docs/canvas-etl-dotnet-guide.md` for the .NET runtime, watermark,
retry, transaction, and GitHub Actions flow.

GitHub-hosted runners only require HTTPS access to the Olympia backend; RDS can remain private to
the backend network. The backend endpoint requires the dedicated sync key and passes the Canvas
token only in a request header. Rotate both credentials if either is exposed.

## Run With Local MySQL

Start MySQL, set your connection string, then run the backend:

```bash
export ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=olympia_student_360;User=root;Password=YOUR_PASSWORD;"
export SeedAdmin__Password="CHANGE_ME_TO_A_STRONG_LOCAL_PASSWORD"
cd backend
dotnet run --project OlympiaStudent360.Api/OlympiaStudent360.Api.csproj --urls http://127.0.0.1:5279
```

In another terminal, run the frontend:

```bash
cd frontend
npm install
npm start -- --host 127.0.0.1 --port 4200
```

Open:

```txt
http://127.0.0.1:4200
```

## Run MySQL With Docker

If Docker is installed, you can use the included MySQL service:

```bash
docker compose up -d mysql
```

The Docker service creates:

```txt
database: olympia_student_360
user: root
password: YourPassword
port: 3306
```

Then run the backend and frontend as described above.

## API Documentation

When the backend runs in development, OpenAPI is available at:

```txt
http://127.0.0.1:5279/openapi/v1.json
```

Swagger UI is available at:

```txt
http://127.0.0.1:5279/swagger
```

## Frontend Internationalization

Translation dictionaries are stored in:

```txt
frontend/public/assets/i18n/en.json
frontend/public/assets/i18n/vi.json
```

The UI currently supports English and Vietnamese. The selected language is stored in `localStorage` and can be changed from the header language toggle.

## OpenAPI Angular Client

The frontend includes an OpenAPI Generator script:

```bash
cd frontend
npm run generate:api
```

Run the backend first so the generator can read:

```txt
http://127.0.0.1:5279/openapi/v1.json
```

The initial project includes hand-written API service stubs under `frontend/src/app/api` so the app builds immediately. Regenerate the API client after backend contract changes.

## Useful Commands

Build backend:

```bash
cd backend
dotnet build OlympiaStudent360.sln
```

Build frontend:

```bash
cd frontend
npm run build
```

Add a migration:

```bash
cd backend
dotnet ef migrations add MigrationName \
  --project OlympiaStudent360.Api/OlympiaStudent360.Api.csproj \
  --startup-project OlympiaStudent360.Api/OlympiaStudent360.Api.csproj \
  --context AppDbContext \
  --output-dir Migrations
```

Apply migrations manually:

```bash
cd backend
dotnet ef database update \
  --project OlympiaStudent360.Api/OlympiaStudent360.Api.csproj \
  --startup-project OlympiaStudent360.Api/OlympiaStudent360.Api.csproj \
  --context AppDbContext
```

## Development Notes

- CORS is configured for `localhost` and `127.0.0.1` on frontend ports `4200` and `4201`.
- The login page does not prefill credentials.
- The dashboard metrics are placeholders until real analytics modules are implemented.
- Do not commit production secrets. Use environment variables or deployment secret storage.

## GitHub

Repository:

```txt
https://github.com/tombui99/olympia-student-360.git
```
