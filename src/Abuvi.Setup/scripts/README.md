# verify-media-import.ps1

Dry-run verification pass for the 50th-anniversary photo archive import. Reads folders
only — **never touches the database or blob storage**. Run this before any real upload
script, from your own machine, against the real contributor folders.

## What it does

Walks one or more root folders (typically one per contributor or storage location) and,
for every media file found, works out:

- **Contributor** — the first folder segment under the root you pass in
- **Year** — the nearest ancestor folder segment that starts with a plausible camp year
  (`1976`–`2026`), e.g. `2024 - Clar del Bosc` or `2024 Clar del Bosc`
- **Venue guess** — whatever text follows the year in that same segment
- **SHA-256 hash** — to catch duplicate files, including duplicates that live under two
  different contributors' folders

It writes a CSV report and prints a console summary. It does not resolve a bare 2-digit
year (`98` instead of `1998`) — those are reported as unresolved rather than guessed at,
since a filename-only script cannot verify a short year safely.

## Usage

```powershell
.\verify-media-import.ps1 -RootPaths "D:\Historia Abuvi\Timo\Abuvi", "D:\Historia Abuvi\Maruja\Abuvi contenido"
```

| Parameter | Required | Default | Notes |
|---|---|---|---|
| `-RootPaths` | Yes | — | One or more root folders, one per contributor/location |
| `-OutCsv` | No | `media-import-review.csv` next to the script | Where the report is written |
| `-ExpectedYearMin` | No | `1976` | First ABUVI camp edition |
| `-ExpectedYearMax` | No | `2026` | Used to flag years outside the expected range |

## Reviewing the report

Open the CSV and check, in this order:

1. **Unresolved rows** (`DetectedYear` empty) — no year could be inferred from the folder
   path. These will need a manual year or will land in the "sin ubicar" pile as-is.
2. **Out-of-range years** (`YearInRange = False`) — likely a mis-scanned digit or a
   folder that isn't camp material at all.
3. **Duplicate groups** (`IsDuplicate = True`) — the same file content under more than
   one contributor's folder. `DuplicateGroupSize` shows how many copies exist.

## Relationship to the real importer

This script is a **manual pre-check**, not the importer itself. The actual bulk CLI
importer (`Task 3` of [feat-photo-albums-social](../../../ai-specs/changes/feat-photo-albums-social/feat-photo-albums-social_setup-importer_backend.md))
is a separate, not-yet-built `Abuvi.Setup` command that will read files and create
`MediaItem` rows. Run this script first, fix what it flags, and only then plan the real
import.
