# DigitalOcean Droplet deployment (SQL Server Express)

This is a production-oriented starting point for a small deployment that keeps the existing SQL Server provider. It runs the Angular site and ASP.NET Core API behind Caddy HTTPS, with SQL Server Express on a private Docker network. The database port is not published on the Droplet.

SQL Server Express is free for production use, but SQL Server 2025 Express is limited to four cores, 1,410 MB of buffer-pool memory, and 50 GB per database. A DigitalOcean Droplet, storage, and backups still have a cost. The SQL Server Developer edition in the root `compose.yaml` remains for local development only. See [Microsoft's edition limits](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-editions-and-components-2025?view=sql-server-ver17) and [container licensing settings](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-configure-environment-variables).

## Requirements before starting

- An Ubuntu x86-64 Droplet with Docker Engine and Docker Compose v2 installed.
- A public DNS hostname for the app, such as `app.example.com`, pointed to the Droplet. Caddy obtains and renews the website's HTTPS certificate; allow inbound TCP ports 80 and 443 (UDP 443 is optional for HTTP/3).
- A public DNS hostname for SQL Server, such as `db.example.com`, also pointed to the Droplet, plus a CA-issued TLS certificate whose DNS SAN exactly matches that name. The SQL name is only a Docker network alias; port 1433 remains private. Mount its full chain and private key where `.env.production` points.
- Keep SSH limited to your own IP with a DigitalOcean Cloud Firewall. Do not create an inbound rule for port 1433.
- A Gmail App Password for a demo or a verified transactional email sender for a public launch. Configure Cloudinary if users will upload media.

The Droplet setup, DNS records, certificate issuance/renewal, and firewall are host-level steps. The Compose files do not create DigitalOcean resources or purchase a domain.

## Prepare the Droplet

Clone the merged project branch on the Droplet, install Docker Engine and the Compose plugin, and create the persistent directories:

```bash
sudo mkdir -p /srv/nikladori/sqlserver/certs /srv/nikladori/backups /etc/ssl/nikladori
```

Copy the SQL Server CA-issued certificate chain and private key to the paths configured in `.env.production`. Set their owner to the `mssql` account's numeric UID in the SQL Server image and restrict both files to that account. The server certificate needs the `db.example.com` SAN; keep the private key readable only by SQL Server and root. Arrange a renewal hook that copies renewed files with the same owner/mode and restarts the `sqlserver` service.

Create `.env.production` from the root template, replace every placeholder, then restrict it:

```bash
cp .env.production.example .env.production
chmod 600 .env.production
```

Set `SITE_HOST`, `FRONTEND_ORIGIN`, and `APP_URL` to the exact HTTPS site origin, for example `app.example.com` / `https://app.example.com`. Set `SQLSERVER_HOSTNAME` to the hostname on the SQL TLS certificate. Pick an unused private subnet for `PROXY_NETWORK_SUBNET`; `CADDY_PROXY_IP` must be a free address inside it. The API trusts forwarded headers only from that static Caddy IP.

Generate a unique JWT signing key with `openssl rand -hex 32`. Use separate strong passwords for `MSSQL_SA_PASSWORD`, `DB_APP_PASSWORD`, `DB_MIGRATOR_PASSWORD`, and the bootstrap admin. Use URL-safe random passwords without semicolons so the SQL connection strings parse correctly. Never copy local development secrets into this file or GitHub.

Find the SQL Server container's `mssql` UID, then make the data, certificate, and backup paths accessible only to that UID and root. For example, inspect the image with `docker run --rm --entrypoint /bin/sh mcr.microsoft.com/mssql/server:2025-latest -c 'id mssql'`, then use the reported UID with `chown` and `chmod`. Do not use world-readable permissions for the private key or database directory.

## Start and verify

From the repository root:

```bash
docker compose --env-file .env.production --file deploy/production/compose.yaml config --quiet
docker compose --env-file .env.production --file deploy/production/compose.yaml up --build --detach
docker compose --env-file .env.production --file deploy/production/compose.yaml ps
docker compose --env-file .env.production --file deploy/production/compose.yaml logs --follow sqlserver api frontend
```

Compose provisions the application and migration logins, applies EF Core migrations using the separate `OnlineLearningPlatformMigrator` login, and starts the API using the restricted `OnlineLearningPlatformApp` login. The migration login is not passed to the running API. The API and SQL Server use encrypted connections with certificate validation enabled. Caddy serves Angular and proxies `/api/*` and `/health/*` to the API on the same HTTPS origin.

Verify the public site, sign in separately as Student, Teacher, and Admin, and test registration verification, password reset, Cloudinary uploads (if enabled), and the `/health/ready` route. The bootstrap admin must change the initial password; remove its bootstrap credentials from `.env.production` after seeding and recreate the API container.

The SQL database and Data Protection keys use persistent host/Docker storage. Do not delete the SQL data directory or the `dataprotection-keys` volume when recreating containers.

## Backup and restore

Create an online SQL backup with:

```bash
bash deploy/production/backup.sh
```

The `.bak` file is written under the directory in `SQL_BACKUP_DIR`. Copy it to separate offsite storage (for example, a private DigitalOcean Spaces bucket) and protect it as sensitive data. A backup kept only on the same Droplet does not protect against Droplet or disk loss.

Before relying on backups, restore one into a separate test database on a separate SQL Server instance. Use `RESTORE FILELISTONLY` to read the backup's logical data/log filenames, then run `RESTORE DATABASE ... WITH MOVE ...` to restore under a different database name. Verify that the restored database is online and that expected tables and records exist. Record the tested restore date and repeat the exercise periodically.

## Remaining deployment operations

- Configure an automated schedule to call `backup.sh`, copy backups offsite, and prune local backups only after confirming offsite upload.
- Monitor Droplet disk, memory, container health, and SQL Express database growth.
- Configure certificate renewal for both Caddy's site certificate and SQL Server's certificate.
- Add malware scanning and retention controls for public uploads.
- Use a real public email sender and verify deliverability before inviting users.

This Compose file is for a small single-Droplet launch, not high availability. A Droplet failure takes both the API and SQL Server offline; use separately managed infrastructure and tested restores if the service becomes important to real users.
