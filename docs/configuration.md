# Configuration Reference

This document describes every setting in `appsettings.json` and how to configure the application for a production Docker environment.

---

## Settings Overview

### `Serilog`

Controls the application log verbosity.

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

- `Default`: the baseline log level for application code. `Information` is appropriate for production.
- `Override`: silences noisy framework namespaces. In development these are set to `Information`/`Debug` via `appsettings.Development.json`.

---

### `Seq`

Connection to the [Seq](https://datalust.co/seq) structured log server.

```json
"Seq": {
  "ServerUrl": "http://localhost:5341"
}
```

In a Docker environment, replace `localhost` with the Seq service name:

```
Seq__ServerUrl=http://seq:5341
```

---

### `LogRetention`

```json
"LogRetention": {
  "RetentionDays": 90
}
```

How many days of log records are kept in the database before the background cleanup job removes them.

---

### `AllowedHosts`

```json
"AllowedHosts": "*"
```

Standard ASP.NET Core host filtering. `"*"` is fine when a reverse proxy (Nginx, Traefik) terminates requests. Without a proxy, set this to the actual domain name.

---

### `AllowedOrigins`

```json
"AllowedOrigins": "http://localhost:5173"
```

CORS allowed origin for the frontend. **Must be updated in production** to the real frontend URL:

```
AllowedOrigins=https://abuvi.org
```

---

### `ConnectionStrings`

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=abuvi;Username=abuvi_user;Password=dev_password"
}
```

PostgreSQL connection string. In Docker, `localhost` becomes the database service name and the password comes from an environment variable:

```
ConnectionStrings__DefaultConnection=Host=db;Port=5432;Database=abuvi;Username=abuvi_user;Password=${DB_PASSWORD}
```

---

### `Jwt`

```json
"Jwt": {
  "Issuer": "https://abuvi.api",
  "Audience": "https://abuvi.app",
  "ExpiryInHours": 24
}
```

| Field | Description |
|---|---|
| `Issuer` | JWT `iss` claim. Can remain as-is or be updated to the real API domain. |
| `Audience` | JWT `aud` claim. Can remain as-is or be updated to the real frontend domain. |
| `ExpiryInHours` | How long an access token is valid. |

> **Important — `Jwt:Secret` is not in this file.** The signing secret is read from user secrets in development. In production it must be supplied as an environment variable:
>
> ```
> Jwt__Secret=${JWT_SECRET}
> ```
>
> Use a long, randomly generated string (32+ characters). Rotating this key invalidates all active sessions.

---

### `Resend`

Email delivery via [Resend](https://resend.com).

```json
"Resend": {
  "ApiKey": "",
  "FromEmail": "noreply@abuvi.org",
  "FromName": "Abuvi Camps"
}
```

`FromEmail` and `FromName` can stay in the file. The `ApiKey` is a secret and must not be committed — supply it as an environment variable:

```
Resend__ApiKey=${RESEND_API_KEY}
```

---

### `Membership`

```json
"Membership": {
  "AnnualFeeAmount": 50.0
}
```

The annual membership fee amount used when generating fee records. Changing this value does not retroactively affect already-generated fees.

---

### `Encryption`

```json
"Encryption": {
  "Key": "abuvi-dev-encryption-key-change-in-production"
}
```

Used by `EncryptionService` (AES-256) to encrypt sensitive health data stored for guests. The key string is hashed with SHA-256 to produce the 32-byte AES key.

> **Critical warnings:**
>
> - **Do not commit a production key to the repository.** Supply it via environment variable:
>
>   ```
>   Encryption__Key=${ENCRYPTION_KEY}
>   ```
>
> - **Never change this key once the database contains encrypted data.** Existing records will become unreadable. If rotation is ever needed, all encrypted fields must be decrypted with the old key and re-encrypted with the new one before switching.
> - Generate a strong key: `openssl rand -base64 32`

---

### `GooglePlaces`

```json
"GooglePlaces": {
  "ApiKey": "",
  "AutocompleteUrl": "https://maps.googleapis.com/maps/api/place/autocomplete/json",
  "DetailsUrl": "https://maps.googleapis.com/maps/api/place/details/json"
}
```

Google Places API used for camp location autocomplete. The URLs are stable and do not need to change. The `ApiKey` is a secret:

```
GooglePlaces__ApiKey=${GOOGLE_PLACES_API_KEY}
```

---

### `BlobStorage`

S3-compatible object storage used for media uploads (images, videos, audio, documents). The application uses the AWS SDK (`AWSSDK.S3`) with Hetzner Object Storage as the default provider, but any S3-compatible service works.

```json
"BlobStorage": {
  "BucketName": "abuvi-media",
  "Endpoint": "https://fsn1.your-objectstorage.com",
  "Region": "fsn1",
  "AccessKeyId": "",
  "SecretAccessKey": "",
  "PublicBaseUrl": "https://abuvi-media.fsn1.your-objectstorage.com",
  "MaxFileSizeBytes": 52428800,
  "AllowedImageExtensions": [".jpg", ".jpeg", ".png", ".webp", ".gif"],
  "AllowedVideoExtensions": [".mp4", ".mov", ".avi", ".webm"],
  "AllowedAudioExtensions": [".mp3", ".wav", ".ogg", ".m4a", ".flac", ".aac"],
  "AllowedDocumentExtensions": [".pdf", ".doc", ".docx"],
  "ThumbnailWidthPx": 400,
  "ThumbnailHeightPx": 400,
  "StorageQuotaBytes": 53687091200,
  "StorageWarningThresholdPct": 80,
  "StorageCriticalThresholdPct": 95
}
```

| Field | Description |
|---|---|
| `BucketName` | S3 bucket name. **Required** — startup validation fails if empty. |
| `Endpoint` | S3-compatible service URL. For Hetzner: `https://fsn1.your-objectstorage.com`. |
| `Region` | Storage region identifier (e.g. `fsn1` for Hetzner Falkenstein). |
| `AccessKeyId` | S3 access key. **Secret** — do not commit. |
| `SecretAccessKey` | S3 secret key. **Secret** — do not commit. |
| `PublicBaseUrl` | Public URL prefix for uploaded objects. **Required** — startup validation fails if empty. |
| `MaxFileSizeBytes` | Maximum allowed upload size in bytes. Default: `52428800` (50 MB). |
| `AllowedImageExtensions` | Permitted image file extensions. |
| `AllowedVideoExtensions` | Permitted video file extensions. |
| `AllowedAudioExtensions` | Permitted audio file extensions. |
| `AllowedDocumentExtensions` | Permitted document file extensions. |
| `ThumbnailWidthPx` | Maximum width in pixels for generated thumbnails. |
| `ThumbnailHeightPx` | Maximum height in pixels for generated thumbnails. |
| `StorageQuotaBytes` | Total storage quota in bytes. `0` disables threshold checks. Default in appsettings: ~50 GB. |
| `StorageWarningThresholdPct` | Usage percentage at which the health check reports **Degraded**. |
| `StorageCriticalThresholdPct` | Usage percentage at which the health check reports **Unhealthy**. |

`BucketName`, `Endpoint`, `Region`, and `PublicBaseUrl` can stay in the file. The `AccessKeyId` and `SecretAccessKey` are secrets and must not be committed — supply them via user secrets in development or environment variables in production:

```bash
# Development (user secrets)
dotnet user-secrets set "BlobStorage:AccessKeyId" "your-key" --project src/Abuvi.API
dotnet user-secrets set "BlobStorage:SecretAccessKey" "your-secret" --project src/Abuvi.API
```

```
# Production (environment variables)
BlobStorage__AccessKeyId=${BLOB_ACCESS_KEY_ID}
BlobStorage__SecretAccessKey=${BLOB_SECRET_ACCESS_KEY}
```

> **Note:** The Kestrel request body limit is set to 55 MB to accommodate file uploads. If you increase `MaxFileSizeBytes` beyond this value, update the Kestrel limit in `Program.cs` as well.

---

### `FrontendUrl`

```json
"FrontendUrl": "http://localhost:5173"
```

The public URL of the frontend application. Used when constructing links inside transactional emails (e.g. password reset). **Must be updated in production:**

```
FrontendUrl=https://abuvi.org
```

---

## Production Docker Configuration

.NET maps the `__` (double underscore) separator to `:` in the configuration hierarchy, so `Jwt__Secret` equals `Jwt:Secret`.

### `docker-compose.prod.yml` example

```yaml
services:
  api:
    image: abuvi-api:latest
    environment:
      # Non-secret overrides
      - ASPNETCORE_ENVIRONMENT=Production
      - AllowedOrigins=https://abuvi.org
      - FrontendUrl=https://abuvi.org
      - Seq__ServerUrl=http://seq:5341
      - ConnectionStrings__DefaultConnection=Host=db;Port=5432;Database=abuvi;Username=abuvi_user;Password=${DB_PASSWORD}

      # Blob storage
      - BlobStorage__Endpoint=https://fsn1.your-objectstorage.com
      - BlobStorage__PublicBaseUrl=https://abuvi-media.fsn1.your-objectstorage.com

      # Secrets — values come from .env file or secret manager
      - Jwt__Secret=${JWT_SECRET}
      - Encryption__Key=${ENCRYPTION_KEY}
      - Resend__ApiKey=${RESEND_API_KEY}
      - GooglePlaces__ApiKey=${GOOGLE_PLACES_API_KEY}
      - BlobStorage__AccessKeyId=${BLOB_ACCESS_KEY_ID}
      - BlobStorage__SecretAccessKey=${BLOB_SECRET_ACCESS_KEY}
```

The `${...}` values should come from a `.env` file (not committed) or a secrets manager (e.g. Docker Secrets, AWS Secrets Manager, Doppler).

---

## Database Setup Tool (`Abuvi.Setup`)

A standalone console application for resetting the database and importing seed data from CSV files. It lives at `src/Abuvi.Setup/` and references the API project to reuse `AbuviDbContext` and all entity models directly — no HTTP, no auth tokens.

> Full usage documentation: [`ai-specs/specs/development_guide.md` — Database Setup Tool section](../ai-specs/specs/development_guide.md#database-setup-tool-abuvisetup)

### Connection String

The tool does **not** read `appsettings.json`. It resolves the connection string in this order:

1. `--connection=<string>` CLI flag (highest priority)
2. `DATABASE_URL` environment variable
3. Default: `Host=localhost;Database=abuvi;Username=postgres;Password=postgres`

For production, always pass the connection string explicitly:

```bash
dotnet run --project src/Abuvi.Setup setup \
  --env=production \
  --connection="Host=db;Port=5432;Database=abuvi;Username=abuvi_user;Password=${DB_PASSWORD}" \
  --dir=./production-data/
```

### Commands

| Command | Description | Dev | Production |
| --- | --- | --- | --- |
| `run-all` | Reset + import all CSVs (default) | Free | `--confirm` + interactive "YES" |
| `setup` | Import only (no reset) | Skips duplicates | Only on empty tables |
| `reset` | Wipe all data, re-seed admin | Free | `--confirm` + interactive "YES" |
| `import <entity>` | Import a single CSV | Skips duplicates | Only on empty table |

### Options

| Flag | Description | Default |
| --- | --- | --- |
| `--env=dev\|production` | Environment mode | `dev` |
| `--connection=<str>` | PostgreSQL connection string | localhost default |
| `--dir=<path>` | CSV files directory | `./seed/` (next to executable) |
| `--confirm` | Required for production destructive ops | — |

### CSV Files

Default sample files are included at `src/Abuvi.Setup/seed/`. Import order is strict due to foreign key dependencies:

```
Users → FamilyUnits → FamilyMembers → Camps → CampEditions
```

| File | Required Columns |
| --- | --- |
| `users.csv` | email, password, firstName, lastName, role |
| `family-units.csv` | name, representativeEmail |
| `family-members.csv` | familyUnitName, firstName, lastName, dateOfBirth, relationship |
| `camps.csv` | name, pricePerAdult, pricePerChild, pricePerBaby |
| `camp-editions.csv` | campName, year, startDate, endDate, pricePerAdult, pricePerChild, pricePerBaby, status |

### Security Notes

- **Production mode** requires `--confirm` flag plus interactive "YES" confirmation for destructive operations (reset).
- **Production import** is blocked if the target table already contains data — prevents accidental duplicates.
- Passwords in CSV are plain text; they are **BCrypt-hashed (cost 12)** before insertion.
- Sensitive health fields (`medicalNotes`, `allergies`) are **excluded** from CSV import — they must be entered through the API with proper encryption.
- The admin user (`admin@abuvi.local` / `Admin@123456`) is always re-seeded after a reset. **Change the admin password immediately in production.**

---

## Quick Reference

| Setting | Secret? | Must change for prod? | Notes |
|---|:---:|:---:|---|
| `Serilog` | No | No | Already production-safe |
| `Seq:ServerUrl` | No | Yes | Point to Seq container |
| `LogRetention:RetentionDays` | No | No | Adjust to taste |
| `AllowedHosts` | No | Maybe | OK with reverse proxy |
| `AllowedOrigins` | No | Yes | Real frontend URL |
| `ConnectionStrings:DefaultConnection` | Partial | Yes | Password is secret; change host |
| `Jwt:Issuer` / `Jwt:Audience` | No | No | Can stay as-is |
| `Jwt:ExpiryInHours` | No | No | Adjust to taste |
| `Jwt:Secret` *(not in file)* | **Yes** | **Yes** | Supply via env var |
| `Resend:ApiKey` | **Yes** | **Yes** | Supply via env var |
| `Resend:FromEmail` / `FromName` | No | No | Already production values |
| `Membership:AnnualFeeAmount` | No | No | Business decision |
| `Encryption:Key` | **Yes** | **Yes** | Never rotate once data exists |
| `GooglePlaces:ApiKey` | **Yes** | **Yes** | Supply via env var |
| `GooglePlaces:AutocompleteUrl` / `DetailsUrl` | No | No | Stable Google URLs |
| `BlobStorage:BucketName` | No | No | Already set to production bucket |
| `BlobStorage:Endpoint` / `Region` | No | No | Already set to Hetzner fsn1 |
| `BlobStorage:AccessKeyId` | **Yes** | **Yes** | Supply via env var |
| `BlobStorage:SecretAccessKey` | **Yes** | **Yes** | Supply via env var |
| `BlobStorage:PublicBaseUrl` | No | No | Already set to production URL |
| `BlobStorage:MaxFileSizeBytes` | No | No | Adjust to taste (default 50 MB) |
| `BlobStorage:Allowed*Extensions` | No | No | Adjust to taste |
| `BlobStorage:Thumbnail*Px` | No | No | Adjust to taste |
| `BlobStorage:StorageQuotaBytes` | No | No | 0 disables threshold checks |
| `BlobStorage:Storage*ThresholdPct` | No | No | Health check thresholds |
| `FrontendUrl` | No | Yes | Real frontend URL |
