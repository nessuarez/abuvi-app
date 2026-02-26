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
