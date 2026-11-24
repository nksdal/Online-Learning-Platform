# Security review and deployment requirements

This review covers the repository configuration and application code. It is not an external penetration test. Re-run dependency and test checks after changing authentication, upload handling, deployment settings, or packages.

## Controls verified or added

- Password creation and changes use ASP.NET Core Identity's password hasher and validators. Login lockout is enabled after five failed attempts for 15 minutes.
- JWTs validate issuer, audience, lifetime, signing key, and the HMAC-SHA256 algorithm. Access tokens last no more than 15 minutes. Refresh tokens are stored as hashes and use an HttpOnly, SameSite=Strict cookie.
- Admin, teacher, and student controllers use role policies. Teacher services also verify course ownership; student services scope learning data to the authenticated student. Public catalog and account-recovery routes are intentional.
- Authentication and account routes use a five-request-per-minute IP limit. Trusted forwarded IP addresses are accepted only from explicitly configured proxies.
- Request DTO validators apply server-side length and format rules. Angular interpolation escapes displayed content, and the frontend source has no `innerHTML`, `bypassSecurityTrust`, `document.write`, or `eval` sinks.
- Upload endpoints apply size, extension, MIME, and file-signature checks. Uploaded files are stored outside the application web root.
- CORS allows exact configured origins and credentials; production validation requires HTTPS origins. API responses receive `nosniff`, frame, referrer, and permissions headers. Production enables HSTS and HTTPS redirection.
- `.env` and `.env.*` are ignored, `.env.example` contains placeholders, and the Docker build context excludes environment files. Production settings keep Swagger and demo seeding off.
- Local Compose now provisions a separate SQL login for the API with data-reader/data-writer access. The `sa` credential is used only by the local migration/provisioning services. Production startup rejects `sa`, unencrypted database connections, and unvalidated database certificates.
- Local Compose persists ASP.NET Data Protection keys in a restricted Docker volume so container restarts do not invalidate protected data. Production must use a persistent key store protected by the host's key-management service.
- The frontend dependency lockfile was updated within the declared Angular 21 ranges. NuGet and npm vulnerability audits reported no known vulnerable packages at the time of this review.

## Before deployment

`appsettings.Production.json` is a safe template, not a ready-to-run configuration. Replace its example hostnames and origins with the real API and frontend hosts. Set the JWT signing key, database connection, email credentials, and optional Cloudinary credentials through the hosting provider's secret store. Do not deploy with the local `.env` file or the Development Compose stack.

The production API database connection must use a dedicated database principal with only the permissions needed by the app, set `Encrypt=True`, and set `TrustServerCertificate=False`. Run schema migrations through a separate deployment identity with migration privileges; never give that identity to the running API.

If TLS terminates at a reverse proxy, configure its exact IP under `ForwardedHeaders:KnownProxies` (or `ForwardedHeaders__KnownProxies__0`) and set the hosting platform's HTTPS port. Do not trust forwarded headers from arbitrary clients. Configure the production CORS origins and `AllowedHosts` to exact real hosts.

Use a persistent, access-restricted Data Protection key store across production instances, protected by the hosting platform's key-management service. Configure malware scanning and retention rules for user uploads before accepting public submissions. A real public sender also requires a verified sending domain and provider credentials; Gmail SMTP is a development stopgap.

## Package note

No unused direct dependency was confirmed, so package references required by the API, frontend, tests, or coverage collection were retained. The current package audit reported no known vulnerabilities. Packages whose only reported updates were a new major version were not upgraded across the .NET 10 target or package major boundary.
