# Abuvi.Setup — Deployment Guide

How to publish the database setup tool locally, upload it to a VPS, and run it.

---

## 1. Publish (local machine)

Build a self-contained binary for Linux x64 (no .NET SDK needed on the server):

```bash
dotnet publish src/Abuvi.Setup -c Release -r linux-x64 --self-contained -o ./publish/setup
```

This generates all required files in `./publish/setup/`, including the .NET runtime.

---

## 2. Upload to VPS

### Create the remote directory

```bash
ssh user@your-vps "mkdir -p ~/abuvi-setup/seed"
```

### Copy the binary and CSV seed files

```bash
scp -r ./publish/setup/* user@your-vps:~/abuvi-setup/
scp -r ./production-data/* user@your-vps:~/abuvi-setup/seed/
```

> Replace `user@your-vps` with your actual SSH user and server IP/hostname.

### Verify the upload

```bash
ssh user@your-vps "ls -la ~/abuvi-setup/"
```

You should see `Abuvi.Setup` (the executable) among the files.

---

## 3. Run on the server

### Connect via SSH

```bash
ssh user@your-vps
cd ~/abuvi-setup
```

### Make the binary executable (first time only)

```bash
chmod +x ./Abuvi.Setup
```

### Available commands

#### Initial production setup (import only, no reset)

```bash
./Abuvi.Setup setup \
  --env=production \
  --connection="Host=localhost;Database=abuvi;Username=abuvi_user;Password=YOUR_PASSWORD" \
  --dir=./seed/
```

> Safe for production: only imports into empty tables, refuses if data already exists.

#### Full reset + seed (development)

```bash
./Abuvi.Setup run-all \
  --connection="Host=localhost;Database=abuvi;Username=abuvi_user;Password=YOUR_PASSWORD"
```

> Default mode is `--env=dev`, which freely resets and re-imports.

#### Full reset + seed (production — destructive)

```bash
./Abuvi.Setup run-all \
  --env=production \
  --confirm \
  --connection="Host=localhost;Database=abuvi;Username=abuvi_user;Password=YOUR_PASSWORD"
```

> Requires `--confirm` flag **and** typing `YES` interactively.

#### Reset only (wipe data, re-seed admin)

```bash
./Abuvi.Setup reset \
  --connection="Host=localhost;Database=abuvi;Username=abuvi_user;Password=YOUR_PASSWORD"
```

#### Import a single entity

```bash
./Abuvi.Setup import users \
  --connection="Host=localhost;Database=abuvi;Username=abuvi_user;Password=YOUR_PASSWORD" \
  --dir=./seed/
```

> Valid entities: `users`, `family-units`, `family-members`, `camps`, `camp-editions`

---

## 4. Connection string

You can pass the connection string in three ways (in order of priority):

1. **CLI flag:** `--connection="Host=...;Database=...;Username=...;Password=..."`
2. **Environment variable:** `export DATABASE_URL="Host=...;Database=...;Username=...;Password=..."`
3. **Default:** `Host=localhost;Database=abuvi;Username=postgres;Password=postgres`

Using an environment variable avoids exposing the password in the command history:

```bash
export DATABASE_URL="Host=localhost;Database=abuvi;Username=abuvi_user;Password=YOUR_PASSWORD"
./Abuvi.Setup setup --env=production --dir=./seed/
```

---

## 5. Update the tool

When the setup tool changes, repeat the publish and upload steps:

```bash
# Local: rebuild
dotnet publish src/Abuvi.Setup -c Release -r linux-x64 --self-contained -o ./publish/setup

# Upload: overwrite existing files
scp -r ./publish/setup/* user@your-vps:~/abuvi-setup/
```

---

## 6. Commands summary

| Command | Description | Dev | Production |
|---------|-------------|-----|------------|
| `run-all` | Reset + import all CSVs (default) | Free | `--confirm` + "YES" |
| `setup` | Import only (no reset) | Skips duplicates | Only on empty tables |
| `reset` | Wipe all data, re-seed admin | Free | `--confirm` + "YES" |
| `import <entity>` | Import a single CSV | Skips duplicates | Only on empty table |

### Global flags

| Flag | Description |
|---|---|
| `--dry-run` | Run full pipeline (parse, validate, map) without saving. Prints a report of what would happen. Safe for repeated use. |

---

## 7. Logging

The setup tool writes structured logs to two destinations:

- **Console**: Colored output via Serilog
- **File**: `logs/setup-YYYYMMDD.log` in the tool's directory (daily rolling, 31-day retention)

### Changing log level

Set the `SETUP_LOG_LEVEL` environment variable before running:

```bash
export SETUP_LOG_LEVEL=Debug
./Abuvi.Setup setup --env=dev
```

Valid levels: `Verbose`, `Debug`, `Information` (default), `Warning`, `Error`, `Fatal`

---

## 8. Dry-run mode

Preview what the import would do without writing to the database:

```bash
./Abuvi.Setup setup \
  --env=production \
  --connection="Host=...;Database=...;Username=...;Password=..." \
  --dir=./seed/ \
  --dry-run
```

- Parses all CSV files and validates every row
- Checks for duplicates and resolves FK references
- Reports what would be imported, skipped, or fail
- **No data is written** — uses a transaction that is always rolled back
- Does not require `--confirm` even in production mode
- Exit code is always 0 (it's a report, not an action)
