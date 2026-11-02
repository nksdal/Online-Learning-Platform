# Project checklist

Legend: `[x]` done and observed, `[ ]` pending, `[~]` deferred (Tier 3, cut first if time is short).
Only tick an item after you have seen the real result (build output, test run, CI run).

## Environment (confirmed on the developer machine)

- [x] .NET SDKs 10.0.203 and 10.0.401 installed
- [x] Visual Studio Community 2026 18.10.3 with the ASP.NET and web development workload
- [x] Git 2.50.1
- [x] Node 24.11.1 / npm 11.6.2
- [x] WSL2 and Docker Desktop (`hello-world` ran)
- [x] SQL Server 2025 Express reachable at `.\SQLEXPRESS`

## Week 1 - Backend foundation

- [x] Step 3: scaffold script run, `dotnet build` succeeded, SDK pinned to 10.0.401
- [ ] Step 2/4: GitHub remote configured, `main` pushed, feature branch pushed (confirm with `git status -sb`)
- [x] Step 5a: Domain (`ApplicationUser`, `RefreshToken`, `AuditLog`) - builds, rules unit-tested
- [x] Step 5b: Application (`IApplicationDbContext`) - builds
- [x] Step 5c: Infrastructure (DbContext, Identity, `InitialFoundation` migration) - applied to SQL Server 2025 Express; 10 tables, 3 roles, unique `EmailIndex` verified
- [x] Step 5c: unit tests pass (15/15: `RefreshTokenTests`, `LayerDependencyTests`)
- [x] Step 5d: API pipeline (Serilog, FluentValidation, Problem Details, Swagger with JWT scheme, health checks) - build, 57 tests, and local endpoints verified
- [ ] .NET runtime updated to the latest 10.0.x patch (machine currently runs 10.0.12; advisories were fixed in 10.0.13)
- [x] Step 5e: Docker Compose (SQL Server 2025, Mailpit, API, migration job) with health checks - SQL Server, API, and Mailpit healthy; migration job exited successfully
- [x] `docs/endpoints.md` endpoint inventory (implemented foundation routes separated from planned routes)
- [x] `.env.example` with placeholders only
- [ ] First CI run green (confirmed on GitHub, not assumed)

## Week 2 - Auth, core backend, Angular foundation

- [x] CORS allow-list for the Angular frontend (`http://localhost:4200` by default; configured origins only)
- [x] JWT bearer authentication setup (signing key from secrets) and role policies - signed token issuance/validation and anonymous/authorized/role-protected integration checks pass
- [x] Rate limiting for authentication endpoints (5 requests per minute per IP)
- [x] Student registration and email verification; messages captured in Mailpit
- [x] Login and short-lived access-token issuance after email confirmation
- [x] Refresh-token rotation, family reuse detection, logout and logout-all
- [x] Forgot-password and password reset with email delivery and session revocation
- [x] Seeded admin from optional deployment secrets with `MustChangePassword`; protected routes require password change
- [x] User and role management (last-active-admin protection, audit log, immediate access-token invalidation)
- [x] Categories: public active-category list; admin create, edit, activate/deactivate; audit logging; `AddCategories` migration generated; build and 57 tests pass
- [x] Course draft, submission, admin approval/rejection, teacher resubmission, public published catalogue, and terminal archive; `AddCourses` migration generated and covered by lifecycle tests
- [x] Lessons and enrollments: teacher lesson management, student enrollment in published courses, enrolled-only lesson access, archive read access; `AddLessonsAndEnrollments` applied to Compose SQL Server; 64 tests pass (43 integration, 21 unit)
- [ ] Angular project, auth screens, course catalogue, course details, enrollment

## Week 3 - Learning workflows and features

- [x] Lesson completion and course progress: complete/reset lesson, per-course summary; `AddLessonProgress` applied to Compose SQL Server; 65 tests pass (44 integration, 21 unit)
- [x] Text assignments, deadline enforcement, attempt history (`MaxAttempts` defaults to 1), and teacher grading; `AddAssignmentsAndGrading` applied to Compose SQL Server; 66 tests pass (45 integration, 21 unit)
- [ ] Discussions, learning streaks, recommendations, deadline reminders
- [ ] Student, teacher and admin interfaces
- [ ] GitHub Actions CI (build, test, Docker image build)

## Week 4 - Quality and deployment

- [ ] Unit and integration test coverage of the required areas
- [ ] Security and authorization review
- [ ] Persist Data Protection keys (Identity tokens survive restarts and redeploys)
- [ ] Forwarded headers, HSTS and security headers behind the reverse proxy; restrict `AllowedHosts`
- [ ] Structured JSON console logging in production
- [ ] Production Docker configuration, backup strategy, deployment documentation
- [ ] DigitalOcean deployment (only after explicit approval)

## Deferred (Tier 3)

- [~] Video uploads
- [~] Submission file attachments
- [~] Advanced statistics dashboard
- [~] Production email provider (Resend vs Brevo comparison still to do)
- [~] Full HTTPS deployment (needs a domain or an agreed alternative)

## Open decisions

- [x] Course workflow decisions: rejected courses can be edited and resubmitted; archived courses are terminal; enrolled students retain read access to archived course material.
- ~~Swagger tooling~~ decided: Swashbuckle 10 (see docs/architecture.md).
- SQL Server container: local development uses SQL Server 2025 Developer (`2025-latest`); production edition and deployment settings are separate decisions.
