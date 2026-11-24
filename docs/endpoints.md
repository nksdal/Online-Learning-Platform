# API endpoint inventory

This document distinguishes endpoints that are implemented now from routes planned for later phases. A planned route is not available in the running API until its controller or endpoint handler is implemented.

## Implemented foundation endpoints

| Method | Path | Access | Behavior |
|---|---|---|---|
| `GET` | `/health/live` | Anonymous | Liveness probe. Runs no dependency checks and returns `200 Healthy` while the API process can serve requests. |
| `GET` | `/health/ready` | Anonymous | Readiness probe. Checks SQL Server and returns `200 Healthy` when ready or `503 Unhealthy` when the database is unreachable. |
| `GET` | `/swagger` | Development/configuration enabled | Swagger UI. The document is available at `/swagger/v1/swagger.json` and includes the currently implemented API routes. |
| `GET` | `/swagger/v1/swagger.json` | Development/configuration enabled | OpenAPI document for the current API. |

## Implemented authentication endpoints

| Method | Path | Access | Behavior |
|---|---|---|---|
| `POST` | `/api/auth/register` | Anonymous, rate limited | Creates a Student account and sends an email confirmation message. Returns a generic accepted response. |
| `POST` | `/api/auth/verify-email` | Anonymous, rate limited | Confirms the email using the Identity user ID and confirmation token. |
| `POST` | `/api/auth/resend-verification` | Anonymous, rate limited | Sends another confirmation message when appropriate; response does not disclose account existence. |
| `POST` | `/api/auth/forgot-password` | Anonymous, rate limited | Sends a password reset link to a confirmed, active account. Returns the same accepted response for unknown addresses. |
| `POST` | `/api/auth/reset-password` | Anonymous, rate limited | Resets the password with the Identity token and revokes the user's refresh sessions. Invalid or expired reset details return a generic `400` problem response. |
| `POST` | `/api/auth/login` | Anonymous, rate limited | Returns a short-lived JWT for an active account with a confirmed email and valid password; sets a 30-day refresh token in an HttpOnly cookie. Invalid credentials and unconfirmed or inactive accounts receive the same generic `401` response. |
| `POST` | `/api/auth/refresh` | Anonymous, rate limited; refresh cookie and `X-Refresh-Request: 1` required | Rotates the refresh token and returns a new short-lived JWT. Reuse of a revoked token revokes its entire token family. |
| `POST` | `/api/auth/logout` | Anonymous, rate limited; refresh cookie and `X-Refresh-Request: 1` required | Revokes the current refresh-token family and clears the cookie. |
| `POST` | `/api/auth/logout-all` | Bearer JWT and `X-Refresh-Request: 1` required | Revokes all refresh-token sessions belonging to the authenticated user and clears the cookie. |
| `GET` | `/api/users/me` | Bearer JWT | Returns the current user's profile and role. |
| `POST` | `/api/users/me/password` | Bearer JWT, rate limited | Changes the current user's password, clears the forced-change flag, revokes refresh sessions, and returns a replacement access token. |
| `GET` | `/api/admin/users` | Admin bearer JWT | Returns a paginated user list; supports optional `search`, `page`, and `pageSize` query parameters. |
| `PUT` | `/api/admin/users/{id}/role` | Admin bearer JWT | Assigns exactly one role (`Student`, `Teacher`, or `Admin`); prevents demoting the last active admin. |
| `PUT` | `/api/admin/users/{id}/status` | Admin bearer JWT | Activates or deactivates an account; prevents deactivating the last active admin. |
| `GET` | `/api/categories` | Anonymous | Lists active course categories. |
| `GET` | `/api/admin/categories` | Admin bearer JWT | Lists all categories, including inactive categories. |
| `POST` | `/api/admin/categories` | Admin bearer JWT | Creates a category; names are unique case-insensitively. |
| `PUT` | `/api/admin/categories/{id}` | Admin bearer JWT | Updates a category's name and description. |
| `PUT` | `/api/admin/categories/{id}/status` | Admin bearer JWT | Activates or deactivates a category without deleting it. |
| `GET` | `/api/courses` | Anonymous | Lists published courses; optional `categoryId` filters by category. |
| `GET` | `/api/courses/{id}` | Anonymous | Gets a published course; drafts, pending, rejected, archived, and missing courses return 404. |
| `GET` | `/api/teacher/courses` | Teacher bearer JWT | Lists the authenticated teacher's courses in every workflow state. |
| `POST` | `/api/teacher/courses` | Teacher bearer JWT | Creates a Draft course in an active category. |
| `PUT` | `/api/teacher/courses/{id}` | Owning teacher bearer JWT | Edits own Draft, Rejected, or Published course. A pending or archived course cannot be edited. |
| `POST` | `/api/teacher/courses/{id}/submit` | Owning teacher bearer JWT | Submits a Draft or resubmits a Rejected course for admin review. |
| `POST` | `/api/teacher/courses/{id}/archive` | Owning teacher bearer JWT | Archives a Published course. Archive is terminal. |
| `GET` | `/api/admin/courses` | Admin bearer JWT | Lists all courses in every workflow state. |
| `GET` | `/api/admin/courses/{id}` | Admin bearer JWT | Gets any course, including drafts and archived courses. |
| `GET` | `/api/admin/courses/pending` | Admin bearer JWT | Lists courses awaiting review. |
| `POST` | `/api/admin/courses/{id}/approve` | Admin bearer JWT | Approves a pending course and publishes it. |
| `POST` | `/api/admin/courses/{id}/reject` | Admin bearer JWT | Rejects a pending course with a reason. |
| `POST` | `/api/courses/{courseId}/enrollments` | Student bearer JWT | Enrolls in a published course once. Duplicate enrollment returns 409. |
| `GET` | `/api/users/me/enrollments` | Student bearer JWT | Lists the student's enrollments, including archived courses. |
| `GET` | `/api/courses/{courseId}/lessons` | Enrolled Student bearer JWT | Lists lessons for a Published or Archived course when the student has an enrollment; otherwise returns 404. |
| `GET` | `/api/teacher/courses/{courseId}/lessons` | Owning Teacher bearer JWT | Lists lessons for the teacher's course. |
| `POST` | `/api/teacher/courses/{courseId}/lessons` | Owning Teacher bearer JWT | Adds an ordered lesson to a course that is not pending review or archived. |
| `PUT` | `/api/teacher/lessons/{lessonId}` | Owning Teacher bearer JWT | Updates lesson title, content, or position. |
| `PUT` | `/api/teacher/lessons/{lessonId}/video` | Owning Teacher bearer JWT | Uploads or replaces an MP4/WebM lesson video (up to 100 MB) to Cloudinary. |
| `DELETE` | `/api/teacher/lessons/{lessonId}/video` | Owning Teacher bearer JWT | Removes a lesson video and deletes its Cloudinary asset. |
| `DELETE` | `/api/teacher/lessons/{lessonId}` | Owning Teacher bearer JWT | Deletes a lesson from an editable course. |
| `GET` | `/api/admin/courses/{courseId}/lessons` | Admin bearer JWT | Lists lessons for any course, including archived courses. |
| `GET` | `/api/courses/{courseId}/progress/me` | Enrolled Student bearer JWT | Returns lesson completion states and overall course completion percentage. Archived courses return the saved progress read-only. |
| `PUT` | `/api/lessons/{lessonId}/progress` | Enrolled Student bearer JWT | Marks a lesson complete. Repeating the request is safe and does not create duplicate progress. |
| `DELETE` | `/api/lessons/{lessonId}/progress` | Enrolled Student bearer JWT | Resets a lesson to incomplete. Returns 204 when successful or already incomplete. Progress cannot change after archive. |
| `GET` | `/api/courses/{courseId}/assignments` | Enrolled Student bearer JWT | Lists assignments for a Published or Archived course. |
| `GET` | `/api/teacher/courses/{courseId}/assignments` | Owning Teacher bearer JWT | Lists assignments for the teacher's course. |
| `POST` | `/api/teacher/courses/{courseId}/assignments` | Owning Teacher bearer JWT | Creates a text assignment with optional UTC deadline, maximum attempts (default 1), and maximum score (default 10.0). Maximum score is capped at 10.0. |
| `PUT` | `/api/teacher/assignments/{assignmentId}` | Owning Teacher bearer JWT | Updates assignment instructions, deadline, attempt limit, and a maximum score up to 10.0. |
| `POST` | `/api/assignments/{assignmentId}/submissions` | Enrolled Student bearer JWT | Adds a text submission attempt while the course is Published and before the deadline. Returns 409 when all allowed attempts are used. |
| `POST` | `/api/assignments/{assignmentId}/submissions/with-attachment` | Enrolled Student bearer JWT | Multipart submit with optional response text and one private Cloudinary attachment (PDF, DOCX, XLSX, PPTX, TXT, CSV, PNG, JPG, or WebP; up to 10 MB). |
| `GET` | `/api/submissions/{submissionId}/attachment` | Owning Student, course Teacher, or Admin bearer JWT | Streams the private attachment through an authorized API route using an expiring Cloudinary download URL. |
| `GET` | `/api/assignments/{assignmentId}/submissions/me` | Enrolled Student bearer JWT | Lists the current student's attempt history and any grades/feedback. |
| `GET` | `/api/teacher/assignments/{assignmentId}/submissions` | Owning Teacher bearer JWT | Lists every student's attempt history for that assignment. |
| `PUT` | `/api/submissions/{submissionId}/grade` | Owning Teacher bearer JWT | Grades a submission with a score from zero through the assignment's maximum and optional feedback. |
| `GET` | `/api/admin/courses/{courseId}/assignments` | Admin bearer JWT | Lists assignments for any course. |
| `GET` | `/api/admin/assignments/{assignmentId}/submissions` | Admin bearer JWT | Reads all submissions for an assignment. |
| `GET` | `/api/admin/analytics?days=30` | Admin bearer JWT | Returns platform metrics, a 12-month activity trend, and course engagement. Supports reporting windows of 7, 30, 90, or 365 days. |
| `GET` | `/api/courses/{courseId}/discussions` | Enrolled Student bearer JWT | Lists visible discussion posts and replies for a Published or Archived course. |
| `POST` | `/api/courses/{courseId}/discussions` | Enrolled Student bearer JWT | Creates a discussion post in a Published course. |
| `POST` | `/api/discussions/{postId}/replies` | Enrolled Student bearer JWT | Replies to a visible post in a Published course. |
| `GET` | `/api/teacher/courses/{courseId}/discussions` | Owning Teacher bearer JWT | Lists visible and hidden posts for the teacher's course. |
| `PUT` | `/api/teacher/discussions/{postId}/moderation` | Owning Teacher bearer JWT | Hides or restores a post; hiding requires a reason. |
| `GET` | `/api/admin/courses/{courseId}/discussions` | Admin bearer JWT | Lists all posts, including hidden posts. |
| `PUT` | `/api/admin/discussions/{postId}/moderation` | Admin bearer JWT | Hides or restores any post; hiding requires a reason. |
| `GET` | `/api/users/me/streak` | Student bearer JWT | Returns current and longest learning streaks using the student's time zone and lesson completions. |
| `GET` | `/api/users/me/recommendations?limit=10` | Student bearer JWT | Recommends published courses, preferring the student's enrolled categories, then overall enrollment popularity; limit is clamped to 1–20. |

## Compatibility routes from the project brief

These routes keep the original brief's resource-shaped paths available while preserving the explicit role routes above. They delegate to the same application services and authorization rules.

| Method | Path | Access | Behavior |
|---|---|---|---|
| `POST` | `/api/courses` | Teacher bearer JWT | Creates a draft course. |
| `PUT` | `/api/courses/{id}` | Owning Teacher bearer JWT | Updates an editable course. |
| `DELETE` | `/api/courses/{id}` | Owning Teacher bearer JWT | Archives a published course and keeps enrolled learners' read access; it does not erase learning history. |
| `POST` | `/api/courses/{courseId}/lessons` | Owning Teacher bearer JWT | Adds a lesson. |
| `PUT` | `/api/courses/{courseId}/lessons/{id}` | Owning Teacher bearer JWT | Updates a lesson only when it belongs to the course in the route. |
| `DELETE` | `/api/courses/{courseId}/lessons/{id}` | Owning Teacher bearer JWT | Deletes a lesson only when it belongs to the course in the route. |
| `POST` | `/api/courses/{courseId}/assignments` | Owning Teacher bearer JWT | Creates an assignment. |
| `PUT` | `/api/courses/{courseId}/assignments/{id}` | Owning Teacher bearer JWT | Updates an assignment only when it belongs to the course in the route. |
| `POST` | `/api/assignments/{id}/submit` | Enrolled Student bearer JWT | Creates a text submission attempt. |
| `PUT` | `/api/assignments/{id}/grade` | Owning Teacher bearer JWT | Grades a submission; the path's `id` is the submission ID, matching the app's per-attempt grading model. |
| `GET` | `/api/users` | Admin bearer JWT | Lists users with the existing pagination and search options. |
| `GET` | `/api/users/{id}` | Admin bearer JWT | Reads a user's profile. |
| `PUT` | `/api/users/{id}` | Admin bearer JWT | Updates the user's first name, last name, and time zone. |
| `DELETE` | `/api/users/{id}` | Admin bearer JWT | Anonymizes the account and retains linked courses and learning history. |

`GET /api/courses` and `GET /api/courses/{id}` were already implemented as public catalogue routes. Student lesson and assignment reads already use the nested course paths from the brief. The added aliases do not grant users permissions they do not already have through the role-specific routes.

Unknown paths return `404` using `application/problem+json`. The health routes are anonymous so Docker or an orchestrator can probe the service without credentials.

## Planned route groups

These are project requirements, not implemented endpoints yet. Their detailed contracts should be added as each feature is designed and built.

| Area | Planned routes |
|---|---|
| Lessons and materials | `/api/lessons/{lessonId}/materials`, `/api/materials/{materialId}` |
| Enrollment and progress | Streak history and daily learning summaries (future extension) |
| Reviews and administration | `/api/courses/{courseId}/reviews` |

Before implementing a planned route, define its request/response DTOs, validation, authorization rules, status codes, and error responses. Update this inventory when the route becomes available.
