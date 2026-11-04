# Online Learning Platform frontend

Angular 21 student-facing foundation for the Online Learning Platform API.

## Run locally

Start the API and SQL Server from the repository root. Configure Gmail SMTP in `.env` first; see the repository README for setup:

```powershell
docker compose up --build --detach
```

Then start the Angular dev server:

```powershell
cd frontend
npm start
```

Open `http://localhost:4200`. The API URL is configured in `src/environments/environment.ts` and defaults to `http://localhost:5220`.

## Current screens

- Course catalogue with category filters and published course details.
- Student registration, sign-in, and enrollment.
- Email confirmation page for links sent to the user's real inbox.
- Forgot-password and reset-password screens.
- “My Learning” dashboard with enrollment progress, streaks, and recommendations.
- Enrolled course lesson reading with completion and progress reset.
- Assignment instructions, text submissions, attempt history, grades, and instructor feedback.
- Course discussions with posts and replies.

Registration and password recovery emails are delivered through the configured SMTP provider to real inboxes. The default local links point to `localhost:4200`, so they work on the computer running the frontend. Use the deployed frontend URL in the email settings for public users.

## Build

```powershell
npm run build
```
