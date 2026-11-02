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
| `DELETE` | `/api/teacher/lessons/{lessonId}` | Owning Teacher bearer JWT | Deletes a lesson from an editable course. |
| `GET` | `/api/admin/courses/{courseId}/lessons` | Admin bearer JWT | Lists lessons for any course, including archived courses. |
| `GET` | `/api/courses/{courseId}/progress/me` | Enrolled Student bearer JWT | Returns lesson completion states and overall course completion percentage. Archived courses return the saved progress read-only. |
| `PUT` | `/api/lessons/{lessonId}/progress` | Enrolled Student bearer JWT | Marks a lesson complete. Repeating the request is safe and does not create duplicate progress. |
| `DELETE` | `/api/lessons/{lessonId}/progress` | Enrolled Student bearer JWT | Resets a lesson to incomplete. Returns 204 when successful or already incomplete. Progress cannot change after archive. |

Unknown paths return `404` using `application/problem+json`. The health routes are anonymous so Docker or an orchestrator can probe the service without credentials.

## Planned route groups

These are project requirements, not implemented endpoints yet. Their detailed contracts should be added as each feature is designed and built.

| Area | Planned routes |
|---|---|
| Lessons and materials | `/api/lessons/{lessonId}/materials`, `/api/materials/{materialId}` |
| Enrollment and progress | Streak history and daily learning summaries (future extension) |
| Assignments and grading | `/api/courses/{courseId}/assignments`, `/api/assignments/{assignmentId}/submissions`, `/api/submissions/{submissionId}/grade` |
| Reviews and administration | `/api/courses/{courseId}/reviews`, `/api/admin/dashboard/statistics` |

Before implementing a planned route, define its request/response DTOs, validation, authorization rules, status codes, and error responses. Update this inventory when the route becomes available.
