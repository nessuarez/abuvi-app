# Backend Implementation Plan: feat-photo-albums-social (Task 3) — Bulk Media Importer

## Overview

Implements **Task 3** of [feat-photo-albums-social_enriched.md](./feat-photo-albums-social_enriched.md): an `Abuvi.Setup` CLI command that walks a folder of historical material, resolves each file to a camp edition from folder names and EXIF, records provenance, uploads to blob storage and creates `MediaItem` rows.

This is a **console tool in `src/Abuvi.Setup/`**, not an API feature — no endpoints, no DI container, no `Program.cs` in `Abuvi.API`. It sits alongside `CampImporter` and the `geocode` command and follows their conventions exactly.

**Depends on:** Task 1 (the data model) only — the entities and migration from [feat-photo-albums-social_backend.md](./feat-photo-albums-social_backend.md) Steps 1–6 must be merged. It does **not** depend on Task 2 (the API), so this branch runs in parallel.

Getting real content into the database early makes the frontend task far easier to build and review, which is why this is worth doing before the API is finished.

---

## Architecture Context

### Files to create

```
src/Abuvi.Setup/Media/MediaImportRunner.cs         — walk, upload, persist
src/Abuvi.Setup/Media/EditionResolver.cs           — pure resolution logic (the testable core)
src/Abuvi.Setup/Media/ExifReader.cs                — ImageSharp ExifProfile wrapper
src/Abuvi.Setup/Media/MediaImportReportWriter.cs   — HTML review report
src/Abuvi.Setup/Media/MediaImportModels.cs         — MediaMatch, MediaImportOptions, records
src/Abuvi.Setup/Importers/MediaThemeImporter.cs    — seed the theme catalogue from CSV
src/Abuvi.Setup/seed/media-themes.csv              — starting theme list
```

### Files to modify

| File | Change |
|------|--------|
| `src/Abuvi.Setup/Program.cs` | New `import-media` command + usage block |
| `src/Abuvi.Setup/SetupConfig.cs` | Parse the new flags |
| `src/Abuvi.Setup/SeedRunner.cs` | Register `MediaThemeImporter` |
| `src/Abuvi.Setup/schemas/import-order.json` | Add `media-themes.csv` (no dependencies) |
| `src/Abuvi.Setup/schemas/README.md` | Document the command and the donation runbook |

### Reference implementations to copy from

- **Command wiring**: `RunGeocodeAsync` in [Program.cs:140](src/Abuvi.Setup/Program.cs#L140) — a positional command handled before the DB connection is established. `import-media` **does** need the database, so it goes in the main `switch` instead, after `db` is created.
- **Runner shape and XML doc style**: [GeocodeRunner.cs](src/Abuvi.Setup/Geocoding/GeocodeRunner.cs) — note its idempotency comment; the same discipline applies here.
- **HTML review output**: `ReviewMapWriter` produced `docs/CAMPAMENTOS_HISTORICOS-geocode-review.html`. Mirror its structure.
- **Result reporting**: `SeedResult` / `SeedResult.Print(dryRun)` in [Models.cs](src/Abuvi.Setup/Models.cs).

### Dependencies

**No new NuGet packages.**

- **ImageSharp** arrives transitively via the `Abuvi.API` project reference (`BlobStorageService` already uses it). EXIF is read through `Image.Identify(path).Metadata.ExifProfile`.
- **`BlobStorageService`** is constructible by hand — it takes `(IBlobStorageRepository, IOptions<BlobStorageOptions>, IMemoryCache)`:

```csharp
var blobOptions = config.GetSection(BlobStorageOptions.SectionName).Get<BlobStorageOptions>()!;
var blobRepo = new BlobStorageRepository(Options.Create(blobOptions), NullLogger<BlobStorageRepository>.Instance);
var blobService = new BlobStorageService(
    blobRepo, Options.Create(blobOptions), new MemoryCache(new MemoryCacheOptions()));
```

Verify `BlobStorageRepository`'s actual constructor before writing this — the geocode command builds its Google Places dependencies the same way and is the precedent for the pattern.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to the branch.
- **Branch Naming**: `feature/feat-photo-albums-social-setup-importer` — **required**, separate from the API branch.
- **Implementation Steps**:
  1. `git checkout dev`
  2. `git pull origin dev` — **base branch is `dev`, not `main`**
  3. Ensure Task 1's entities and migration are present (`git log --oneline dev | grep AddCampAlbumsThemesAndSocial`, or check that `MediaSources` exists in `AbuviDbContext`). If not, this branch cannot compile — wait for that merge.
  4. `git checkout -b feature/feat-photo-albums-social-setup-importer`
  5. `git branch` to verify

---

### Step 1: Theme Seed Importer

- **Files**: `src/Abuvi.Setup/seed/media-themes.csv`, `src/Abuvi.Setup/Importers/MediaThemeImporter.cs`
- **Action**: Seed the theme catalogue so the feature is not empty on day one and so folder-name theme matching (Step 4) has something to match against.

**`media-themes.csv`**

```csv
name,slug,description
San Abuvino,san-abuvino,Fiesta de San Abuvino
Actuaciones,actuaciones,Actuaciones y espectáculos
Asambleas,asambleas,Asambleas y reuniones
Cocina y comedor,cocina-y-comedor,Cocina, comedor y comidas
Excursiones,excursiones,Salidas y excursiones
Montaje y desmontaje,montaje-y-desmontaje,Montaje y desmontaje del campamento
Juegos de noche,juegos-de-noche,Juegos y veladas nocturnas
Deportes,deportes,Actividades deportivas
Talleres,talleres,Talleres y manualidades
```

> `description` values contain commas — `CsvHelper` already handles RFC 4180 quoting, but quote those fields in the file.

**`MediaThemeImporter`** — copy the shape of [CampEditionImporter.cs](src/Abuvi.Setup/Importers/CampEditionImporter.cs):

```csharp
public class MediaThemeImporter(AbuviDbContext db)
{
    public async Task<SeedResult> ImportAsync(string filePath)
    {
        // For each row: require name + slug; skip when slug already exists
        // (duplicate check is on slug, case-insensitive); insert MediaTheme.
        // Return new SeedResult("MediaThemes", total, imported, skipped, results);
    }
}
```

Register it in `SeedRunner` and add to `import-order.json` with `"dependsOn": []` — same tier as `camps.csv`.

---

### Step 2: Import Models

- **File**: `src/Abuvi.Setup/Media/MediaImportModels.cs`

```csharp
namespace Abuvi.Setup.Media;

public enum MatchConfidence { None, Medium, High }

/// <summary>What we managed to work out about one file, and why.</summary>
public record MediaMatch(
    Guid? CampEditionId,
    int? Year,
    MediaItemYearSource YearSource,
    MatchConfidence Confidence,
    IReadOnlyList<Guid> ThemeIds,
    IReadOnlyList<string> Evidence);   // human-readable trail for the review report

public record MediaImportOptions(
    string RootDir,
    bool DryRun,
    MatchConfidence MinConfidence,
    string ReportPath,
    Guid UploaderUserId,
    Guid? MediaSourceId,
    string? SourceName,
    Guid? SourceUserId,
    string? SourceContact,
    string? SourceNotes);

public record MediaImportRow(
    string RelativePath,
    MediaItemType Type,
    MediaMatch Match,
    bool Skipped,
    string? SkipReason,
    string? ThumbnailUrl);
```

---

### Step 3: EXIF Reader

- **File**: `src/Abuvi.Setup/Media/ExifReader.cs`
- **Action**: Read capture date and GPS from an image, tolerating every kind of missing or malformed metadata.

```csharp
public record ExifData(int? Year, double? Latitude, double? Longitude);

public static class ExifReader
{
    /// <summary>
    /// Never throws. Historical scans routinely carry absent, truncated or
    /// nonsensical EXIF; a bad tag must not abort a 5.000-file import.
    /// </summary>
    public static ExifData Read(string path)
    {
        try
        {
            var info = Image.Identify(path);
            var exif = info?.Metadata?.ExifProfile;
            if (exif is null) return new ExifData(null, null, null);

            int? year = null;
            if (exif.TryGetValue(ExifTag.DateTimeOriginal, out var dto) &&
                DateTime.TryParseExact(dto.Value, "yyyy:MM:dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                year = parsed.Year;

            var (lat, lon) = ReadGps(exif);
            return new ExifData(year, lat, lon);
        }
        catch
        {
            return new ExifData(null, null, null);
        }
    }
}
```

**Implementation Notes**:
- `DateTimeOriginal` is EXIF tag 36867 and uses the `yyyy:MM:dd HH:mm:ss` format with colons in the date — `DateTime.Parse` will not handle it. Use `TryParseExact`.
- GPS is stored as degrees/minutes/seconds rationals plus an N/S/E/W reference. Convert to signed decimal degrees; negate for S and W.
- Sanity-check the result: a year outside 1970–(current year) or coordinates outside valid ranges are treated as absent, not as data.

---

### Step 4: Edition Resolver — the core

- **File**: `src/Abuvi.Setup/Media/EditionResolver.cs`
- **Action**: Pure logic mapping a file path + EXIF to a `MediaMatch`. **No I/O, no database, no blob calls** — everything it needs is passed in as pre-loaded lists, which is what makes it unit-testable.

```csharp
public class EditionResolver(
    IReadOnlyList<CampEdition> editions,   // pre-loaded, with Camp navigation
    IReadOnlyList<Camp> camps,
    IReadOnlyList<MediaTheme> themes)
{
    public MediaMatch Resolve(string relativePath, ExifData exif);
}
```

#### Normalisation helper

Shared by venue and theme matching:

```csharp
private static readonly string[] Stopwords =
    ["campa", "campamento", "abuvi", "fotos", "foto", "verano"];

/// <summary>lowercase, strip accents, drop stopwords, collapse whitespace.</summary>
private static string Normalize(string segment);
```

#### Step 4.1 — Folder-name year

Scan path segments **from the file's own folder outward**; first match wins.

- 4-digit: `\b(19[7-9]\d|20[0-4]\d)\b` → `2003 Espinosa`, `Campa 1998`
- 2-digit, **only** when the same segment also matched a camp name: `\b([7-9]\d|[0-2]\d)\b` → `Selva de Oza 77` → 1977

> **Never accept a bare 2-digit number without camp-name evidence in the same segment.** `carrete 77` is a roll number, not a year. This single rule prevents the most common class of wrong assignment.

#### Step 4.2 — Folder-name venue

Normalise each segment, match against normalised `Camp.Name`. Accept on token containment **or** Levenshtein distance ≤ 2. Implement Levenshtein as a small private static method and unit-test it.

#### Step 4.3 — Folder-name theme

Match remaining normalised segments against `MediaTheme.Name` and `Slug`, same comparison.

**Themes attach regardless of whether the edition resolved.** A `Fotos sueltas/San Abuvino/` folder still produces a themed, undated item — a far better starting point for the community than an untagged one, and the theme itself becomes a dating clue in the API.

#### Step 4.4 — EXIF (images only)

Capture year from `DateTimeOriginal`. GPS → nearest `Camp` within **25 km**.

**No haversine helper exists in this codebase** — verify with `grep -rn "6371\|[Hh]aversine" src/`. Add a small private one and unit-test it:

```csharp
private const double EarthRadiusKm = 6371.0;

private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
{
    var dLat = (lat2 - lat1) * Math.PI / 180;
    var dLon = (lon2 - lon1) * Math.PI / 180;
    var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
    return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
}
```

#### Step 4.5 — Combine

There is exactly one edition per year in the historical data, so a resolved year alone usually determines the edition.

| Evidence | Edition | Confidence | `YearSource` |
|----------|---------|------------|--------------|
| Folder year + folder venue agree on one edition | that edition | `High` | `FolderName` |
| Folder year → unique edition for that year | that edition | `High` | `FolderName` |
| Folder venue only, venue has exactly one edition | that edition | `Medium` | `FolderName` |
| EXIF year → unique edition, no folder evidence | that edition | `Medium` | `Exif` |
| EXIF GPS venue only, one edition at that venue | that edition | `Medium` | `Exif` |
| Folder year and EXIF year disagree | **folder year wins** | `Medium` | `FolderName` |
| Nothing resolves | `null` | `None` | `Unknown` |

**Folder year beats EXIF year** because scanned negatives carry the scanner's date, not the photograph's. Record both in `Evidence` so the review report can flag the conflict for a human.

Every branch appends to `Evidence` — e.g. `"folder segment '1998 Selva de Oza' → year 1998"`, `"EXIF DateTimeOriginal → 2007 (conflicts with folder year 1998; folder wins)"`. This trail is the entire value of the review report.

---

### Step 5: Import Runner

- **File**: `src/Abuvi.Setup/Media/MediaImportRunner.cs`

```csharp
public class MediaImportRunner(
    AbuviDbContext db,
    IBlobStorageService blobService,
    BlobStorageOptions blobOptions)
{
    public async Task<(SeedResult Result, IReadOnlyList<MediaImportRow> Rows)> RunAsync(
        MediaImportOptions options, CancellationToken ct = default);
}
```

#### 5.1 — File type mapping

Map extension → `MediaItemType` using the lists already in `BlobStorageOptions`. **Not photo-only:**

| Extension list | `MediaItemType` | Thumbnail | EXIF |
|----------------|-----------------|-----------|------|
| `AllowedImageExtensions` | `Photo` | yes | yes |
| `AllowedVideoExtensions` | `Video` | no | no |
| `AllowedAudioExtensions` | `Audio` | no | no |
| `AllowedDocumentExtensions` | `Document` | no | no |

Non-image files resolve from **folder evidence only** — that is the whole difference. An unresolved audio file lands in the unplaced pile exactly like an unresolved photo. Anything outside all four lists, or over `MaxFileSizeBytes`, is logged and skipped with a reason.

`Interview` is never inferred — audio imports as `Audio`; an admin reclassifies later.

#### 5.2 — Provenance

Resolve **once per run**, before the walk:

- `--source-id=<guid>` → load and reuse that `MediaSource`
- `--source-name="…"` → create **one** `MediaSource` for the whole run, with `RegisteredByUserId` = the uploader; `--source-user=<email>` resolves to a `User` and sets `ContributorUserId`; `--source-contact` and `--source-notes` fill the rest
- neither → `MediaSourceId` stays null

> **One source row per run, never per file.** A 800-file donation must produce exactly one `media_sources` row — this is asserted by a unit test.

`--source-name` is what makes a shoebox donation traceable a decade later, and `--source-user` is what later enables *"ask this person"* on undated items. The runbook should treat both as effectively required.

#### 5.3 — `SourcePath`

Record the path **relative to `--dir`**, never the absolute path:

```csharp
var relativePath = Path.GetRelativePath(options.RootDir, filePath).Replace('\\', '/');
```

This drops `C:/Users/<name>/` at the point of capture. The association has no reason to store a donor's home-directory layout, and the camp clues live in the trailing segments anyway. **Unit-tested.**

#### 5.4 — Idempotency

SHA-256 of the file bytes; skip when a `MediaItem` already exists with that hash.

```csharp
item.Context = $"import:{Convert.ToHexString(hash)[..16].ToLowerInvariant()}";
```

Stored in `MediaItem.Context`, which is already indexed — **no schema change**. Pre-load existing import hashes into a `HashSet<string>` before the walk rather than querying per file.

> Re-running the import must produce zero duplicates. This is the property that makes it safe to run repeatedly as more material arrives.

#### 5.5 — Upload and persist

```csharp
await using var stream = File.OpenRead(filePath);
var upload = await blobService.UploadAsync(
    stream, Path.GetFileName(filePath), contentType,
    folder: "anniversary",
    contextId: match.CampEditionId,
    generateThumbnail: type == MediaItemType.Photo,
    ct);
```

Then create the `MediaItem`:

| Field | Value |
|-------|-------|
| `Type` | mapped from extension |
| `Title` | original filename without extension |
| `UploadedByUserId` | from `--uploader=<email>`, default the seeded admin |
| `IsApproved` / `IsPublished` | **`true`** — historical archive material is vetted by whoever runs the import |
| `Year` / `Decade` | from the match; `Decade` via the existing `MediaItemMappingExtensions.DeriveDecade` |
| `CampEditionId` / `YearSource` | from the match |
| `MediaSourceId` / `SourcePath` | from 5.2 / 5.3 |
| `Context` | `import:<hash16>` |

Plus one `MediaItemTheme` row per matched theme.

**Below `--min-confidence` (default `medium`)** → still imported, with `CampEditionId = null` and **themes attached**. Not discarded. This is the pile the community dates.

#### 5.6 — Dry run

`--dry-run` resolves, reports and writes the HTML — but **uploads nothing and saves nothing**. Consistent with `SetupConfig.DryRun` elsewhere. Guard both the blob call and `SaveChangesAsync`.

Stream files one at a time. Never load a folder into memory.

---

### Step 6: Review Report Writer

- **File**: `src/Abuvi.Setup/Media/MediaImportReportWriter.cs`
- **Action**: Self-contained HTML mirroring `docs/CAMPAMENTOS_HISTORICOS-geocode-review.html`. Default output `docs/media-import-review.html`.

Per row: thumbnail (images) or a type icon, relative path, resolved edition and year, confidence, themes, and the `Evidence` trail.

**Group unresolved and conflicting files at the top** — they are the ones a human should actually eyeball. A report that buries 40 problems under 3.000 successes does not get read.

Highlight folder/EXIF year conflicts in a distinct colour.

---

### Step 7: Wire the Command into `Program.cs`

- **File**: `src/Abuvi.Setup/Program.cs`

`import-media` needs the database, so it goes in the main `switch` **after** `db` is created — unlike `geocode`, which runs before the connection because it only touches CSVs.

```csharp
case "import-media":
    return await RunMediaImportAsync(db, config, args);
```

Update the usage block:

```
  import-media --dir=<path>   Import photos, audio, video and documents from a folder tree

Options for import-media:
  --dir=<path>            Root folder to walk (required)
  --min-confidence=<lvl>  none|medium|high (default: medium)
  --report=<path>         HTML review report (default: docs/media-import-review.html)
  --uploader=<email>      Account credited with the upload (default: seeded admin)
  --source-name=<name>    Who provided the material — strongly recommended
  --source-user=<email>   Link the provider to a registered user
  --source-contact=<val>  Provider email/phone (Admin-only data)
  --source-notes=<text>   How the material arrived
  --source-id=<guid>      Reuse an existing contributor instead of creating one
```

Add the flags to `SetupConfig.Parse`. Reuse the existing `ArgValue(args, "--flag")` helper.

**Safety**: `--dry-run` first is mandatory in the runbook. Apply the existing `SafetyGuard` conventions for production runs — this command uploads to **production blob storage**, which is not reversible with a database rollback.

---

### Step 8: Write Unit Tests

`src/Abuvi.Tests/Unit/Setup/Media/`. Follow the existing [CsvHelperTests.cs](src/Abuvi.Tests/Unit/Setup/CsvHelperTests.cs) and `GeocodeRunnerTests` style.

#### `EditionResolverTests` — one test per row of the Step 4.5 table, plus:

**Successful cases**
- `1998/IMG_001.jpg` → 1998, `High`, `FolderName`
- `Campa 1998/` → 1998
- `2003 Espinosa/` → edition 2003 at Espinosa, `High`
- `Selva de Oza 77/` → 1977 (2-digit accepted **because** the venue matched)
- `1998/San Abuvino/foto.jpg` → edition **and** theme
- `Fotos sueltas/San Abuvino/foto.jpg` → theme only, `CampEditionId` null

**Edge cases**
- `carrete 77/` with no venue match → **unresolved**, not 1977
- Folder year 1998 vs EXIF year 2007 → folder wins, `Medium`, both recorded in `Evidence`
- Venue-only folder where the venue has several editions → unresolved
- `Levenshtein("espinossa", "espinosa") <= 2` → venue matches despite the typo
- Accented and uppercase segments normalise identically
- `DistanceKm` against two known coordinate pairs

#### `ExifReaderTests`

- Reads `DateTimeOriginal` and GPS from a fixture image in `src/Abuvi.Tests/Helpers/TestFiles/`
- Image with no EXIF → all nulls, no throw
- Corrupt/truncated file → all nulls, no throw
- Year outside 1970–now → treated as absent
- Southern/western hemisphere coordinates come back negative

#### `MediaImportRunnerTests`

- **Dry run performs no uploads** — assert on a substituted `IBlobStorageService` (NSubstitute: `blobService.DidNotReceive().UploadAsync(...)`) and no rows saved
- Duplicate hash → skipped on the second run; **re-running a full import creates zero new rows**
- **An `.mp3` and a `.pdf` import with the right `MediaItemType` and no thumbnail**
- Unresolved files still produce a `MediaItem` with `CampEditionId = null` **and their themes attached**
- **`--source-name` creates exactly one `MediaSource` for a 50-file run, not fifty**
- `SourcePath` is relative to `--dir` and never contains the absolute prefix
- Files over `MaxFileSizeBytes` or with unknown extensions are skipped with a reason, not crashed on

#### `MediaThemeImporterTests`

- Imports the seed CSV; re-running skips existing slugs
- Rows with a missing name or slug are reported as failures, not thrown

---

### Step 9: Update Technical Documentation

- **Action**: **MANDATORY** before considering the implementation complete.
- **Implementation Steps**:
  1. **Review Changes** — new CLI command, new seed CSV, new import order entry.
  2. **Identify Documentation Files**:
     - `src/Abuvi.Setup/schemas/README.md` — the `import-media` command, every flag, the expected folder conventions (`<year> <venue>/`, `<year>/<theme>/`), and a **donation runbook**: always `--dry-run` first, always pass `--source-name`, and tell the contributor their name will be shown to members
     - `src/Abuvi.Setup/schemas/import-order.json` — `media-themes.csv`, `dependsOn: []`
     - [data-model.md](ai-specs/specs/data-model.md) — only if this task changed the model; it should not have
     - [INDEX.md](ai-specs/changes/INDEX.md) — reflect importer progress
  3. **Update Documentation** — English, matching existing structure.
  4. **Verify** — accurate and consistently formatted.
  5. **Report Updates** — list files changed and how.
- **References**: [documentation-standards.mdc](ai-specs/specs/documentation-standards.mdc).

---

## Implementation Order

1. **Step 0** — Branch `feature/feat-photo-albums-social-setup-importer` off `dev`, confirm Task 1 is merged
2. **Step 1** — Theme seed CSV + `MediaThemeImporter`
3. **Step 2** — Import models
4. **Step 3** — `ExifReader`
5. **Step 4** — `EditionResolver` (write tests alongside — this is the piece worth TDD-ing)
6. **Step 5** — `MediaImportRunner`
7. **Step 6** — `MediaImportReportWriter`
8. **Step 7** — `Program.cs` command wiring
9. **Step 8** — Unit tests
10. **Step 9** — Update technical documentation

---

## Testing Checklist

- [ ] `dotnet build` clean; `dotnet test` green
- [ ] `import-media --dry-run` on a real folder writes **nothing** to blob storage or the database
- [ ] HTML review report opens and is readable; unresolved files grouped first
- [ ] Re-running a completed import produces **zero** duplicates
- [ ] Images, audio, video and documents all import with the correct `MediaItemType`
- [ ] Thumbnails generated for images only
- [ ] `--source-name` produces exactly one `MediaSource` per run
- [ ] `SourcePath` verified to exclude the absolute prefix
- [ ] Themes attached even when the edition is unresolved
- [ ] Unresolved files land in the unplaced pile rather than being discarded
- [ ] A corrupt image does not abort the run

---

## Error Response Format

Not applicable — this is a console tool. It reports through `SeedResult.Print(dryRun)` and Serilog, and returns an exit code:

| Exit code | Meaning |
|-----------|---------|
| `0` | Completed (possibly with skipped rows, reported individually) |
| `1` | Fatal: missing `--dir`, unreadable folder, no DB connection, blob storage unreachable |

Per-file failures are collected as `SeedRowResult` entries and printed at the end. **One bad file never aborts the run** — a 5.000-file import that dies on file 4.000 because of a corrupt JPEG is worse than useless.

---

## Notes

**Business rules**

- Exactly one `CampEdition` per historical year (50 editions, 1976–2025). The resolver leans on this; assert `Count == 1` rather than assuming.
- `CampEditionId = null` means *"edition unknown"* — always temporary. Unresolved files are imported, never discarded.
- Folder year beats EXIF year: scans carry the scanner's date.
- Bare 2-digit numbers are only years when the same segment names a venue.

**Language**

- Code, comments and the review report's structure in **English**.
- Log messages follow the existing Serilog style in `Abuvi.Setup` (English, structured properties).
- The seed theme names are **Spanish** — they are user-facing content.

**RGPD / GDPR**

- `--source-contact` writes personal data about someone who may not be a member. It is Admin/Board-only in the API and must not be echoed into the HTML report.
- `SourcePath` is stored relative to `--dir` precisely to avoid capturing a donor's home-directory layout.
- The donation runbook must state that the contributor's **name will be visible to members** as attribution.

**Operational**

- This command uploads to **production blob storage**. A database rollback does not undo uploads.
- A bulk import moves the blob quota needle, and video is far heavier than photos. Check `GET /api/blobs/stats` before and after; the 80 %/95 % thresholds in `BlobStorageOptions` already warn.

---

## Next Steps After Implementation

1. Open the PR against **`dev`**.
2. Dry-run against the real historical folders and review the HTML report with someone who recognises the material — the `Evidence` trail exists for exactly this conversation.
3. Tune `--min-confidence` based on what that review shows before running for real.
4. Run for real, per donation, with `--source-name` set each time.
5. The unplaced pile is now populated — the frontend's collaborative dating (Task 4) has something to work on.

---

## Implementation Verification

**Code Quality**
- [ ] C# analyzers clean; nullable reference types satisfied
- [ ] `EditionResolver` is pure — no I/O, no `DbContext`, no blob calls
- [ ] `ExifReader` never throws
- [ ] `CancellationToken` threaded through the walk

**Functionality**
- [ ] All four media types import correctly
- [ ] Dry run is genuinely inert
- [ ] Idempotent across repeated runs

**Testing**
- [ ] ≥ 90 % coverage on `EditionResolver` (xUnit + FluentAssertions + NSubstitute)
- [ ] Fixture images committed to `src/Abuvi.Tests/Helpers/TestFiles/`

**Integration**
- [ ] `media-themes.csv` imports via the normal seed flow
- [ ] `import-order.json` valid

**Documentation**
- [ ] `schemas/README.md` documents the command, flags and donation runbook
- [ ] `Program.cs` usage block updated
