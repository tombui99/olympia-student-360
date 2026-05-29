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

Canvas integration, Excel upload, and real dashboard analytics are not implemented yet.

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

## Database

The application uses EF Core migrations. On backend startup, `DbInitializer.Seed(app)` runs:

1. Applies pending migrations
2. Creates roles: `Admin`, `Principal`, `Teacher`, `Leadership`
3. Seeds the default admin user if it does not already exist

Default seeded account:

```txt
username: admin
password: Admin123!
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

The next backend startup will recreate the database and seed the admin user.

## Run With Local MySQL

Start MySQL, set your connection string, then run the backend:

```bash
export ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=olympia_student_360;User=root;Password=YOUR_PASSWORD;"
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
