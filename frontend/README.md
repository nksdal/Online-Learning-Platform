# Online Learning Platform frontend

Angular 21 student-facing foundation for the Online Learning Platform API.

## Run locally

Start the API, SQL Server, and Mailpit from the repository root:

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
- Email confirmation page for the link captured by local Mailpit.
- Forgot-password and reset-password screens.
- “My Learning” dashboard with enrollment progress, streaks, and recommendations.
- Enrolled course lesson reading with completion and progress reset.

Registration and password recovery emails are captured in Mailpit at `http://localhost:8025`; local development does not deliver them to real mailboxes.

## Build

```powershell
npm run build
```
