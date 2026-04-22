# Local Blob Storage Development Environment (MinIO)

**Depends on:** `feat/blob-storage` (current branch)
**Priority:** High — developers cannot test blob storage features locally without real Hetzner credentials

---

## Context

The blob storage service (`IBlobStorageService` / `BlobStorageRepository`) uses the S3-compatible API via `AWSSDK.S3`. In production it targets Hetzner Object Storage, but for local development there is no way to test uploads, deletions, or the health check without real credentials and an internet connection.

MinIO is a lightweight, S3-compatible object storage server that runs as a Docker container. Adding it to the local `docker-compose.yml` allows developers to test the full blob storage pipeline locally — including uploads, thumbnail generation, public URL access, storage stats, and health checks — without any external dependency.

---

## Scope

### What this adds

1. **MinIO service** in `docker-compose.yml` with persistent volume.
2. **Bucket auto-creation** on startup via the MinIO Client (`mc`) init container.
3. **Development configuration overrides** in `appsettings.Development.json` pointing to local MinIO.
4. **Documentation update** in `docs/configuration.md` with local development instructions.

### What this does NOT change

- No changes to `BlobStorageService`, `BlobStorageRepository`, or any C# code.
- No changes to production configuration or `appsettings.json`.
- No new NuGet packages.

---

## Technical Design

### 1. Docker Compose — MinIO service

Add to `docker-compose.yml`:

```yaml
  minio:
    image: minio/minio:latest
    container_name: abuvi-minio
    command: server /data --console-address ":9001"
    environment:
      MINIO_ROOT_USER: minioadmin
      MINIO_ROOT_PASSWORD: minioadmin
    ports:
      - "9000:9000"   # S3 API
      - "9001:9001"   # Web console
    volumes:
      - minio-data:/data
    healthcheck:
      test: ["CMD", "mc", "ready", "local"]
      interval: 10s
      timeout: 5s
      retries: 5

  minio-init:
    image: minio/mc:latest
    container_name: abuvi-minio-init
    depends_on:
      minio:
        condition: service_healthy
    entrypoint: >
      /bin/sh -c "
      mc alias set local http://minio:9000 minioadmin minioadmin;
      mc mb --ignore-existing local/abuvi-media;
      mc anonymous set download local/abuvi-media;
      exit 0;
      "
```

Add to `volumes:` section:

```yaml
  minio-data:
```

**Key decisions:**

| Decision | Rationale |
| --- | --- |
| `mc anonymous set download` | Makes the bucket publicly readable, matching the Hetzner production bucket policy (`S3CannedACL.PublicRead`). Allows the frontend to load files via `PublicBaseUrl` without signed URLs. |
| Separate `minio-init` container | Runs once to create the bucket and set the anonymous policy, then exits. Keeps the main MinIO container clean. |
| Persistent volume `minio-data` | Uploaded files survive `docker-compose down` / `up` cycles during development. |
| Default credentials `minioadmin/minioadmin` | Standard MinIO dev defaults. Not a security concern — local development only. |

### 2. Development configuration

Update `src/Abuvi.API/appsettings.Development.json` to add BlobStorage overrides:

```json
"BlobStorage": {
  "Endpoint": "http://localhost:9000",
  "Region": "us-east-1",
  "AccessKeyId": "minioadmin",
  "SecretAccessKey": "minioadmin",
  "PublicBaseUrl": "http://localhost:9000/abuvi-media"
}
```

**Notes:**

- `Region` is set to `us-east-1` — MinIO accepts any region but this is the standard S3 default.
- `BucketName` is inherited from `appsettings.json` (`abuvi-media`), no override needed.
- `PublicBaseUrl` points to MinIO's local S3 API. Files are accessible at `http://localhost:9000/abuvi-media/{key}` thanks to the anonymous download policy.
- `AccessKeyId` / `SecretAccessKey` can be committed in the dev config — these are local MinIO defaults, not real credentials.

### 3. ForcePathStyle compatibility

The existing `BlobStorageRepository` already uses `ForcePathStyle = true` in the `AmazonS3Config`, which is required for both Hetzner and MinIO. No code changes needed.

### 4. Documentation

Add a "Local Development with MinIO" section to `docs/configuration.md` explaining:

- How to start MinIO: `docker-compose up -d minio`
- Web console access: `http://localhost:9001` (minioadmin / minioadmin)
- How files are served: `http://localhost:9000/abuvi-media/{key}`
- No code changes needed — `appsettings.Development.json` handles the routing

---

## Acceptance Criteria

- [ ] `docker-compose up -d` starts MinIO alongside PostgreSQL and Seq without errors.
- [ ] The `abuvi-media` bucket is automatically created on first startup.
- [ ] The API can upload a file via `POST /api/blobs/upload` and it appears in MinIO.
- [ ] Uploaded files are publicly accessible at `http://localhost:9000/abuvi-media/{key}`.
- [ ] Thumbnail generation works (upload an image with `generateThumbnail=true`).
- [ ] `GET /api/blobs/stats` returns correct storage usage from MinIO.
- [ ] The blob storage health check (`/health`) reports healthy.
- [ ] `appsettings.Development.json` contains MinIO overrides; `appsettings.json` is unchanged.
- [ ] MinIO web console is accessible at `http://localhost:9001`.
- [ ] `docs/configuration.md` documents the local MinIO setup.

---

## Implementation Steps

1. Add `minio` and `minio-init` services to `docker-compose.yml`.
2. Add `minio-data` volume.
3. Add `BlobStorage` overrides to `appsettings.Development.json`.
4. Add "Local Development with MinIO" section to `docs/configuration.md`.
5. Test the full flow: start containers, upload file, verify public access, check health.

---

## Testing Notes

- **No new unit/integration tests required** — this is infrastructure configuration only.
- Manual verification is sufficient: upload a file, check MinIO console, verify public URL.
- Existing `BlobStorageEndpointsTests` and `BlobStorageServiceTests` mock the repository and do not require a running MinIO instance.
