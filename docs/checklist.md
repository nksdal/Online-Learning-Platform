# Project checklist

Legend: `[x]` done and observed, `[ ]` pending, `[~]` deferred (Tier 3, cut first if time is short).
Only tick an item after you have seen the real result (build output, test run, CI run).

## Environment (confirmed on the developer machine)

- [x] .NET SDKs 10.0.203 and 10.0.401 installed
- [x] Visual Studio 2026 18.10.3
- [x] Git 2.50.1
- [x] Node 24.11.1 / npm 11.6.2
- [x] WSL2 and Docker Desktop (`hello-world` ran)
- [x] SQL Server 2025 Express reachable at `.\SQLEXPRESS`
- [ ] Visual Studio "ASP.NET and web development" workload confirmed (optional, only needed to run the API from Visual Studio)

## Week 1 - Backend foundation

- [ ] Step 2: private GitHub repo created, cloned, `main` is the default branch
- [ ] Step 3: `scaffold.ps1` run, `dotnet build` succeeds
- [ ] Step 4: README, docs and configuration files committed (initial commit on `main`)
- [ ] Step 5a: Domain (`ApplicationUser`, `RefreshToken`, `AuditLog`)
- [ ] Step 5b: Application (interfaces, DTOs, validators)
- [ ] Step 5c: Infrastructure (EF Core, Identity, first migration)
- [ ] Step 5d: Api (Serilog, FluentValidation, Problem Details, Swagger with JWT, health checks)
- [ ] Step 5e: Docker Compose (SQL Server, Mailpit, API) with health checks
- [ ] `docs/database.md` with ER diagram, `docs/endpoints.md` with endpoint inventory
- [ ] `.env.example` with placeholders only
- [ ] First CI run green (confirmed on GitHub, not assumed)

## Week 2 - Auth, core backend, Angular foundation

- [ ] Registration, email verification, login
- [ ] Refresh-token rotation, reuse detection, logout
- [ ] Seeded admin with `MustChangePassword`
- [ ] User and role management (last-admin protection, audit log)
- [ ] Categories, courses, publishing workflow, lessons, enrollments
- [ ] Angular project, auth screens, course catalogue, course details, enrollment

## Week 3 - Learning workflows and features

- [ ] Progress tracking, assignments, submissions, grading
- [ ] Discussions, learning streaks, recommendations, deadline reminders
- [ ] Student, teacher and admin interfaces
- [ ] GitHub Actions CI (build, test, Docker image build)

## Week 4 - Quality and deployment

- [ ] Unit and integration test coverage of the required areas
- [ ] Security and authorization review
- [ ] Production Docker configuration, backup strategy, deployment documentation
- [ ] DigitalOcean deployment (only after explicit approval)

## Deferred (Tier 3)

- [~] Video uploads
- [~] Submission file attachments
- [~] Advanced statistics dashboard
- [~] Production email provider (Resend vs Brevo comparison still to do)
- [~] Full HTTPS deployment (needs a domain or an agreed alternative)

## Open decisions

- Course workflow details: what happens to a `Rejected` course, whether `Archived` is terminal, what enrolled students see in an archived course (decide before Week 2 course coding).
- Swagger tooling for .NET 10 (Swashbuckle vs built-in OpenAPI plus Swagger UI): to be checked and decided in Step 5d.
- SQL Server container image tag and edition setting: to be verified in Step 5e.
