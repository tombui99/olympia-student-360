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

The `OlympiaStudent360.CanvasEtl` command is a durable direct Canvas-to-MySQL import; it does not
route a long-running sync through an HTTP request. It imports the account hierarchy, student user
directory, courses, sections/classes, assignments, student enrollments and grades, and submissions
through the Canvas REST API. It follows Canvas pagination, retries rate limits and transient server
errors, and performs idempotent EF Core upserts. Student identity is resolved using the durable
Canvas user id first, with SIS id as a fallback:

```txt
StudentProfile.CanvasUserId == Canvas user.id
StudentProfile.SISUserId == Canvas user.sis_user_id (when Canvas exposes it)
```

SIS remains the future source of truth for official names and demographics. Until SIS arrives, the
ETL can create a minimal profile using the exact Canvas `sis_user_id` as both key and placeholder
display name; no Canvas name is guessed or promoted to an official SIS name. A separate
`CanvasGradeLevel` fallback is derived only from structured Canvas grade-account membership.
Official SIS `GradeLevel` takes precedence after SIS is imported. Ambiguous identity matches stop
the job rather than merging records.

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

Configure these GitHub `production` environment values. The RDS connection secret is optional;
when it is absent, the workflow reads the connection already configured on App Runner.

```txt
Secrets:
CANVAS_ACCESS_TOKEN             Canvas API token with account/course grade access
AWS_DEPLOY_ROLE_ARN             Existing deployment role used by the backend workflow
OLYMPIA_RDS_CONNECTION_STRING   Optional direct override

Variables:
AWS_REGION
APP_RUNNER_SERVICE_ARN
CANVAS_BASE_URL                 Optional; defaults to Olympia's Canvas host
CANVAS_ACCOUNT_ID               Optional; defaults to Olympia's root account 1
```

Run `.github/workflows/sync-canvas-all.yml` manually once for the initial import. Olympia's workflow
is deliberately bounded to enrollment terms 227-232 and 333-340, covering the 2025-2026 and
2026-2027 school years that overlap calendar year 2026. It rereads assignments, submissions,
enrollments, course grades, and grading-period grades for those terms and establishes the first
successful watermark.

After bootstrap, `.github/workflows/sync-canvas.yml` is the incremental workflow. The importer
reads the last successful watermark and subtracts the configured two-hour
overlap. Canvas submissions are queried separately with `submitted_since` and `graded_since`, then
deduplicated before upsert. The watermark advances to the run's captured start time only after every
course succeeds. A failed or cancelled run leaves the previous successful watermark unchanged, so
the next run safely repeats the window. Partial incremental responses never prune older submissions.

Canvas does not expose the same updated-since filter for courses, assignments, enrollments, or
enrollment/course-grade totals. Those dimensions and smaller totals are refreshed for available
courses in the configured current terms on each daily run. The weekly
`.github/workflows/reconcile-canvas.yml` job reloads the previous 30 days across the 2025 and current
terms to catch late grading corrections. All three workflows share one concurrency group.

For a local run, store credentials in the ETL project's .NET user-secret store. The ETL connects
directly to Canvas and MySQL, so a long import is not tied to a browser, API request, or web-host
timeout. The hidden prompt keeps the Canvas token out of shell history:

```bash
dotnet user-secrets set --project backend/OlympiaStudent360.CanvasEtl "Canvas:BaseUrl" "https://lms.theolympiaschools.edu.vn"
dotnet user-secrets set --project backend/OlympiaStudent360.CanvasEtl "Canvas:AccountId" "1"
dotnet user-secrets set --project backend/OlympiaStudent360.CanvasEtl "Canvas:EnrollmentTermIds" "333,334,335,336,337,338,339,340"
dotnet user-secrets set --project backend/OlympiaStudent360.CanvasEtl "Canvas:SyncAllEnrollmentTermIds" "227,228,229,230,231,232,333,334,335,336,337,338,339,340"
dotnet user-secrets set --project backend/OlympiaStudent360.CanvasEtl "Canvas:WatermarkOverlapMinutes" "120"
dotnet user-secrets set --project backend/OlympiaStudent360.CanvasEtl "Canvas:CreateMissingStudentProfiles" "true"
dotnet user-secrets set --project backend/OlympiaStudent360.CanvasEtl "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=olympia_student_360;User=root;Password=YOUR_PASSWORD;"

read -s "CANVAS_TOKEN?Canvas token: "; echo
dotnet user-secrets set --project backend/OlympiaStudent360.CanvasEtl "Canvas:AccessToken" "$CANVAS_TOKEN"
unset CANVAS_TOKEN

dotnet run --project backend/OlympiaStudent360.CanvasEtl/OlympiaStudent360.CanvasEtl.csproj -- --sync-all --replace
```

Environment variables such as `Canvas__AccessToken` and
`ConnectionStrings__DefaultConnection` override local user secrets. Do not commit or log either
the Canvas credential or a database connection string.

Use `-- --sync-all` for a complete manual reconciliation; it requires
`Canvas:SyncAllEnrollmentTermIds`. Use `-- --incremental` for a watermark-driven local run or
`-- --reconcile-days=30` for a bounded correction replay.
Append `-- --replace` only to a full, non-incremental sync when intentionally replacing all Canvas
warehouse rows; the CLI rejects replacement with incremental or reconciliation responses.
Scheduled daily jobs use `Canvas__EnrollmentTermIds` and limit the account query to available,
published courses with student enrollments. Full and reconciliation runs additionally include
completed courses. Canvas warehouse tables preserve Canvas accounts, users, courses, sections,
enrollments, grades, assignments, and submissions separately from future SIS-owned classes and
student demographics. There is no manual course mapping file. A Canvas grade fallback is derived
only from the structured Canvas grade-account hierarchy and never by parsing a course title; an
official SIS grade takes precedence when SIS is imported. See `docs/canvas-etl-simple-guide.md` for the exact
field-by-field contract and `docs/canvas-etl-dotnet-guide.md` for the .NET runtime, watermark,
retry, transaction, and GitHub Actions flow.

The Canvas GitHub workflows use the optional production environment secret
`OLYMPIA_RDS_CONNECTION_STRING`, or obtain the deployed App Runner service's existing
`ConnectionStrings__DefaultConnection` through the configured AWS deployment role. They run this
same direct import. Their runner must have network access to RDS; use a VPC-connected self-hosted
runner or an ECS/Fargate task when RDS is private.
The automatic App Runner fallback uses the existing `AWS_DEPLOY_ROLE_ARN` secret plus the
`AWS_REGION` and `APP_RUNNER_SERVICE_ARN` production environment variables already used by the
backend deployment workflow.
Do not route a complete import through App Runner's HTTP endpoint: the import can take several
minutes and must outlive an HTTP request.

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
