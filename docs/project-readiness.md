# Quality fixes and readiness note

Updated: 2026-11-25 (Asia/Tbilisi)

This note records follow-up work after the baseline audit in `VERIFICATION_REPORT.md`. The audit remains a dated snapshot of the pre-fix state; its original score and failure counts are not current results.

## Completed fixes

- Fixed the two integration tests that extracted email URLs incorrectly. They now parse a URL from the message body rather than treating the final word as a link.
- Aligned the invalid-grade test with the API's validation contract: invalid request data returns HTTP 400 Problem Details. Grades above the assignment maximum are still rejected.
- Added the brief-compatible course, lesson, assignment, submission, grading, and user route aliases while retaining the existing role routes and authorization checks.
- Added course duration, GEL price, difficulty, and optional prerequisites to the domain model, API DTOs, SQL schema/migrations, and teacher/admin forms. Existing rows migrate to safe defaults. Course price is displayed information; payment processing is not implemented.
- Removed the 320px minimum body width that caused a horizontal scrollbar at the 319px viewport used by the audit.
- Updated `README.md` and database/endpoint documentation for the 10.0 grading maximum and course details. Added a presentation outline and role-by-role demo script in `docs/presentation.md`.

## Verification run

| Check | Result | Evidence |
|---|---|---|
| .NET unit tests | PASS | 21 passed, 0 failed. |
| .NET integration tests | PASS | 52 passed, 0 failed; this includes the three cases that failed in the baseline audit. |
| Frontend tests | PASS | 3 tests across 2 test files passed. |
| Angular production build | PASS | `npm run build` completed. |
| EF model/migration consistency | PASS | `dotnet ef migrations has-pending-model-changes` reported no pending model changes. |
| Local SQL migration | PASS | `docker compose run --build --rm migrate` completed successfully against the existing development database. |
| Docker API rebuild and smoke check | PASS | API and SQL containers healthy; live/ready and Swagger JSON return 200; 24 catalog courses return the new duration, price, difficulty, and prerequisites fields. |
| Brief route registration | PASS | Swagger lists all eight required resource paths, including `/api/assignments/{id}/submit` and `/api/assignments/{id}/grade`; anonymous probes of the two protected assignment routes return 401. |
| Mobile/tablet widths | PASS | In Chromium, document/body scroll widths matched viewport at 319, 320, 360, 375, and 768px. |
| Desktop width | PASS | At 1440px, document/body scroll widths matched the viewport. |
| Public/protected route layout | PASS for overflow | 12 routes measured at 319, 360, 375, and 768px. Protected routes redirected to sign-in when opened without a session. |

The authenticated role workflows are API-tested with role-specific test identities, but a live browser sign-in run for every role is still pending. Use the manual walkthrough in `docs/presentation.md` before presenting. This is also why the deployment checklist keeps the full live role journey as an open item.

## Presentation/deployment status

Presentation material is prepared in `docs/presentation.md`. Deployment is not yet cleared: finish the Week 4 security/configuration, production Docker, backup/restore, and deployment documentation items in `docs/checklist.md`, then run the manual role walkthrough against the intended release configuration. No deployment was performed.
