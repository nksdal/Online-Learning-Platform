# Nikladori presentation and demo walkthrough

This is a 7–9 minute presentation plan and a role-by-role demo script. The verification notes distinguish automated API evidence from a live browser walkthrough; do not describe a role's browser journey as manually verified until it has been run with that account.

## Before presenting

- Start the local stack from the repository root with `docker compose up -d --build`; wait for the API and SQL Server to become healthy.
- Start the Angular app in a second PowerShell window with `cd frontend; npm start`, then open `http://localhost:4200`.
- Use a clean demo database or test accounts. Prepare one confirmed Student, one Teacher, and one Admin account. Never put passwords, JWTs, email tokens, or provider secrets in slides or screenshots.
- Confirm the Teacher owns an editable course with at least two lessons and an assignment. Confirm the Student is enrolled in a published course. Have one pending course ready for Admin review.
- If using a seeded Admin, finish the forced password change before the demo. If registration verification is part of the talk, use an inbox you can access and allow for email delivery delay.
- Keep the API docs at `http://localhost:5220/swagger` available as a technical backup. Hide browser developer tools and account email addresses in presentation screenshots.

## Slide outline and speaker notes

### 1. Nikladori: learning in clear steps (45 seconds)

Show the landing page. Explain that the platform connects course discovery, teacher-created lessons, assignments, feedback, and learner progress in one place. The intended audience is pupils, so the experience uses short, direct actions and a calm visual style.

### 2. Three roles, one learning path (45 seconds)

Introduce the three roles: Students learn and submit work; Teachers create content and assess submissions; Admins review courses and manage the platform. Roles have separate permissions enforced by the API as well as the interface.

### 3. Course catalogue and course details (60 seconds)

Browse or search the course catalogue, filter by subject, and open a course. Point out duration, difficulty, prerequisites, and the listed GEL price. Prices are catalogue information only; checkout and payment processing are not implemented.

### 4. Teacher studio (90 seconds)

Open Teacher Studio. Show a course draft, its lessons, and an assignment. Explain the review cycle: save a draft, submit it, then wait for Admin approval. Show that grading is capped at the assignment's configured maximum, up to 10.0 points.

### 5. Admin workspace (75 seconds)

Open the pending course queue. Approve or reject the prepared pending course. Show user/course management and the analytics dashboard; use the selected date window and explain that the dashboard reports platform activity and learning metrics.

### 6. Student learning space (90 seconds)

Open My Learning, enroll in a published course if needed, open a lesson, and mark it complete. Show an assignment submission and a returned grade/feedback. If the prepared lesson contains a video, show playback. If the submission has a sample attachment, show the authorized download.

### 7. Architecture, safeguards, and next work (45 seconds)

Summarize the Angular frontend, ASP.NET Core API, SQL Server persistence, role authorization, and automated tests. State clearly that local Compose is a development setup; public deployment still needs the production hardening and operational checks listed in the project checklist.

## Role-by-role demo walkthrough

Follow this order so each action creates something the next role can show. Use separate browser profiles or sign out before changing accounts. Do not reuse a Student token to demonstrate Teacher or Admin access.

| Role | Steps to perform | Expected result | Automated API evidence |
|---|---|---|---|
| Visitor | Open Home, Courses, search/filter a course, open its detail page. | Public pages load at phone and desktop widths; only published courses appear; the detail page shows duration, price, difficulty, and prerequisites. | Public route and catalogue checks in the integration suite; mobile viewport measurements recorded in `docs/project-readiness.md`. |
| Teacher | Open Teacher Studio; create or edit a course; add a lesson and an assignment; submit the course for review. | The teacher sees only their own courses; content saves; the course enters Pending Approval. | `CourseTests.Teacher_and_admin_can_complete_course_review_lifecycle`; nested lesson and assignment route coverage in `LearningTests` and `AssignmentTests`. |
| Admin | Open Admin Workspace; review the pending course; approve it; optionally view user management and analytics. | The course becomes Published and appears in the public catalogue; admin-only screens reject other roles. | `CourseTests.Teacher_and_admin_can_complete_course_review_lifecycle`, `CategoryTests.Admin_can_create_update_deactivate_and_list_categories`, and `AdminUserManagementTests.Admin_can_view_own_profile_and_page_through_user_list`. |
| Student | Browse and enroll in the approved course; open lessons; complete one; submit an assignment. | The enrollment appears in My Learning; lesson progress updates; the attempt is visible to the teacher. | `LearningTests.Student_can_complete_reset_and_view_course_progress`, `LearningTests.Enrolled_student_keeps_lesson_access_after_course_is_archived`, and `AssignmentTests.Student_attempts_are_preserved_and_teacher_can_grade_them`. |
| Teacher | Reopen Grading and grade the student's submission with a score within its maximum. | The saved score and feedback appear on the student's attempt; a score over the configured maximum is rejected. | `AssignmentTests.Student_attempts_are_preserved_and_teacher_can_grade_them`. |

### Verification record

The listed backend actions and authorization paths are covered by automated integration tests. That proves the API workflows against the test host; it is not a claim that every authenticated browser screen has been manually clicked through. The current repository verification used a real local browser for responsive layout and public routes. Run the account-based role steps above once before a live presentation and record any issues in `docs/project-readiness.md`.

## Presenter checklist

- [ ] Home, Courses, and How it works look correct on the projector and on a narrow phone viewport.
- [ ] API, SQL Server, and frontend are healthy; `/health/ready` returns 200.
- [ ] Admin can approve the prepared course.
- [ ] Teacher can open the prepared lessons/assignment and grading screen.
- [ ] Student can open the enrolled course, update progress, and see the grade.
- [ ] Course photos and lesson video are available on the demo network.
- [ ] Screenshots contain no secrets, email tokens, or personal learner data.
- [ ] A copy of this walkthrough and the project readiness note is available offline.
