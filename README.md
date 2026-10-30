# OnlineLearningPlatform

A secure, production-style online learning platform: students discover courses, enroll, work through lessons and assignments, and track progress; teachers create and grade; administrators moderate.

> **Status: Week 1 - backend foundation in progress.** This README describes only what exists today. Sections marked *(planned)* are filled in as the features are built. See [docs/checklist.md](docs/checklist.md) for the live progress list.

## Roles

| Role | Can do (v1) |
|---|---|
| **Student** | Register, verify email, browse published courses, enroll, study lessons, submit assignments, discuss, see grades. Default role for every new account. |
| **Teacher** | Create and manage own courses and lessons, create assignments, grade submissions, moderate own course discussions. Assigned by an admin only. |
| **Admin** | Manage users and roles, approve or reject courses, moderate content. The first admin is seeded from configuration. |

Each user has exactly one role in v1.

## Technology

| Area | Choice |
|---|---|
| Backend | .NET 10, ASP.NET Core Web API (controllers), C# |
| Data | Entity Framework Core, Microsoft SQL Server |
| Identity | ASP.NET Core Identity, JWT access tokens, rotating refresh tokens in an HttpOnly cookie *(planned)* |
| Validation / logging | FluentValidation, Serilog *(planned)* |
| API docs | Swagger / OpenAPI *(planned)* |
| Tests | xUnit, Testcontainers with real SQL Server *(planned)* |
| Containers / CI | Docker, Docker Compose, GitHub Actions *(planned)* |
| Frontend | Angular + TypeScript, in `frontend/` *(Week 2)* |

## Repository layout

```text
src/
  OnlineLearningPlatform.Domain/          entities, enums, business rules (no dependencies)
  OnlineLearningPlatform.Application/     use cases, DTOs, validators, interfaces
  OnlineLearningPlatform.Infrastructure/  EF Core, Identity, email, file storage, background jobs
  OnlineLearningPlatform.Api/             controllers, middleware, auth setup, Swagger
tests/
  OnlineLearningPlatform.UnitTests/
  OnlineLearningPlatform.IntegrationTests/
frontend/    Angular app (Week 2)
deploy/      production compose, reverse proxy, backup scripts (Week 4)
docs/        architecture, database, endpoints, security notes
```

Dependencies point inward. See [docs/architecture.md](docs/architecture.md).

## Prerequisites

| Tool | Version used in development |
|---|---|
| .NET SDK | 10.0.401 (pinned in `global.json`) |
| Docker Desktop | with the WSL2 backend |
| Git | 2.50+ |
| Node.js / npm | 24.x / 11.x (only needed for `frontend/`) |

## Getting started

```powershell
git clone https://github.com/<your-account>/OnlineLearningPlatform.git
cd OnlineLearningPlatform
dotnet --version     # should print 10.0.401
dotnet tool restore  # installs the pinned dotnet-ef tool
dotnet build
dotnet test
```

Create or update the database (uses `ConnectionStrings:DefaultConnection`; see [docs/database.md](docs/database.md)):

```powershell
dotnet ef database update --project src/OnlineLearningPlatform.Infrastructure --startup-project src/OnlineLearningPlatform.Api
```

Running the API, the database and Mailpit together via Docker Compose is *(planned for Week 1, Step 5e)*.

## Documentation

- [Architecture and design decisions](docs/architecture.md)
- [Project checklist](docs/checklist.md)
- [Database design and ER diagram](docs/database.md)
- API endpoint inventory *(Week 1, Step 5)*

## Security

Never commit secrets. Local configuration goes in `.env` or .NET user-secrets, both git-ignored. Only `.env.example` (placeholders) is committed.
