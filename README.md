# OnlineLearningPlatform

A secure, production-style online learning platform: students discover courses, enroll, work through lessons and assignments, and track progress; teachers create and grade; administrators moderate.

> **Status: Backend learning workflows and Angular student, teacher, and admin interfaces are implemented. Lesson video uploads, private submission attachments, and the admin statistics dashboard are included. Email uses Gmail SMTP; delivery requires a dedicated Gmail account and App Password.** See [docs/checklist.md](docs/checklist.md) for current verification and deployment tasks.

The local Development Compose stack can seed a sample catalog for the first Teacher account: 9 published courses, 45 lessons, and 18 assignments. Set `DEMO_CATALOG_ENABLED=false` in `.env` to turn it off. The sample courses use locally stored Unsplash photos as their covers, so the site does not depend on an external image host to display them. If the database has no Teacher account yet, assign the Teacher role and restart the API to seed the catalog.

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
| Identity | ASP.NET Core Identity, JWT access tokens, rotating refresh tokens in an HttpOnly cookie |
| Validation / logging | FluentValidation, Serilog |
| API docs | Swagger / OpenAPI via Swashbuckle |
| Tests | xUnit; in-memory API tests now, Testcontainers with real SQL Server for database workflows *(planned)* |
| Email | Gmail SMTP via MailKit; credentials supplied through environment variables or user-secrets |
| Containers / CI | Docker, Docker Compose, GitHub Actions *(CI pending)* |
| Frontend | Angular 21 + TypeScript, in `frontend/` |

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
frontend/    Angular student app
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
| Node.js / npm | 24.x / 11.x (needed for `frontend/`) |

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

Run the API (Development environment, uses your local SQL Server Express):

For direct local API runs, add Gmail SMTP settings to .NET user-secrets. Use a Gmail App Password, not your normal Google password:

```powershell
# Run init once per clone; then set the local key.
dotnet user-secrets init --project src/OnlineLearningPlatform.Api
dotnet user-secrets set "Authentication:Jwt:SigningKey" "<at-least-32-random-bytes>" --project src/OnlineLearningPlatform.Api
dotnet user-secrets set "Email:Smtp:Host" "smtp.gmail.com" --project src/OnlineLearningPlatform.Api
dotnet user-secrets set "Email:Smtp:Port" "465" --project src/OnlineLearningPlatform.Api
dotnet user-secrets set "Email:Smtp:Secure" "true" --project src/OnlineLearningPlatform.Api
dotnet user-secrets set "Email:Smtp:UserName" "your-account@gmail.com" --project src/OnlineLearningPlatform.Api
dotnet user-secrets set "Email:Smtp:Password" "your-gmail-app-password" --project src/OnlineLearningPlatform.Api
dotnet user-secrets set "Email:From" "your-account@gmail.com" --project src/OnlineLearningPlatform.Api
dotnet user-secrets set "Email:AppUrl" "http://localhost:4200" --project src/OnlineLearningPlatform.Api
dotnet run --project src/OnlineLearningPlatform.Api
```

Gmail SMTP uses `smtp.gmail.com` on port `465` with SSL. Google App Passwords require 2-Step Verification; create one for a dedicated account and use that same Gmail address for `EMAIL_USER` and `EMAIL_FROM`.

The signing key is required and must stay out of source control. Docker Compose reads it from the ignored `.env` file instead.

| URL (use the port printed in the console) | What |
|---|---|
| `/swagger` | Swagger UI. Enabled only when `Swagger:Enabled` is `true` (Development). |
| `/health/live` | Liveness: the process is up. |
| `/health/ready` | Readiness: the database is reachable. Returns 503 otherwise. |

### Run API and SQL Server with real email

Docker Compose starts SQL Server, applies the checked-in EF Core migrations, and runs the API. Configure Gmail SMTP in `.env` to deliver verification, password-recovery, and assignment-reminder emails. Mailpit is not used.

If `.env` does not exist yet, copy the example environment file. Replace the SQL Server password and JWT key, then set `EMAIL_USER` and `EMAIL_FROM` to your dedicated Gmail address and `EMAIL_PASS` to its App Password. `EMAIL_HOST` should be `smtp.gmail.com` and `EMAIL_PORT` should be `465`. Set `APP_URL` to `http://localhost:4200` for the PC, or to the PC's LAN address such as `http://192.168.1.50:4200` when opening email links from a phone on the same Wi-Fi.

Gmail SMTP is a temporary free sender for this project. For a public launch, switch to a transactional email provider and a verified sender domain; keep `APP_URL` set to the deployed frontend address.

Signed-in users can manage their name and time zone at `/account`. The sign-in and registration screens include a resend-verification action for users who did not receive the first email. Profile pictures, course covers, lesson videos, and submission attachments use Cloudinary: create a Cloudinary account and set `CLOUDINARY_CLOUD_NAME`, `CLOUDINARY_API_KEY`, and `CLOUDINARY_API_SECRET` in the ignored `.env` file. Leave them blank to keep the rest of the app running; media uploads return a clear unavailable response until all three values are configured. Compose passes the credentials only to the API, and the API secret is never sent to the browser.

Generate a random signing key in PowerShell with:

```powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
[Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

Paste the generated hex string into `.env` as the `JWT_SIGNING_KEY` value, and choose a strong SQL Server password. Keep `.env` local; it is ignored by Git. Start the stack:

```powershell
docker compose up --build --detach
docker compose ps
docker compose logs --follow api
```

For phone testing on the same Wi-Fi, set `FRONTEND_HOST` in `.env` to this PC's private IPv4 address (for example, `192.168.1.50`), set `APP_URL=http://192.168.1.50:4200`, and set `API_BIND_ADDRESS=0.0.0.0`. Then recreate the API with `docker compose up -d api` and start Angular with `npm run start:lan` from `frontend`. Open `http://<FRONTEND_HOST>:4200` on the phone. Both devices must be on the same Wi-Fi, and Windows Firewall must allow Node.js and Docker traffic on the private network. Existing email links still contain the old address; resend the verification email after restarting the services. A private Wi-Fi address is not reachable over mobile data or by public users. For deployment, set `APP_URL` to the deployed frontend's public HTTPS URL.

Local URLs:

| URL | What |
|---|---|
| `http://localhost:5220/swagger` | Swagger UI |
| `http://localhost:5220/health/live` | API liveness |
| `http://localhost:5220/health/ready` | API and database readiness |
| `http://localhost:5220/api/auth/register` | Student registration (`POST`) |
| `http://localhost:5220/api/auth/verify-email` | Confirm an email (`POST`) |
| `http://localhost:5220/api/auth/resend-verification` | Resend confirmation (`POST`) |
| `http://localhost:5220/api/users/me/password` | Change password (`POST`, bearer token required) |
| `http://localhost:5220/api/categories` | List active categories (`GET`) |
| `http://localhost:5220/api/courses` | Browse published courses (`GET`) |
| `http://localhost:5220/api/users/me/enrollments` | List the current student's enrollments (`GET`, Student bearer token required) |
| `localhost,14330` | SQL Server, for local database tools (avoids colliding with a local SQL Express instance) |

### Run the Angular frontend

With the Compose API running, start the frontend in a second PowerShell window:

```powershell
cd frontend
npm start
```

Open `http://localhost:4200`. The student experience supports registration, email confirmation through a real inbox, sign-in, password recovery, published-course browsing, enrollment, a “My Learning” dashboard, lesson reading and video, assignment attempts with file attachments and grading feedback, and course discussions/replies. Change the API address in `frontend/src/environments/environment.ts` if the API uses a different local URL.

By default, published ports bind to `127.0.0.1`. When `API_BIND_ADDRESS=0.0.0.0` is set for same-Wi-Fi phone testing, only the API is exposed to the local network; SQL Server remains local. SQL Server data uses a named Docker volume and survives container recreation. Stop the stack with `docker compose down`; this preserves the database. `docker compose down --volumes` deletes the local database.

To try registration, send a `POST` to `/api/auth/register` with JSON containing `email`, `firstName`, `lastName`, and `password`. Follow the confirmation link delivered to that address. For API-only development, copy the link's `userId` and `token` values into a `POST` to `/api/auth/verify-email` through Swagger. Then send `email` and `password` to `POST /api/auth/login`; a confirmed, active account receives a short-lived JWT in the response and a 30-day refresh token in an HttpOnly cookie. The `/api/auth/refresh` and `/api/auth/logout` requests require the cookie plus `X-Refresh-Request: 1`; `/api/auth/logout-all` also requires a bearer access token. Registration responses intentionally do not reveal whether an email is already registered.

Password recovery uses `POST /api/auth/forgot-password` with an email address. For a confirmed, active account, Gmail SMTP delivers a reset link that opens the Angular reset-password page. The response for an unknown email is the same as for a known email. Reset and verification links contain random 32-byte tokens; only SHA-256 hashes are stored, and tokens are single-use. Verification links expire after 24 hours; reset links expire after 30 minutes. A successful reset revokes all refresh-token sessions for that account. If you are using Swagger instead, submit the link's `userId` and `token` with `newPassword` to `POST /api/auth/reset-password`.

To seed the first administrator, set `BOOTSTRAP_ADMIN_EMAIL` and `BOOTSTRAP_ADMIN_PASSWORD` in your ignored `.env` file, then recreate the API with `docker compose up --build --detach`. If either value is absent, startup skips admin seeding; if an admin already exists, startup leaves it unchanged. The initial admin can log in but must change the bootstrap password through `POST /api/users/me/password` before using other protected routes. That endpoint returns a new access token; replace the old one in Swagger's Authorize dialog.

Admins can inspect users with `GET /api/admin/users?page=1&pageSize=50`, change a user's role with `PUT /api/admin/users/{id}/role`, or activate/deactivate an account with `PUT /api/admin/users/{id}/status`. Role and status changes are recorded in `AuditLogs`, invalidate the target user's existing access tokens, and revoke refresh sessions. The last active admin cannot be demoted or deactivated. The current user can view their own profile at `GET /api/users/me`.

Admins manage course categories through `GET` and `POST /api/admin/categories`, `PUT /api/admin/categories/{id}`, and `PUT /api/admin/categories/{id}/status`. Deactivation hides a category from the public `GET /api/categories` list while preserving its record. Category changes are recorded in `AuditLogs`.

Teachers create courses as drafts with `POST /api/teacher/courses`, edit their own courses with `PUT /api/teacher/courses/{id}`, and submit drafts or rejected courses with `POST /api/teacher/courses/{id}/submit`. Admins can inspect every course state through `GET /api/admin/courses` and `GET /api/admin/courses/{id}`. They review `GET /api/admin/courses/pending`, then approve or reject with the corresponding `POST /api/admin/courses/{id}/approve` or `/reject` route (rejection requires a `reason`). Published courses appear in `GET /api/courses`; teachers can archive their published courses, and archived courses cannot be restored.

Students enroll in a published course with `POST /api/courses/{courseId}/enrollments`; duplicate enrollments return `409`. Their enrollments, including archived courses, are listed by `GET /api/users/me/enrollments`. Enrolled students can read lessons at `GET /api/courses/{courseId}/lessons`; access continues after the course is archived. Teachers manage lessons through `/api/teacher/courses/{courseId}/lessons` and `/api/teacher/lessons/{lessonId}`. Teachers upload/replace a lesson video with multipart `PUT /api/teacher/lessons/{lessonId}/video` (MP4 or WebM, maximum 100 MB) and remove it with `DELETE` on that route. Admins can read any course's lessons through `/api/admin/courses/{courseId}/lessons`.

Enrolled students can mark a lesson complete with `PUT /api/lessons/{lessonId}/progress` and reset it with `DELETE` on the same route. `GET /api/courses/{courseId}/progress/me` shows completed lessons, total lessons, and the percentage complete. Progress remains visible after archive, but cannot be changed once the course is archived.

Teachers create assignments with `POST /api/teacher/courses/{courseId}/assignments`. The optional `dueAtUtc` must be UTC (end with `Z`); `maxAttempts` defaults to 1 and `maxScore` defaults to 10.0, with grades capped at the assignment maximum. Enrolled students submit text through `POST /api/assignments/{assignmentId}/submissions` or submit text and/or one attachment through multipart `POST /api/assignments/{assignmentId}/submissions/with-attachment` (PDF, DOCX, XLSX, PPTX, TXT, CSV, PNG, JPG, or WebP; maximum 10 MB). Attachments are stored as private Cloudinary assets; students can download their own work, teachers can download submissions for their courses, and admins can access all attachments through the authenticated `/api/submissions/{submissionId}/attachment` route. Cloudinary download links are signed and expire after ten minutes. Teachers inspect attempts with `GET /api/teacher/assignments/{assignmentId}/submissions` and grade one with `PUT /api/submissions/{submissionId}/grade`, sending `{ "score": 8.5, "feedback": "Good work." }`. Students see their attempts and feedback through `GET /api/assignments/{assignmentId}/submissions/me`.

Courses store a duration label, a GEL price (zero means free), a difficulty level (`Beginner`, `Intermediate`, or `Advanced`), and optional prerequisites. Teachers enter these when creating or editing a course; administrators can update them during course editing.

Admins can view the analytics dashboard in the admin workspace or request `GET /api/admin/analytics?days=30`. The reporting window accepts 7, 30, 90, or 365 days. It summarizes active accounts, course status, enrollments, lesson completion, submissions and grades, includes a 12-month activity trend, and ranks courses by enrollment.

Enrolled students discuss course material through `GET` and `POST /api/courses/{courseId}/discussions`; replies use `POST /api/discussions/{postId}/replies`. Course teachers can hide or restore posts with `PUT /api/teacher/discussions/{postId}/moderation`; hiding requires a reason. Admins can inspect and moderate any course discussion. Student streaks are available at `GET /api/users/me/streak`; the calculation uses the student's configured time zone and completed lessons. `GET /api/users/me/recommendations` ranks published courses by the student's enrolled categories and course popularity.

The reminder worker checks every 15 minutes for assignments due within the next 24 hours and emails enrolled students who have not submitted. Failed sends are retried up to five times, and each student receives at most one reminder per assignment.

SQL Server uses the official `mcr.microsoft.com/mssql/server:2025-latest` Developer image. It is for local development, not production. The exact tag and password environment variable follow [Microsoft's SQL Server 2025 container quickstart](https://learn.microsoft.com/sql/linux/quickstart-install-connect-docker?view=sql-server-ver16).

## Documentation

- [Architecture and design decisions](docs/architecture.md)
- [Project checklist](docs/checklist.md)
- [Database design and ER diagram](docs/database.md)
- [API endpoint inventory](docs/endpoints.md) (implemented foundation routes vs planned routes)
- [Presentation outline and role-by-role demo walkthrough](docs/presentation.md)
- [Security review and deployment requirements](docs/security.md)

## Security

Never commit secrets. Local configuration goes in `.env` or .NET user-secrets, both git-ignored. Only `.env.example` (placeholders) is committed. The Development Compose stack is for local use; see the [security review](docs/security.md) before deployment.
