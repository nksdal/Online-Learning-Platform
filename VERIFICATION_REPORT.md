# Online Learning Platform — Verification Report

Review date: 2026-11-25 (Asia/Tbilisi)
Student: Not identified in supplied brief
Reviewer: Codex AI reviewer
Repository: C:\Projects\Online-Learning-Platform
Reviewed state: Current working tree, including pre-existing uncommitted changes; this is not a review of commit 3e6c2d6 alone.

> **Historical baseline:** This report records the pre-fix audit. Its 69/100 score, three integration failures, missing course fields/routes, and mobile overflow describe the state at that audit, not the current follow-up state. See [docs/project-readiness.md](docs/project-readiness.md) for the fixes and latest verification, and [docs/presentation.md](docs/presentation.md) for the role demo script. Authenticated browser journeys still need a manual run before presentation.

### 7.1 Cover & Executive Summary

**Environment:** Windows 11 Pro 10.0.26200; .NET SDK 10.0.401; Node.js 24.11.1; npm 11.6.2; Angular 21.2.25 / Angular Build 21.2.29; Docker Engine 29.8.1; SQL Server 2025 Developer Edition, product version 17.0.5035.5. The local API and SQL containers were healthy. Frontend: localhost:4200; API: localhost:5220.

**Overall verdict:** This is a substantial working learning-platform prototype. The C# / ASP.NET Core / EF Core / SQL Server backend builds, Angular builds, the app serves real database-backed course data, Swagger is available, and a clean temporary database was created from all 14 migrations. Identity, role policies, assignments, progress, discussions, media endpoints and administration are present. The grade is provisional because three integration tests fail, the authenticated flows and external providers were not exercised against the live stack, some endpoint paths and Course fields differ from the supplied contract, and the mobile page has horizontal overflow. **Provisional score: 69 / 100, plus 5 bonus points.** No hard-cap penalty applies.

**Top strengths**
- All six .NET projects build with zero warnings and zero errors.
- SQL migrations apply both to the existing database and a new temporary database; EF reports no pending model changes.
- Angular production build passes; all 3 frontend tests pass.
- 49 of 52 .NET integration tests pass, and all 21 unit tests pass.
- Live catalog data, 24 course cards and their photos load from the backend.
- Identity password hashing, JWT validation, Admin/Teacher/Student policies, single-use hashed account-action tokens, file validation and Problem Details handling are implemented.

**Top weaknesses**
- Three integration tests fail: two while parsing an email link from test text and one because the expected validation status differs.
- Several supplied endpoint paths are absent or use a different route. Equivalent features often exist under /api/teacher, /api/admin or /api/submissions.
- Course duration, price, difficulty and prerequisites are absent; financial reports, a lecture calendar and online consultations are also absent.
- At the observed narrow 319 px viewport, decorative elements cause a horizontal scrollbar. The requested 360/768/1024/1440 px checks were not possible because browser viewport control was unavailable.
- Deployment is not ready: Data Protection key persistence is pending, Docker runs the API in Development with Swagger enabled, and deploy/ contains only .gitkeep.

| Area | Signal | Summary |
|---|---|---|
| Frontend | 🟡 Amber | Builds and tests pass; public routes and data render; mobile overflow remains; authenticated UI and requested widths were not fully checked. |
| Backend | 🟡 Amber | Builds and starts; database/migrations pass; most integration tests pass; three fail and some contract paths do not match. |
| Docs & Quality | 🟡 Amber | Useful README and design docs; test suite and Git history exist; README has a stale maxScore value, no presentation materials, deployment work remains. |

**Not verified:** Live successful login as each role; role-specific UI journeys; sending Gmail messages; Cloudinary uploads/downloads; exact requested responsive widths; Lighthouse; production HTTPS/deployment; the two-month timeline because no selection date was supplied. No live email was sent, no Cloudinary asset was uploaded, and no user data was changed during probing.

### 7.2 Score Summary Table

| Criterion | Score | Max | Short justification |
|---|---:|---:|---|
| F1 Responsive design | 6 | 10 | Readable narrow mobile layout, but decorative overflow creates a scrollbar; other required widths unavailable. |
| F2 Angular structure | 8 | 10 | Standalone pages, lazy routes, services, typed models, strict TypeScript, guards, interceptor and reactive forms. |
| F3 UI functionality | 7 | 10 | Public catalog routes and backend data work; authenticated role journeys were not manually completed. |
| F4 State management | 7 | 10 | Angular signals and AuthService provide clear state; no centralized store; role flows not fully exercised. |
| **Frontend subtotal** | **28** | **40** | |
| B1 SQL database design | 8 | 10 | Real SQL schema has 25 foreign keys, 57 indexes and range constraints; required Course fields missing. |
| B2 EF models and migrations | 7 | 10 | 14 migrations apply cleanly to an empty DB; no model drift; spec fields absent and workflows not tested on SQL. |
| B3 REST endpoints | 6 | 10 | Swagger lists 76 operations and equivalents exist, but supplied routes differ; three tests fail. |
| B4 Authentication and authorization | 8 | 10 | Identity, JWT, hashed passwords/tokens, policies, refresh cookie, lockout and rate limits; two auth-flow tests fail at link parsing and live email is untested. |
| **Backend subtotal** | **29** | **40** | |
| A1 Code quality and documentation | 7 | 10 | Good layering, exceptions, logs, README and docs; failing tests, stale docs and dirty tree reduce confidence. |
| A2 Presentation readiness | 5 | 10 | Seeded data and polished pages; demo credentials/materials and a verified role-by-role walkthrough absent. |
| **Additional subtotal** | **12** | **20** | |
| **Final score** | **69** | **100** | Provisional; no score cap applied. |
| Additional functionality bonus | 2 | 3 | Discussions, recommendations/streaks, media workflows and admin analytics. |
| UI/UX bonus | 2 | 3 | Coherent visual identity and public pages; overflow remains. |
| Code quality bonus | 1 | 2 | Strong patterns, moderated by failing tests and stale docs. |
| Innovation bonus | 0 | 2 | No distinct innovation separately evidenced. |
| **Bonus** | **5** | **10** | Separate from the 100-point score. |
| **Score plus bonus** | **69 + 5** | | |

**Caps/penalties:** None. The app builds and starts; a clean database was created from migrations; passwords are hashed; protected endpoints require authorization; the frontend reads real backend data. No evidence of copied code left unadapted was found. This does not establish authorship.

### 7.3 Build & Run Log

| Command / check | Result | Evidence |
|---|---|---|
| dotnet build --nologo | PASS | Six projects built; 0 warnings, 0 errors; about 2 seconds. |
| dotnet test --nologo | PARTIAL | UnitTests 21/21 pass. IntegrationTests 49 pass, 3 fail, 0 skipped; 52 total; about 9 seconds. Overall command exits nonzero. |
| npm ci in frontend | BLOCKED initially | Windows EPERM unlinking frontend/node_modules/@esbuild/win32-x64/esbuild.exe, which was in use. |
| npm ci in clean temporary copy | PASS | 468 packages installed; npm reported 0 vulnerabilities. |
| npm run build | PASS | Angular production bundle generated; initial bundle 392.31 kB raw / 99.55 kB estimated transfer, plus lazy chunks. |
| npm test | PASS | 2 test files, 3 tests passed. |
| Frontend dependency recovery | PASS | Copied only missing ignored dependency files from clean install without overwriting locked files; re-ran project-folder build/tests, both passed. No source/lockfile changed. |
| docker compose ps | PASS | SQL Server healthy; API healthy; API on port 5220. |
| docker compose run --rm migrate | PASS | Existing database migration runner completed. |
| Fresh-database migration check | PASS | Temporary SQL DB OLP_Verification_20261125; all 14 migrations applied; 19 app tables plus migration history; then removed. |
| dotnet ef migrations has-pending-model-changes | PASS | “No changes have been made to the model since the last migration.” |
| GET /health/live and /health/ready | PASS | Both HTTP 200; ready is against SQL Server. |
| GET /swagger | PASS | HTTP 301 to Swagger UI; /swagger/v1/swagger.json returns HTTP 200. |
| GET /audit-not-found | PASS | HTTP 404, application/problem+json. |
| git diff --check | PASS with environment warnings | No whitespace errors; Git emitted existing LF-to-CRLF normalization warnings for edited files. |

**Integration test failures**
1. PasswordRecoveryTests.Valid_reset_changes_password_and_revokes_existing_refresh_session — UriFormatException at PasswordRecoveryTests.cs:55. The test extracts the last whitespace-delimited word from the email body as a URL, but the body has an expiry sentence after the URL. It attempts to parse “minutes.” rather than the link; the reset call is not reached.
2. RegistrationTests.Verification_link_confirms_the_email_and_invalid_details_are_non_enumerating — same extraction pattern; UriFormatException at RegistrationTests.cs:47. The verification call is not reached.
3. AssignmentTests.Student_attempts_are_preserved_and_teacher_can_grade_them — invalid score 11 is rejected with HTTP 400 while the test expects 422 at AssignmentTests.cs:110. The range rejection works, but test and API validation status disagree.

No workaround changed source code. The temporary dependency copy is under the Windows temporary directory, outside the repository.
### 7.4 Requirements Traceability Matrix

Status vocabulary: ✅ PASS; 🟡 PARTIAL; ❌ FAIL / MISSING; ⚪ N/A because optional; 🔍 UNVERIFIED. A partial requirement is present but is not counted as fully met.

| Req ID | Requirement | Status | Evidence | Notes |
|---|---|---|---|---|
| G4.1-1 | Back-end language C# | ✅ | OnlineLearningPlatform.Api and Domain; successful .NET build | |
| G4.1-2 | ASP.NET Core | ✅ | API Program.cs; running Kestrel container | |
| G4.1-3 | Relational SQL database | ✅ | Live SQL Server 2025 queried | |
| G4.1-4 | EF Core with migrations | ✅ | 14 migrations; existing and clean-database runs pass | |
| G4.1-5 | Angular frontend | ✅ | Angular production build and tests pass | |
| G4.1-6 | Responsive HTML/CSS | 🟡 | frontend/src/styles.scss media queries; 319 px browser inspection | Horizontal scrollbar; exact target widths unavailable. |
| G4.1-7 | REST API | ✅ | Swagger OpenAPI and HTTP probes | |
| G4.2-1 | Git with meaningful history | ✅ | 18 commits, descriptive feature/fix subjects, one author | Branches are optional. |
| G4.2-2 | Swagger/OpenAPI | ✅ | Swagger JSON returns 200; 76 operations discovered | |
| G4.2-3 | Meaningful tests | 🟡 | 21 unit pass; 52 integration with 3 failures; 3 frontend tests pass | Most integration tests use EF InMemory. |
| G4.2-4 | Error handling, logging and consistent errors | 🟡 | Program.cs Problem Details, exception handler, Serilog; Angular interceptor | Unknown route checked; not every failure path was run live. |
| G4.2-5 | Full README documentation | 🟡 | README.md documents stack, setup, roles, routes and workflows | Stale maxScore value; no presentation images. |
| G4.2-6 | Installation and run instructions | 🟡 | README.md, compose.yaml, .env.example | Useful local setup; no documented demo credentials; production setup incomplete. |
| G4.2-7 | Database scripts/schema/seed | 🟡 | EF migrations, docs/database.md and Compose migration service | No standalone SQL schema/seed script; EF migration path works. |
| G4.2-8 | Presentation materials | ❌ | docs contains architecture/checklist/database/endpoints markdown only | No slides/video/presentation package found. |
| G4.3-1 | Intuitive UI and clear feedback | ✅ | Live home, courses and how-it-works; loading/catalog states | Authenticated forms not manually exercised. |
| G4.3-2 | Secure data processing | 🟡 | Identity, JWT, roles, hashed tokens, rate limits and upload validation | Key persistence and production HTTPS/config pending. |
| G4.3-3 | Clearly defined role permissions | ✅ | AuthorizationPolicies and controller attributes; unauthenticated probes return 401; policy tests pass | |
| G4.3-4 | Registration/profile creation and update | 🟡 | AuthController, UsersController, account page and tests | Email-link parser failure prevents two auth tests completing. |
| G4.3-5 | Secure password recovery | 🟡 | AuthService; hashed one-use AccountActionTokens; generic response/rate limits | Recovery test fails before reset call due to test URL extraction. |
| G4.3-6 | Profile photo/personal information | ✅ | UsersController profile/picture routes and account page | Live Cloudinary upload not exercised. |
| G4.4 | Two-month timeline | 🔍 | Git history spans 2026-10-31 through 2026-11-04 | Selection date was not supplied. |
| G4.5-1 | Additional functionality eligibility | ✅ | Discussions, recommendations/streaks, analytics and media workflows | Bonus eligibility, not a core feature. |
| G4.5-2 | UI/UX bonus eligibility | ✅ | Distinctive design and public routes | Mobile overflow noted. |
| G4.5-3 | High code quality eligibility | 🟡 | Layered architecture and typed Angular app | Failing tests and stale docs reduce confidence. |
| G4.5-4 | Innovation eligibility | ✅ | Reviewed as eligibility criterion | No innovation bonus awarded. |

#### Project 1: Online Learning Platform

| Req ID | Requirement | Status | Evidence | Notes |
|---|---|---|---|---|
| P1-1 | Online learning platform purpose | ✅ | Courses, enrollments, lessons, submissions, grading and admin features | |
| P1-2 | Administrator, Teacher and Student roles | ✅ | Roles.cs, authorization policies and role tests | |
| P1-3 | Authentication, registration, profile, recovery and roles | 🟡 | AuthController, UsersController, AuthService and integration tests | Features present; email-link tests fail during extraction. |
| P1-4 | Courses include duration, difficulty and prerequisites | 🟡 | Domain/Catalog/Course.cs includes title, summary, description, category, instructor, status | Duration, difficulty and prerequisites absent; price is also absent. |
| P1-5 | Course materials video/PDF/resources; discussions and optional quizzes | 🟡 | TeacherLessonsController video workflow; discussion controllers | Generic lesson attachments/PDF presentations absent; discussions exist. |
| P1-5a | Quizzes (*) | ⚪ | No quiz model or route found | Optional in supplied brief. |
| P1-6 | Assignments, deadlines, grading and notifications/reminders | 🟡 | CourseAssignment, grading, reminders and email sender | Assignment type and new-assignment notification absent; due reminders exist. |
| P1-7 | Progress, grades, recommendations and teacher analytics | 🟡 | Progress endpoints, recommendation/streak service, teacher grading | Student progress/recommendations exist; comprehensive teacher analytics are limited. |
| P1-8 | Admin statistics: popularity, activity, performance | ✅ | AdminAnalyticsController/service and admin page | Counts, trends, enrollments and completion implemented. |
| P1-9 | Financial report generation | ❌ | No finance model, service or endpoint found | Course price field absent too. |
| P1-10 | Teacher creates course with title/description/duration/price | 🟡 | TeacherCoursesController | Title/description supported; duration/price absent. |
| P1-11 | Course category management | 🟡 | AdminCategoriesController; Teacher chooses category | Category creation/edit is Admin-managed, not Teacher-managed. |
| P1-12 | Teacher uploads PDF/video materials | 🟡 | TeacherLessonsController validates/uploads MP4/WebM | No general PDF/presentation lesson attachment flow. |
| P1-13 | Lecture calendar | ❌ | No calendar/schedule route or feature found | |
| P1-14 | Assignment creation and deadline setting | ✅ | TeacherAssignmentsController and request validator | |
| P1-15 | Student search and filtering | ✅ | CatalogPage/CoursesPage; live search and category filters | Client-side filtering of 24 live records. |
| P1-16 | Student enrollment | ✅ | EnrollmentsController, MyEnrollmentsController and tests | |
| P1-17 | Student access to materials | ✅ | StudentLessonsController and learning tests | |
| P1-18 | Assignment submission and grade view | ✅ | StudentAssignmentsController and submission models | |
| P1-19 | Progress tracking | ✅ | StudentProgressController and learner dashboard | |
| P1-20 | Comments/discussions | ✅ | StudentDiscussionsController and moderation routes | |
| P1-21 | Teacher monitors learner progress/activity | 🟡 | Teacher submissions/grading and progress services | Full analytics/problem-area view is limited. |
| P1-22 | Teacher grades assignments | ✅ | TeacherGradingController; valid 8.5 grade tested | Invalid score is rejected, but response code differs from test. |
| P1-23 | Online consultation planning | ❌ | No consultation/calendar routes found | |
| P1-24 | Teacher sends notifications | 🟡 | Assignment deadline reminder worker emails learners | Reminders exist; general teacher/new-assignment notifications not found. |
| P1-25 | Admin user management | ✅ | AdminUsersController and AdminUserManagementTests | Includes role/status and anonymization. |
| P1-26 | Admin approves/rejects courses | ✅ | AdminCoursesController and course tests | |
| P1-27 | Admin system statistics | ✅ | AdminAnalyticsController and admin page | |
| P1-API-AUTH | Spec AUTH endpoints | 🟡 | POST register/login/reset-password appear in OpenAPI | Blank-body probe returned 415; integration cases exist, two fail at link extraction. |
| P1-API-COURSES | Spec COURSES endpoints | 🟡 | Public GETs and Teacher/Admin equivalents exist | Exact POST/PUT/DELETE /api/courses not registered; archive replaces delete. |
| P1-API-LESSONS | Spec LESSONS endpoints | 🟡 | Student GET and Teacher mutation equivalents exist | Teacher mutation paths differ from contract. |
| P1-API-ASSIGNMENTS | Spec ASSIGNMENTS endpoints | 🟡 | Student list/submit and Teacher create/update/grade equivalents exist | Several contract mutation paths differ. |
| P1-API-USERS | Spec USERS endpoints | 🟡 | Admin list/delete and self-profile routes exist | Exact /api/users/{id} CRUD paths not registered. |
| P1-DATA-USER | User fields: id, email, password, names, role, avatar, createdAt | ✅ | ApplicationUser inherits IdentityUser<Guid>; role join; ProfileImageUrl and timestamps | Password is Identity PasswordHash, not plaintext. |
| P1-DATA-COURSE | Course fields: id, title, description, teacherId, category, duration, price, status, createdAt | 🟡 | Domain/Catalog/Course.cs and Courses table | Duration and price absent; instructor/category are foreign keys. |
| P1-DATA-LESSON | Lesson fields: id, courseId, title, content, attachments, orderIndex, createdAt | 🟡 | Domain/Catalog/Lesson.cs and Lessons table | Position maps orderIndex; video fields exist, generic attachments absent. |
| P1-DATA-ASSIGNMENT | Assignment fields: id, courseId, title, description, dueDate, maxScore, createdAt | ✅ | CourseAssignment and Assignments table | Instructions maps description; DueAtUtc maps dueDate. |
| P1-DATA-SUBMISSION | Submission fields: id, assignmentId, studentId, content, attachments, score, feedback, submittedAt | ✅ | AssignmentSubmission and AssignmentSubmissions table | Text and attachment metadata map to content/attachments. |
| P1-RULE-1 | Only course Teacher can modify that course | ✅ | CourseService ownership checks and CourseTests | |
| P1-RULE-2 | Student submits only when enrolled | ✅ | AssignmentService enrollment check and AssignmentTests | |
| P1-RULE-3 | Score cannot exceed maxScore | ✅ | Domain validator and SQL constraint; current values at most 10 | |
| P1-RULE-4 | Handle submissions after dueDate | ✅ | AssignmentTests late-submission case | |
| P1-RULE-5 | Only Admin deletes users/approves courses | ✅ | Admin controller policies and management/course tests | |
| P1-RULE-6 | Draft courses hidden from students | ✅ | Public catalog status filter and course tests | |
| P1-RULE-7 | Lessons ordered by orderIndex | ✅ | Lesson Position/order configuration and learning tests | |
| P1-RULE-8 | File upload validation | 🟡 | TeacherLessonsController and StudentAssignmentsController type/signature/size/name checks | No real Cloudinary transfer performed. |

**Traceability totals:** ✅ 38 / 🟡 28 / ❌ 4 / ⚪ 1 / 🔍 1.
Mandatory verifiable items exclude four bonus-eligibility rows, the optional quiz and conditional timeline: 35 fully met out of 66 (53.0%); 62 of 66 are at least partly implemented (93.9%). PARTIAL does not count as fully satisfied.

### 7.5 Endpoint Test Matrix

A no-credential request was made to each endpoint in the supplied contract. Protected current routes were also probed unauthenticated. Blank-body requests are route/auth probes, not successful happy paths. Integration tests provide role-specific cases; no live role credentials were used.

| Endpoint from brief | Method | Test case/auth | Expected | Actual | Result | Notes |
|---|---|---|---:|---:|---|---|
| /api/auth/register | POST | No body, anonymous | JSON accepted or validation error | 415 | 🟡 | Route exists; registration test fails later at email-link parsing. |
| /api/auth/login | POST | No body, anonymous | JSON accepted or validation error | 415 | 🟡 | Login integration tests cover credential behavior. |
| /api/auth/reset-password | POST | No body, anonymous | JSON accepted or validation error | 415 | 🟡 | Recovery integration test fails before reset call. |
| /api/courses | GET | Public catalog | 200 | 200 | ✅ | Live frontend loaded 24 cards. |
| /api/courses/{id} | GET | Random missing id | 404 | 404 | ✅ | |
| /api/courses | POST | No credentials | 201 per contract | 405 | ❌ | Exact route absent; Teacher create is /api/teacher/courses. |
| /api/courses/{id} | PUT | No credentials | 200 per contract | 405 | ❌ | Teacher/Admin edit routes differ. |
| /api/courses/{id} | DELETE | No credentials | 204 per contract | 405 | ❌ | Teacher archive replaces deletion. |
| /api/courses/{courseId}/lessons | GET | No credentials | 401 | 401 | ✅ | Requires Student policy. |
| /api/courses/{courseId}/lessons | POST | No credentials | 201 per contract | 405 | ❌ | Teacher route is /api/teacher/courses/{courseId}/lessons. |
| /api/courses/{courseId}/lessons/{id} | PUT | No credentials | 200 per contract | 404 | ❌ | Teacher route is /api/teacher/lessons/{lessonId}. |
| /api/courses/{courseId}/lessons/{id} | DELETE | No credentials | 204 per contract | 404 | ❌ | Teacher route is /api/teacher/lessons/{lessonId}. |
| /api/courses/{courseId}/assignments | GET | No credentials | 401 | 401 | ✅ | Student list route exists. |
| /api/courses/{courseId}/assignments | POST | No credentials | 201 per contract | 405 | ❌ | Teacher route is /api/teacher/courses/{courseId}/assignments. |
| /api/courses/{courseId}/assignments/{id} | PUT | No credentials | 200 per contract | 404 | ❌ | Teacher route is /api/teacher/assignments/{assignmentId}. |
| /api/assignments/{id}/submit | POST | No credentials | 201 per contract | 404 | ❌ | Actual route is /api/assignments/{assignmentId}/submissions. |
| /api/assignments/{id}/grade | PUT | No credentials | 200 per contract | 404 | ❌ | Actual route is /api/submissions/{submissionId}/grade. |
| /api/users | GET | No credentials | 401 | 404 | ❌ | Admin list is /api/admin/users. |
| /api/users/{id} | GET | No credentials | 401 | 404 | ❌ | No matching single-user route. |
| /api/users/{id} | PUT | No credentials | 200 per contract | 404 | ❌ | Self-profile and Admin status/role routes are separate. |
| /api/users/{id} | DELETE | No credentials | 204 per contract | 404 | ❌ | Admin route is /api/admin/users/{id}. |

The 405/404 results establish contract-path mismatch; equivalents are listed above. All **76 OpenAPI operations** were called unauthenticated: 57×401, 5×415, 11×429, 2×200 and 1×404. The 429 responses followed the configured 5-per-minute auth/profile rate limit during the burst. 415 came from anonymous auth probes with no JSON body or Content-Type. These are not successful authenticated tests.

**Relevant test coverage:** AuthenticationTests, LoginTests, RegistrationTests, PasswordRecoveryTests, RefreshTokenTests, AdminUserManagementTests, CourseTests, CategoryTests, AssignmentTests, LearningTests, CommunityAndActivityTests, ErrorHandlingTests, HealthCheckTests and SwaggerExposureTests. 49 of 52 integration tests pass. Policy rejection, enrollment, ownership, grading, late submissions, draft visibility, progress, recommendations and Admin actions have integration coverage; three failures are detailed in 7.3.
### 7.6 Data Model & Database Review

SQL Server currently contains 20 tables including __EFMigrationsHistory, 25 foreign keys, 57 indexes and 2 check constraints. It has 9 users, 24 courses, 94 lessons, 37 assignments and 1 submission. Counts only; no account names, email addresses or credentials were queried for this report. Nine course titles have duplicate rows; duplicates are visible on the public page.

The live range constraints are CK_Assignments_MaxScore_Range and CK_AssignmentSubmissions_Score_Range. Assignments.MaxScore is decimal(8,2). Current assignment max scores and recorded grades are within 0–10; largest values are 10.00.

**Model comparison**
- User: Identity user key/email/name, PasswordHash, role join records, ProfileImageUrl and timestamps cover the listed fields. Password is hashed.
- Course: title, summary, description, instructor, category, status, image and timestamps exist. Duration, price, difficulty and prerequisites are missing. Status adds PendingApproval, Rejected and Archived.
- Lesson: title, course, content, position and timestamps exist, plus video URL/public ID. Position corresponds to orderIndex. General attachments are not represented as an array or related resources table.
- Assignment: title, course, instructions, UTC due date, max score and timestamps exist. MaxAttempts is additional.
- Submission: assignment/student, text, attempt number, attachment metadata, score, feedback, submitted and graded timestamps exist.

**Relationship overview:** ApplicationUser 1-to-many Courses as instructor; Category 1-to-many Courses; Course 1-to-many Lessons, Enrollments, Assignments and DiscussionPosts; ApplicationUser 1-to-many Enrollments, LessonProgress, AssignmentSubmissions and DiscussionPosts; Lesson 1-to-many LessonProgress; CourseAssignment 1-to-many AssignmentSubmissions and DeadlineReminders; ApplicationUser 1-to-many RefreshTokens and AccountActionTokens. Identity join/claim/token tables also exist.

Fourteen migrations are checked in and applied. A temporary empty SQL Server database received all migrations and 19 application tables; it was then removed. The model snapshot is current per dotnet ef migrations has-pending-model-changes. README.md and docs/database.md describe setup/schema, but no standalone SQL script was found. Integration tests use EF InMemory, so SQL migrations are verified but application workflows against SQL were not.

### 7.7 Frontend Review

**Route inventory:** home (/), courses, how-it-works, account, course detail, teacher studio, admin workspace, student dashboard, lesson player, login, change-password, register, verify-email, forgot-password and reset-password, plus catch-all redirect. Teacher/Admin/Student pages have role guards; unauthenticated routes redirect to login.

**Observed screens:** Live home, /courses and /how-it-works rendered at a 319×791 in-app viewport. The catalog resolved from “Finding courses for you…” to 24 backend-backed cards; 24 course images loaded. Search and category controls were present. No console errors occurred during inspection. Screens were visually inspected but no image file was saved in the repository.

**Responsive layout:** Media queries exist in frontend/src/styles.scss. At 319 px, text and cards were readable, but a horizontal scrollbar remained. The largest offscreen boxes were decorative orbit/CTA shapes. Browser viewport control was unavailable, so exact 360, 768, 1024 and 1440 px checks could not be set. Desktop responsiveness and overflow are unverified.

**Structure and state:** Angular standalone pages and lazy feature routes are used. ApiService centralizes HTTP calls; AuthService uses signals and sessionStorage for the access token. The interceptor attaches bearer token and credentials; guards enforce UI roles and redirect unauthenticated users. TypeScript strict mode, strict Angular templates and reactive forms are enabled. No separate NgRx-style store exists; the service/signal approach fits the current app size.

**UX/accessibility:** Public pages have route titles; search/navigation controls have accessible labels. Loading/error/empty states exist in catalog and learner/teacher views. A full keyboard, screen-reader or form-by-form audit was not run. Lighthouse was unavailable in the active browser environment; no score is claimed.

**Performance evidence:** Production build initial bundle is 392.31 kB raw and 99.55 kB estimated transfer. Teacher, admin, student, account, auth and course-detail chunks are lazy loaded. No Lighthouse, waterfall, database profiling or large-catalog load test was performed.

### 7.8 Security Review

| Severity | Issue | Location | Impact | Recommendation |
|---|---|---|---|---|
| High before deployment | Data Protection key ring is not persisted/shared; docs/database.md says it must be persisted before deployment. | Program.cs and docs/database.md | Restart/key loss invalidates outstanding email-change and Identity-protected tokens. | Persist shared key ring in a restricted durable volume; protect at rest and test restart/restore. |
| High before deployment | Local Compose sets Development and enables Swagger. Development refresh cookies are not Secure. | compose.yaml, AuthController, Program.cs | Exposing this configuration publicly can expose development diagnostics/docs and non-Secure cookies. | Use Production, HTTPS/TLS, Secure cookies, trusted proxy settings and disable public Swagger. |
| Medium | Course data lacks price while the brief includes financial reporting. | Course.cs and analytics service | Required finance features cannot be derived from current course data. | Add approved pricing/finance model or formally revise scope. |
| Low | Angular access JWT is readable by JavaScript in sessionStorage. | frontend/src/app/core/auth.service.ts | XSS could steal an active access token. Refresh token is HttpOnly, reducing persistence but not eliminating access-token risk. | Keep template sanitization, add production CSP and consider BFF/HttpOnly access-token design if needed. |
| Low | No malware scanning was evidenced for uploaded student files. | StudentAssignmentsController and media storage | Extension/MIME/signature/size checks reduce spoofing but do not detect malicious documents. | Consider async malware scanning/quarantine before teacher download in a public deployment. |

**Positive controls:** Identity PasswordHash; password policy and 5-attempt/15-minute lockout; JWT issuer/audience/signature/lifetime/algorithm validation; Admin/Teacher/Student policies; single-use random account-action tokens stored as SHA-256 hashes; hashed/rotating refresh tokens in HttpOnly/SameSite cookie; auth/profile rate limiting; CORS allow-list; request logs avoid query strings; global Problem Details handler; upload size/type/header checks; server-generated media IDs; private Cloudinary delivery and short-lived signed download URLs. No secret values were printed. .env is ignored and not tracked by Git.

External Gmail and Cloudinary calls were not made. Deployment credentials, DNS/URLs, email delivery and cloud media remain unverified.

### 7.9 Code Quality & Documentation Review

The repository is split into Domain, Application, Infrastructure, Api, UnitTests and IntegrationTests. Controllers call application services; dependency injection, DTOs, FluentValidation, async EF calls, centralized exception handling and Serilog are used. Angular has typed models, strict compilation, guards, an interceptor and centralized API calls.

Test inventory: 21 .NET unit tests pass; 52 integration tests include 49 pass and 3 failures; 3 frontend tests pass. No coverage percentage is available. Integration tests use EF InMemory, so SQL transaction/constraint behavior is not covered by those workflows.

README.md, docs/architecture.md, docs/database.md, docs/endpoints.md and docs/checklist.md (which has since been removed from the repository) provided design/setup context at the time of this historical review. Accuracy gaps:
- README.md line 177 says maxScore defaults to 100, but backend and SQL constraints cap it at 10.0.
- README seed description says 9 published courses / 45 lessons / 18 assignments, while the current database has additional records. Clarify that these are initial demo-seed counts.

Git history has 18 commits from 2026-10-31 through 2026-11-04, by one author, with generally descriptive messages. Branch at review: feature/week1-foundation. Working tree was already heavily modified and had untracked project files before this audit; this report reviews that working tree. Last commit predates the review by three weeks, so history alone does not show current uncommitted work. Selection date was not supplied; timeline compliance is unverified.

No slides, presentation video or deployment assets were found; deploy/ contains only .gitkeep. A live presentation was not assessed.

### 7.10 Bonus Assessment

| Item | Points | Justification |
|---|---:|---|
| Extra functionality | 2 / 3 | Discussions, streaks/recommendations, email reminders, video/attachment workflows and admin analytics. |
| UI/UX | 2 / 3 | Coherent visual identity and public pages; mobile overflow prevents full credit. |
| Code quality | 1 / 2 | Strong layering/security patterns; failing tests and stale docs reduce confidence. |
| Innovation | 0 / 2 | No distinct innovation beyond implemented learning-platform features was separately evidenced. |
| **Total** | **5 / 10** | Separate bonus only. |

### 7.11 Defects & Missing Features List

**Blocker before public deployment**
1. Persist ASP.NET Core Data Protection keys and define Production HTTPS configuration. Current Compose is Development with Swagger enabled; deploy/ has no implementation.
2. Verify production mail and Cloudinary, DNS/URLs, secure cookie behavior, backups and restore. These were not tested and no production endpoint exists.

**Major**
1. Align supplied endpoint contract with implemented routes or add aliases. Exact course, lesson, assignment and user CRUD paths return 404/405 where brief requires them.
2. Add or formally remove Course duration/price/difficulty/prerequisites, general lesson attachments, finance reports, lecture calendar and consultation planning from scope.
3. Fix the three failing integration tests. Extract email URLs robustly; align invalid-score assertion with chosen 400/422 contract; rerun full suite.
4. Deduplicate current catalog: 9 course titles have repeated rows; duplicates are visible in the 24-card UI.

**Minor**
1. Fix mobile horizontal scrollbar caused by decorative shapes at observed 319 px; verify 360/768/1024/1440 in a viewport-capable browser.
2. Change README maxScore from 100 to 10.0 and distinguish seed counts from live DB counts.
3. Add demo guide and presentation materials; do not commit real passwords.
4. Consider pagination/server-side filtering before catalog grows beyond 24 cards.

### 7.12 Recommendations

1. Agree whether the supplied API rubric or implemented route structure is authoritative. If the rubric is binding, add route aliases and contract tests for every method/path.
2. Decide whether duration, price, difficulty, prerequisites, generic lesson attachments and finance reports remain required. If yes, implement end-to-end with migrations, validators, API DTOs and UI.
3. Repair the three failing integration tests; require dotnet test to pass before merging.
4. Deduplicate demo catalog and test seed idempotency by stable course key.
5. Fix and measure responsive overflow at 360, 768, 1024 and 1440 px; run Lighthouse or equivalent.
6. Add production configuration after key-ring persistence, HTTPS, Production settings, Swagger policy, secrets, SQL backup/restore and real email/media checks.
7. Update README/checklist with maxScore 10.0, actual route map, seed/live counts, demo workflow and deployment limitations.
8. Commit reviewed changes in focused commits; current tree has extensive uncommitted changes.

**Suggested oral defense questions**
1. Why are course creation and grading under /api/teacher and /api/submissions rather than supplied /api/courses and /api/assignments routes?
2. How does Identity hash/verify passwords, and how are reset tokens stored and consumed?
3. Why is refresh token in an HttpOnly cookie while access token is in sessionStorage?
4. How does the score migration preserve percentage when moving from 100-point to 10-point grading?
5. Where is ownership enforced, and which test proves a different teacher cannot edit a course?
6. How do upload extension, MIME, magic-byte and size checks work; what does Cloudinary signing protect?
7. Difference between JWT signing keys and ASP.NET Data Protection keys?
8. How are recommendations ranked and already-enrolled courses excluded?
9. Which tests use EF InMemory, and what SQL behavior still needs a real database test?
10. What must change before exposing Swagger/API publicly?

### 7.13 Appendix

### A. Commands and key output

- dotnet build --nologo — Build succeeded; 0 warnings, 0 errors.
- dotnet test --nologo — 21 unit passed; 49 integration passed; 3 integration failed; 73 .NET tests total.
- npm run build — Angular production build succeeded.
- npm test — 2 files, 3 tests passed.
- npm ci in clean temporary copy — 468 packages; 0 vulnerabilities reported.
- docker compose run --rm migrate — completed.
- Fresh DB — 14 migrations; latest 20261124205717_CapGradingAtTen; 19 application tables plus migration history.
- dotnet ef migrations has-pending-model-changes — no model changes.
- SQL schema — 20 tables including history; 25 FKs; 57 indexes; 2 score constraints.
- Health live/ready — 200; Swagger JSON — 200; unknown route — 404 application/problem+json.
- Database counts — 9 users, 24 courses, 94 lessons, 37 assignments, 1 submission; no personal account data included.
- Grading limits — all stored max scores and grades at most 10.00.
- Git status was dirty before verification. This report is the only new project deliverable created by the audit; project source files were not edited.

### B. Full OpenAPI operation list and unauthenticated probe status

All 76 operations below were called without credentials and without request bodies. Auth routes are rate-limited; 415 means the empty request had no JSON body/content type, 429 reflects burst rate limiting. These are route/access probes, not success tests.

| Method | Path | HTTP |
|---|---|---:|
| GET | /api/admin/analytics | 401 |
| GET | /api/admin/assignments/{assignmentId}/submissions | 401 |
| GET | /api/admin/categories | 401 |
| POST | /api/admin/categories | 401 |
| PUT | /api/admin/categories/{id} | 401 |
| PUT | /api/admin/categories/{id}/status | 401 |
| GET | /api/admin/courses | 401 |
| GET | /api/admin/courses/{id} | 401 |
| PUT | /api/admin/courses/{id} | 401 |
| POST | /api/admin/courses/{id}/approve | 401 |
| GET | /api/admin/courses/{id}/assignments | 401 |
| GET | /api/admin/courses/{id}/discussions | 401 |
| GET | /api/admin/courses/{id}/lessons | 401 |
| POST | /api/admin/courses/{id}/reject | 401 |
| GET | /api/admin/courses/pending | 401 |
| PUT | /api/admin/discussions/{postId}/moderation | 401 |
| GET | /api/admin/users | 401 |
| DELETE | /api/admin/users/{id} | 401 |
| PUT | /api/admin/users/{id}/role | 401 |
| PUT | /api/admin/users/{id}/status | 401 |
| POST | /api/assignments/{assignmentId}/submissions | 401 |
| GET | /api/assignments/{assignmentId}/submissions/me | 401 |
| POST | /api/assignments/{assignmentId}/submissions/with-attachment | 401 |
| POST | /api/auth/confirm-email-change | 415 |
| POST | /api/auth/forgot-password | 429 |
| POST | /api/auth/login | 415 |
| POST | /api/auth/logout | 429 |
| POST | /api/auth/logout-all | 429 |
| POST | /api/auth/refresh | 429 |
| POST | /api/auth/register | 415 |
| POST | /api/auth/resend-verification | 415 |
| POST | /api/auth/reset-password | 429 |
| POST | /api/auth/verify-email | 415 |
| GET | /api/categories | 200 |
| GET | /api/courses | 200 |
| GET | /api/courses/{courseId}/assignments | 401 |
| GET | /api/courses/{courseId}/discussions | 401 |
| POST | /api/courses/{courseId}/discussions | 401 |
| POST | /api/courses/{courseId}/enrollments | 401 |
| GET | /api/courses/{courseId}/lessons | 401 |
| GET | /api/courses/{courseId}/progress/me | 401 |
| GET | /api/courses/{courseId}/submissions/me | 401 |
| GET | /api/courses/{id} | 404 for random id |
| POST | /api/discussions/{postId}/replies | 401 |
| DELETE | /api/lessons/{lessonId}/progress | 401 |
| PUT | /api/lessons/{lessonId}/progress | 401 |
| GET | /api/submissions/{submissionId}/attachment | 401 |
| PUT | /api/submissions/{submissionId}/grade | 401 |
| PUT | /api/teacher/assignments/{assignmentId} | 401 |
| GET | /api/teacher/assignments/{assignmentId}/submissions | 401 |
| GET | /api/teacher/courses | 401 |
| POST | /api/teacher/courses | 401 |
| GET | /api/teacher/courses/{courseId}/assignments | 401 |
| POST | /api/teacher/courses/{courseId}/assignments | 401 |
| GET | /api/teacher/courses/{courseId}/discussions | 401 |
| GET | /api/teacher/courses/{courseId}/lessons | 401 |
| POST | /api/teacher/courses/{courseId}/lessons | 401 |
| GET | /api/teacher/courses/{courseId}/submissions | 401 |
| PUT | /api/teacher/courses/{id} | 401 |
| POST | /api/teacher/courses/{id}/archive | 401 |
| POST | /api/teacher/courses/{id}/submit | 401 |
| PUT | /api/teacher/discussions/{postId}/moderation | 401 |
| DELETE | /api/teacher/lessons/{lessonId} | 401 |
| PUT | /api/teacher/lessons/{lessonId} | 401 |
| DELETE | /api/teacher/lessons/{lessonId}/video | 401 |
| PUT | /api/teacher/lessons/{lessonId}/video | 401 |
| GET | /api/users/me | 429 |
| POST | /api/users/me/email-change | 429 |
| GET | /api/users/me/enrollments | 401 |
| POST | /api/users/me/password | 429 |
| PUT | /api/users/me/profile | 429 |
| DELETE | /api/users/me/profile-picture | 429 |
| PUT | /api/users/me/profile-picture | 429 |
| GET | /api/users/me/progress | 401 |
| GET | /api/users/me/recommendations | 401 |
| GET | /api/users/me/streak | 401 |

### C. Dependency versions reviewed

- .NET SDK: 10.0.401.
- EF Core and ASP.NET Identity/JWT packages: 10.0.14.
- Swashbuckle.AspNetCore: 10.3.1.
- MailKit: 4.18.2.
- Serilog.AspNetCore: 10.0.0.
- Angular Core: 21.2.25; Angular Build: 21.2.29.
- TypeScript 5.9.x; Vitest 4.1.12.
- SQL Server container: mcr.microsoft.com/mssql/server:2025-latest.

### D. Review boundaries

Working tree was modified and contained untracked code before the audit. The review did not reset, commit or rewrite those changes. It did not send real email, upload/delete Cloudinary assets, change user accounts, or leave the temporary verification database in SQL Server. Authenticated live-role workflows and external-service behavior remain unverified. A PDF/DOCX export was not produced; this Markdown file is the report.

