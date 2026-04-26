# Backend Implementation Plan: feat-registration-export-filters — Registration Export & Advanced Filters

## Overview

Add two capabilities to the admin registrations feature:

1. **Advanced filters** on the existing admin list endpoint: filter by accommodation type and by extras selected.
2. **CSV export** endpoint that streams a UTF-8 with BOM file of all matching registrations (respecting the same filters), with one row per registration and dynamic columns per camp-edition extra.

No database schema changes. All work is in the `Registrations` feature slice (`src/Abuvi.API/Features/Registrations/`).

---

## Architecture Context

**Feature slice**: `src/Abuvi.API/Features/Registrations/`

| File | Change type |
|------|-------------|
| `RegistrationsRepository.cs` | Modify — update `IRegistrationsRepository` interface + `GetAdminPagedAsync` implementation + add `GetAllForExportAsync` |
| `RegistrationsService.cs` | Modify — inject `ICampEditionExtrasRepository`, update `GetAdminListAsync`, add `ExportToCsvAsync` |
| `RegistrationsEndpoints.cs` | Modify — update `GetAdminRegistrations` handler params + add `ExportRegistrationsToCsv` endpoint handler |
| `RegistrationsModels.cs` | Modify — no new DTOs needed; the export returns a raw file |
| **New** `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsExportServiceTests.cs` | Create |

**Cross-cutting**: No changes to `Program.cs` — `ICampEditionExtrasRepository` is already registered; no new middleware.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to feature branch before any code changes.
- **Branch name**: `feature/feat-registration-export-filters-backend`
- **Base branch**: `dev`
- **Commands**:
  ```bash
  git checkout dev
  git pull origin dev
  git checkout -b feature/feat-registration-export-filters-backend
  git branch   # verify
  ```

---

### Step 1: Update `IRegistrationsRepository` and `GetAdminPagedAsync`

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs`

**Action**: Add two new optional filter params to the interface method and implementation.

#### 1a. Update the interface

Change the existing signature:

```csharp
// BEFORE
Task<(List<AdminRegistrationProjection> Items, int TotalCount, AdminRegistrationTotals Totals)>
    GetAdminPagedAsync(Guid campEditionId, int page, int pageSize, string? search, string? status, CancellationToken ct);

// AFTER
Task<(List<AdminRegistrationProjection> Items, int TotalCount, AdminRegistrationTotals Totals)>
    GetAdminPagedAsync(
        Guid campEditionId, int page, int pageSize,
        string? search, string? status,
        IReadOnlyList<AccommodationType>? accommodationTypes,
        IReadOnlyList<Guid>? extraIds,
        CancellationToken ct);
```

#### 1b. Update the implementation

In `RegistrationsRepository.GetAdminPagedAsync`, after the existing status and search filters and **before** the `totalCount` call, add:

```csharp
// Accommodation type filter — match if any preference order has this type
if (accommodationTypes?.Count > 0)
{
    query = query.Where(x =>
        db.RegistrationAccommodationPreferences.Any(p =>
            p.RegistrationId == x.Id &&
            db.CampEditionAccommodations.Any(a =>
                a.Id == p.CampEditionAccommodationId &&
                accommodationTypes.Contains(a.AccommodationType))));
}

// Extras filter — match if registration selected at least one of these extras (quantity > 0)
if (extraIds?.Count > 0)
{
    query = query.Where(x =>
        db.RegistrationExtras.Any(e =>
            e.RegistrationId == x.Id &&
            extraIds.Contains(e.CampEditionExtraId) &&
            e.Quantity > 0));
}
```

**Important**: these filters must be applied **after** the `select new { ... }` projection but **before** `totalCount`. The projection exposes `x.Id` which is needed for the subqueries. Both subqueries join back into `db.RegistrationAccommodationPreferences` / `db.RegistrationExtras` — EF Core translates these to SQL `EXISTS` subqueries.

---

### Step 2: Add `GetAllForExportAsync` to the repository

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs`

**Action**: Add a new method that fetches all matching registrations (no pagination) with all related data eagerly loaded.

#### 2a. Add to interface

```csharp
Task<IReadOnlyList<Registration>> GetAllForExportAsync(
    Guid campEditionId,
    string? search,
    string? status,
    IReadOnlyList<AccommodationType>? accommodationTypes,
    IReadOnlyList<Guid>? extraIds,
    CancellationToken ct);
```

#### 2b. Add implementation

```csharp
public async Task<IReadOnlyList<Registration>> GetAllForExportAsync(
    Guid campEditionId,
    string? search,
    string? status,
    IReadOnlyList<AccommodationType>? accommodationTypes,
    IReadOnlyList<Guid>? extraIds,
    CancellationToken ct)
{
    var query = db.Registrations
        .AsNoTracking()
        .Where(r => r.CampEditionId == campEditionId)
        .Include(r => r.FamilyUnit)
        .Include(r => r.RegisteredByUser)
        .Include(r => r.Members).ThenInclude(m => m.FamilyMember)
        .Include(r => r.Extras).ThenInclude(e => e.CampEditionExtra)
        .Include(r => r.AccommodationPreferences)
            .ThenInclude(p => p.CampEditionAccommodation)
        .Include(r => r.Payments)
        .AsQueryable();

    if (!string.IsNullOrWhiteSpace(status) &&
        Enum.TryParse<RegistrationStatus>(status, true, out var statusEnum))
        query = query.Where(r => r.Status == statusEnum);

    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim().ToLower();
        query = query.Where(r =>
            r.FamilyUnit.Name.ToLower().Contains(term) ||
            (r.RegisteredByUser.FirstName + " " + r.RegisteredByUser.LastName).ToLower().Contains(term));
    }

    if (accommodationTypes?.Count > 0)
        query = query.Where(r =>
            r.AccommodationPreferences.Any(p =>
                accommodationTypes.Contains(p.CampEditionAccommodation.AccommodationType)));

    if (extraIds?.Count > 0)
        query = query.Where(r =>
            r.Extras.Any(e => extraIds.Contains(e.CampEditionExtraId) && e.Quantity > 0));

    return await query
        .OrderBy(r => r.FamilyUnit.Name)
        .ToListAsync(ct);
}
```

**Note**: The accommodation-type filter here can use the navigation property directly since we're using `.Include()`. This is simpler than the join-based approach used in `GetAdminPagedAsync`.

---

### Step 3: Update `RegistrationsService` — inject extra repository + update `GetAdminListAsync`

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`

#### 3a. Inject `ICampEditionExtrasRepository`

The service already injects `ICampEditionsRepository` (which has `GetExtraByIdAsync`). We need `ICampEditionExtrasRepository.GetByCampEditionAsync()` to fetch all active extras for the CSV column headers.

Add `ICampEditionExtrasRepository extrasDefinitionRepo` to the primary constructor:

```csharp
public class RegistrationsService(
    IRegistrationsRepository registrationsRepo,
    IRegistrationExtrasRepository extrasRepo,
    IRegistrationAccommodationPreferencesRepository accommodationPrefsRepo,
    IFamilyUnitsRepository familyUnitsRepo,
    ICampEditionsRepository campEditionsRepo,
    ICampEditionAccommodationsRepository accommodationsRepo,
    ICampEditionExtrasRepository extrasDefinitionRepo,   // NEW
    RegistrationPricingService pricingService,
    IEmailService emailService,
    Payments.IPaymentsService paymentsService,
    IMembershipsRepository membershipsRepo,
    ILogger<RegistrationsService> logger)
```

`ICampEditionExtrasRepository` is already registered in DI (used by the Camps feature slice).

#### 3b. Update `GetAdminListAsync` signature and call

```csharp
// BEFORE
public async Task<AdminRegistrationListResponse> GetAdminListAsync(
    Guid campEditionId, int page, int pageSize, string? search, string? status, CancellationToken ct)

// AFTER
public async Task<AdminRegistrationListResponse> GetAdminListAsync(
    Guid campEditionId, int page, int pageSize, string? search, string? status,
    IReadOnlyList<AccommodationType>? accommodationTypes,
    IReadOnlyList<Guid>? extraIds,
    CancellationToken ct)
```

Pass the new params to the repository call:

```csharp
var (items, totalCount, totals) = await registrationsRepo.GetAdminPagedAsync(
    campEditionId, page, pageSize, search, status,
    accommodationTypes, extraIds,   // NEW
    ct);
```

---

### Step 4: Add `ExportToCsvAsync` to `RegistrationsService`

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`

**Action**: Add a new public method that builds and returns the CSV as a `byte[]` plus a suggested filename.

```csharp
public async Task<(byte[] Content, string FileName)> ExportToCsvAsync(
    Guid campEditionId,
    string? search,
    string? status,
    IReadOnlyList<AccommodationType>? accommodationTypes,
    IReadOnlyList<Guid>? extraIds,
    CancellationToken ct)
{
    // 1. Verify edition exists
    var edition = await campEditionsRepo.GetByIdAsync(campEditionId, ct)
        ?? throw new NotFoundException("Edición de Campamento", campEditionId);

    // 2. Fetch all active extras for this edition (defines dynamic columns, ordered by SortOrder)
    var allExtras = await extrasDefinitionRepo.GetByCampEditionAsync(campEditionId, activeOnly: true, ct);
    allExtras = [.. allExtras.OrderBy(e => e.SortOrder)];

    // 3. Fetch all matching registrations with full details
    var registrations = await registrationsRepo.GetAllForExportAsync(
        campEditionId, search, status, accommodationTypes, extraIds, ct);

    // 4. Build CSV
    var csv = new StringBuilder();

    // UTF-8 BOM
    csv.Append('﻿');

    // Header row
    var headers = new List<string>
    {
        "ID Inscripción", "Familia", "Representante", "Email", "Teléfono", "Estado",
        "Nº Miembros", "Miembros",
        "Preferencia alojamiento 1", "Tipo alojamiento 1",
        "Preferencia alojamiento 2", "Tipo alojamiento 2",
        "Preferencia alojamiento 3", "Tipo alojamiento 3",
        "Necesidades especiales", "Preferencia compañeros", "Tiene mascota", "Notas",
        "Base (€)", "Extras (€)", "Total (€)", "Pagado (€)", "Pendiente (€)",
        "Fecha inscripción"
    };
    foreach (var extra in allExtras)
    {
        headers.Add(extra.Name);
        if (extra.RequiresUserInput)
            headers.Add($"{extra.Name} - Detalle");
    }
    csv.AppendLine(string.Join(",", headers.Select(EscapeCsvValue)));

    // Data rows
    foreach (var r in registrations)
    {
        var amountPaid = r.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);
        var amountRemaining = r.TotalAmount - amountPaid;

        var prefs = r.AccommodationPreferences.OrderBy(p => p.PreferenceOrder).ToList();
        var pref1 = prefs.FirstOrDefault(p => p.PreferenceOrder == 1);
        var pref2 = prefs.FirstOrDefault(p => p.PreferenceOrder == 2);
        var pref3 = prefs.FirstOrDefault(p => p.PreferenceOrder == 3);

        var members = r.Members.Select(m =>
            $"{m.FamilyMember.FirstName} {m.FamilyMember.LastName} " +
            $"({MapAgeCategoryEs(m.AgeCategory)}, {MapAttendancePeriodEs(m.AttendancePeriod)})"
        );

        var row = new List<string>
        {
            r.Id.ToString(),
            r.FamilyUnit.Name,
            $"{r.RegisteredByUser.FirstName} {r.RegisteredByUser.LastName}",
            r.RegisteredByUser.Email,
            r.RegisteredByUser.Phone ?? "",
            MapStatusEs(r.Status),
            r.Members.Count.ToString(),
            string.Join("; ", members),
            pref1?.CampEditionAccommodation.Name ?? "",
            pref1 is not null ? MapAccommodationTypeEs(pref1.CampEditionAccommodation.AccommodationType) : "",
            pref2?.CampEditionAccommodation.Name ?? "",
            pref2 is not null ? MapAccommodationTypeEs(pref2.CampEditionAccommodation.AccommodationType) : "",
            pref3?.CampEditionAccommodation.Name ?? "",
            pref3 is not null ? MapAccommodationTypeEs(pref3.CampEditionAccommodation.AccommodationType) : "",
            r.SpecialNeeds ?? "",
            r.CampatesPreference ?? "",
            r.HasPet ? "Sí" : "No",
            r.Notes ?? "",
            r.BaseTotalAmount.ToString("F2"),
            r.ExtrasAmount.ToString("F2"),
            r.TotalAmount.ToString("F2"),
            amountPaid.ToString("F2"),
            amountRemaining.ToString("F2"),
            r.CreatedAt.ToString("dd/MM/yyyy")
        };

        foreach (var extra in allExtras)
        {
            var selected = r.Extras.FirstOrDefault(e => e.CampEditionExtraId == extra.Id);
            row.Add((selected?.Quantity ?? 0).ToString());
            if (extra.RequiresUserInput)
                row.Add(selected?.UserInput ?? "");
        }

        csv.AppendLine(string.Join(",", row.Select(EscapeCsvValue)));
    }

    // 5. Build filename: "inscripciones-{camp-name}-{year}-{date}.csv"
    var campSlug = Regex.Replace(
        edition.Camp.Name.ToLower().Normalize(NormalizationForm.FormD), @"[^a-z0-9]+", "-").Trim('-');
    var fileName = $"inscripciones-{campSlug}-{edition.Year}-{DateTime.UtcNow:yyyy-MM-dd}.csv";

    // 6. Encode as UTF-8 (the BOM char was already written as string above)
    var content = Encoding.UTF8.GetBytes(csv.ToString());
    return (content, fileName);
}
```

**Private helper methods** (add to `RegistrationsService`):

```csharp
private static string EscapeCsvValue(string value)
{
    // CSV injection protection: prefix dangerous starters with a space
    if (value.Length > 0 && "=+-@\t\r".Contains(value[0]))
        value = " " + value;

    // Wrap in quotes if contains comma, quote, or newline
    if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        value = $"\"{value.Replace("\"", "\"\"")}\"";

    return value;
}

private static string MapStatusEs(RegistrationStatus status) => status switch
{
    RegistrationStatus.Pending => "Pendiente",
    RegistrationStatus.Confirmed => "Confirmada",
    RegistrationStatus.Cancelled => "Cancelada",
    RegistrationStatus.Draft => "Borrador",
    _ => status.ToString()
};

private static string MapAccommodationTypeEs(AccommodationType type) => type switch
{
    AccommodationType.Lodge => "Albergue",
    AccommodationType.Tent => "Tienda",
    AccommodationType.Caravan => "Caravana",
    AccommodationType.Bungalow => "Bungalow",
    AccommodationType.Motorhome => "Autocaravana",
    _ => type.ToString()
};

private static string MapAgeCategoryEs(AgeCategory category) => category switch
{
    AgeCategory.Adult => "Adulto",
    AgeCategory.Child => "Niño",
    AgeCategory.Baby => "Bebé",
    _ => category.ToString()
};

private static string MapAttendancePeriodEs(AttendancePeriod period) => period switch
{
    AttendancePeriod.Complete => "Completo",
    AttendancePeriod.FirstWeek => "1ª Semana",
    AttendancePeriod.SecondWeek => "2ª Semana",
    AttendancePeriod.WeekendVisit => "Visita fin de semana",
    _ => period.ToString()
};
```

**Required `using` statements to add** (if not already present):

```csharp
using System.Text;
using System.Text.RegularExpressions;
```

**Note**: `MapAgeCategory` and `MapAttendancePeriod` private helpers already exist as `MapAgeCategory` and `MapAttendancePeriod` in `RegistrationsService`. Rename the new ones to `MapAgeCategoryEs` / `MapAttendancePeriodEs` to avoid naming conflicts (or reuse the existing ones if they return Spanish).

Looking at the existing code, `MapAgeCategory` already returns Spanish strings. Reuse `MapAgeCategory` and `MapAttendancePeriod` instead of adding duplicates.

---

### Step 5: Update `GetAdminRegistrations` endpoint handler

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs`

#### 5a. Update `GetAdminRegistrations` handler signature

```csharp
// BEFORE
private static async Task<IResult> GetAdminRegistrations(
    Guid campEditionId,
    RegistrationsService service,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? search = null,
    [FromQuery] string? status = null,
    CancellationToken ct = default)

// AFTER
private static async Task<IResult> GetAdminRegistrations(
    Guid campEditionId,
    RegistrationsService service,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? search = null,
    [FromQuery] string? status = null,
    [FromQuery] string[]? accommodationTypes = null,
    [FromQuery] Guid[]? extraIds = null,
    CancellationToken ct = default)
```

#### 5b. Update the call to `GetAdminListAsync`

```csharp
// Parse accommodation type strings → AccommodationType enum values (ignore unknown values)
var parsedAccommodationTypes = accommodationTypes?
    .Select(t => Enum.TryParse<AccommodationType>(t, true, out var parsed) ? parsed : (AccommodationType?)null)
    .Where(t => t.HasValue)
    .Select(t => t!.Value)
    .Distinct()
    .ToList();

var result = await service.GetAdminListAsync(
    campEditionId, page, pageSize, search, status,
    parsedAccommodationTypes?.Count > 0 ? parsedAccommodationTypes : null,
    extraIds?.Distinct().ToList(),
    ct);
```

#### 5c. Add the export endpoint handler and registration

In the `MapRegistrationsEndpoints` method, add to the `adminListGroup`:

```csharp
adminListGroup.MapGet("/export/csv", ExportRegistrationsToCsv)
    .WithName("ExportRegistrationsToCsv")
    .WithSummary("Export registrations for a camp edition as CSV (Admin/Board only)")
    .Produces(200)
    .Produces(401).Produces(403).Produces(404);
```

Add the handler method:

```csharp
private static async Task<IResult> ExportRegistrationsToCsv(
    Guid campEditionId,
    RegistrationsService service,
    [FromQuery] string? search = null,
    [FromQuery] string? status = null,
    [FromQuery] string[]? accommodationTypes = null,
    [FromQuery] Guid[]? extraIds = null,
    CancellationToken ct = default)
{
    try
    {
        var parsedAccommodationTypes = accommodationTypes?
            .Select(t => Enum.TryParse<AccommodationType>(t, true, out var parsed) ? parsed : (AccommodationType?)null)
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .Distinct()
            .ToList();

        var (content, fileName) = await service.ExportToCsvAsync(
            campEditionId, search, status,
            parsedAccommodationTypes?.Count > 0 ? parsedAccommodationTypes : null,
            extraIds?.Distinct().ToList(),
            ct);

        return Results.File(
            content,
            contentType: "text/csv; charset=utf-8",
            fileDownloadName: fileName);
    }
    catch (NotFoundException ex)
    {
        return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
}
```

**Note on route ordering**: `adminListGroup.MapGet("/export/csv", ...)` must be registered **before** any route with `{id:guid}` pattern on the same group to avoid ambiguity. In this case the group base is `/api/camp-editions/{campEditionId:guid}/registrations` and we're adding `/export/csv` — no conflict with any existing routes.

---

### Step 6: Register `ICampEditionExtrasRepository` in `RegistrationsService` DI

**File**: `src/Abuvi.API/Program.cs`

Check if `ICampEditionExtrasRepository` is already registered (it should be, since the Camps feature uses it). Search for its registration:

```bash
grep -n "ICampEditionExtrasRepository" src/Abuvi.API/Program.cs
```

If it is already registered, no change needed. If not, add:

```csharp
builder.Services.AddScoped<ICampEditionExtrasRepository, CampEditionExtrasRepository>();
```

---

### Step 7: Write Unit Tests

**File**: `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsExportServiceTests.cs`

```csharp
using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Services;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Memberships;
using Abuvi.API.Features.Payments;
using Abuvi.API.Features.Registrations;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Text;

namespace Abuvi.Tests.Unit.Features.Registrations;

public class RegistrationsExportServiceTests
{
    private readonly IRegistrationsRepository _repo;
    private readonly ICampEditionExtrasRepository _extrasDefinitionRepo;
    private readonly ICampEditionsRepository _editionsRepo;
    private readonly RegistrationsService _sut;

    private static readonly Guid CampEditionId = Guid.NewGuid();

    public RegistrationsExportServiceTests()
    {
        _repo = Substitute.For<IRegistrationsRepository>();
        _extrasDefinitionRepo = Substitute.For<ICampEditionExtrasRepository>();
        var extrasRepo = Substitute.For<IRegistrationExtrasRepository>();
        var accommodationPrefsRepo = Substitute.For<IRegistrationAccommodationPreferencesRepository>();
        var familyUnitsRepo = Substitute.For<IFamilyUnitsRepository>();
        _editionsRepo = Substitute.For<ICampEditionsRepository>();
        var accommodationsRepo = Substitute.For<ICampEditionAccommodationsRepository>();
        var emailService = Substitute.For<IEmailService>();
        var paymentsService = Substitute.For<IPaymentsService>();
        var logger = Substitute.For<ILogger<RegistrationsService>>();
        var settingsRepo = Substitute.For<IAssociationSettingsRepository>();
        var membershipsRepo = Substitute.For<IMembershipsRepository>();
        membershipsRepo.HasPaidCurrentYearFeeForFamilyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var pricingService = new RegistrationPricingService(settingsRepo);

        _sut = new RegistrationsService(
            _repo, extrasRepo, accommodationPrefsRepo, familyUnitsRepo,
            _editionsRepo, accommodationsRepo, _extrasDefinitionRepo,
            pricingService, emailService, paymentsService, membershipsRepo, logger);
    }

    // ── ExportToCsvAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task ExportToCsvAsync_WhenEditionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>())
            .Returns((CampEdition?)null);

        // Act
        var act = () => _sut.ExportToCsvAsync(
            CampEditionId, null, null, null, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExportToCsvAsync_WhenNoRegistrations_ReturnsHeaderOnlyFile()
    {
        // Arrange
        SetupEdition();
        _extrasDefinitionRepo
            .GetByCampEditionAsync(CampEditionId, activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(new List<CampEditionExtra>());
        _repo.GetAllForExportAsync(
            CampEditionId, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<Registration>());

        // Act
        var (content, fileName) = await _sut.ExportToCsvAsync(
            CampEditionId, null, null, null, null, CancellationToken.None);

        // Assert
        var text = Encoding.UTF8.GetString(content).TrimStart('﻿');
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(1, "only the header row should be present");
        lines[0].Should().Contain("ID Inscripción");
        lines[0].Should().Contain("Familia");
        fileName.Should().StartWith("inscripciones-").And.EndWith(".csv");
    }

    [Fact]
    public async Task ExportToCsvAsync_WithRegistrations_IncludesDynamicExtraColumns()
    {
        // Arrange
        SetupEdition();
        var extra = BuildExtra("Kayak", requiresUserInput: false);
        var extraWithInput = BuildExtra("Camiseta", requiresUserInput: true);
        _extrasDefinitionRepo
            .GetByCampEditionAsync(CampEditionId, activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(new List<CampEditionExtra> { extra, extraWithInput });

        var registration = BuildRegistration();
        registration.Extras = new List<RegistrationExtra>
        {
            new() { CampEditionExtraId = extra.Id, Quantity = 2, CampEditionExtra = extra },
            new() { CampEditionExtraId = extraWithInput.Id, Quantity = 1,
                    UserInput = "M", CampEditionExtra = extraWithInput }
        };
        _repo.GetAllForExportAsync(
                CampEditionId, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<Registration> { registration });

        // Act
        var (content, _) = await _sut.ExportToCsvAsync(
            CampEditionId, null, null, null, null, CancellationToken.None);

        // Assert
        var text = Encoding.UTF8.GetString(content).TrimStart('﻿');
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2, "header + 1 data row");
        lines[0].Should().Contain("Kayak");
        lines[0].Should().Contain("Camiseta");
        lines[0].Should().Contain("Camiseta - Detalle");
        lines[1].Should().Contain(",2,");   // Kayak quantity
        lines[1].Should().Contain(",1,");   // Camiseta quantity
        lines[1].Should().Contain(",M,");   // Camiseta user input
    }

    [Fact]
    public async Task ExportToCsvAsync_WhenRegistrationHasDangerousValue_EscapesCsvInjection()
    {
        // Arrange
        SetupEdition();
        _extrasDefinitionRepo
            .GetByCampEditionAsync(CampEditionId, activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(new List<CampEditionExtra>());

        var registration = BuildRegistration();
        registration.Notes = "=HYPERLINK(\"evil.com\",\"click\")";
        _repo.GetAllForExportAsync(
                CampEditionId, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<Registration> { registration });

        // Act
        var (content, _) = await _sut.ExportToCsvAsync(
            CampEditionId, null, null, null, null, CancellationToken.None);

        // Assert
        var text = Encoding.UTF8.GetString(content).TrimStart('﻿');
        text.Should().NotContain("=HYPERLINK");    // raw = is dangerous
        text.Should().Contain(" =HYPERLINK");      // prefixed with space
    }

    [Fact]
    public async Task ExportToCsvAsync_WithExtrasFilter_PassesFilterToRepository()
    {
        // Arrange
        SetupEdition();
        var extraId = Guid.NewGuid();
        _extrasDefinitionRepo
            .GetByCampEditionAsync(CampEditionId, activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(new List<CampEditionExtra>());
        _repo.GetAllForExportAsync(
            CampEditionId, null, null, null, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Registration>());

        // Act
        await _sut.ExportToCsvAsync(
            CampEditionId, null, null, null, new[] { extraId }, CancellationToken.None);

        // Assert
        await _repo.Received(1).GetAllForExportAsync(
            CampEditionId, null, null, null,
            Arg.Is<IReadOnlyList<Guid>>(ids => ids.Contains(extraId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportToCsvAsync_WithAccommodationTypeFilter_PassesFilterToRepository()
    {
        // Arrange
        SetupEdition();
        _extrasDefinitionRepo
            .GetByCampEditionAsync(CampEditionId, activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(new List<CampEditionExtra>());
        _repo.GetAllForExportAsync(
            CampEditionId, null, null, Arg.Any<IReadOnlyList<AccommodationType>>(), null, Arg.Any<CancellationToken>())
            .Returns(new List<Registration>());

        // Act
        await _sut.ExportToCsvAsync(
            CampEditionId, null, null,
            new[] { AccommodationType.Lodge }, null, CancellationToken.None);

        // Assert
        await _repo.Received(1).GetAllForExportAsync(
            CampEditionId, null, null,
            Arg.Is<IReadOnlyList<AccommodationType>>(types => types.Contains(AccommodationType.Lodge)),
            null,
            Arg.Any<CancellationToken>());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetupEdition()
    {
        var camp = new Camp { Id = Guid.NewGuid(), Name = "Campamento Abuvi" };
        var edition = new CampEdition
        {
            Id = CampEditionId,
            Year = 2026,
            Camp = camp,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 14)
        };
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
    }

    private static CampEditionExtra BuildExtra(string name, bool requiresUserInput) =>
        new() { Id = Guid.NewGuid(), Name = name, RequiresUserInput = requiresUserInput, SortOrder = 0 };

    private static Registration BuildRegistration() => new()
    {
        Id = Guid.NewGuid(),
        FamilyUnit = new FamilyUnit { Id = Guid.NewGuid(), Name = "Familia Test" },
        RegisteredByUser = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Juan",
            LastName = "García",
            Email = "juan@example.com",
            Phone = "600000000"
        },
        Status = RegistrationStatus.Confirmed,
        BaseTotalAmount = 500m,
        ExtrasAmount = 50m,
        TotalAmount = 550m,
        Members = [],
        Extras = [],
        AccommodationPreferences = [],
        Payments = [],
        CreatedAt = DateTime.UtcNow
    };
}
```

---

### Step 8: Update Technical Documentation

**Action**: Review and update relevant documentation.

**Implementation Steps**:
1. **Identify affected docs**: The only doc file likely to reference the admin registration list API is `ai-specs/specs/api-spec.yml` (if it exists). Check with `ls ai-specs/specs/`.
2. **If `api-spec.yml` exists**: Add the new endpoint `GET /api/camp-editions/{campEditionId}/registrations/export/csv` and the new query params `accommodationTypes` and `extraIds` on the list endpoint.
3. **Verify auto-generated OpenAPI**: The export endpoint uses `Results.File()` which returns `application/octet-stream` by default. Update the `.Produces(200)` declaration to `.Produces<FileResult>(200)` or add `ProducesResponseType` attributes if needed for Swagger accuracy.
4. Update this plan file to mark all steps complete when done.

---

## Implementation Order

1. **Step 0** — Create feature branch
2. **Step 1** — Update `IRegistrationsRepository.GetAdminPagedAsync` interface + implementation
3. **Step 2** — Add `GetAllForExportAsync` to repository interface + implementation
4. **Step 3** — Inject `ICampEditionExtrasRepository` into `RegistrationsService` + update `GetAdminListAsync`
5. **Step 4** — Add `ExportToCsvAsync` to `RegistrationsService`
6. **Step 5** — Update `GetAdminRegistrations` endpoint handler + add export endpoint
7. **Step 6** — Verify DI registration in `Program.cs`
8. **Step 7** — Write unit tests (`RegistrationsExportServiceTests.cs`)
9. **Step 8** — Update documentation

---

## Testing Checklist

### Unit tests (`RegistrationsExportServiceTests.cs`)

- [ ] `ExportToCsvAsync_WhenEditionNotFound_ThrowsNotFoundException`
- [ ] `ExportToCsvAsync_WhenNoRegistrations_ReturnsHeaderOnlyFile`
- [ ] `ExportToCsvAsync_WithRegistrations_IncludesDynamicExtraColumns`
- [ ] `ExportToCsvAsync_WhenRegistrationHasDangerousValue_EscapesCsvInjection`
- [ ] `ExportToCsvAsync_WithExtrasFilter_PassesFilterToRepository`
- [ ] `ExportToCsvAsync_WithAccommodationTypeFilter_PassesFilterToRepository`

### Existing tests must still pass

- Run `dotnet test` to confirm no regressions in `AdminRegistrationServiceTests` after changing method signatures.
- The constructor in `AdminRegistrationServiceTests` must be updated to pass `extrasDefinitionRepo` (new constructor arg). This is a required change to the existing test file.

### Manual verification

- [ ] `GET /api/camp-editions/{id}/registrations?accommodationTypes=Lodge&accommodationTypes=Tent` returns filtered results
- [ ] `GET /api/camp-editions/{id}/registrations?extraIds={guid}` returns filtered results
- [ ] `GET /api/camp-editions/{id}/registrations/export/csv` triggers file download in Swagger
- [ ] Downloaded CSV opens correctly in Excel (no garbled characters, no extra import wizard)
- [ ] CSV has correct dynamic columns for extras
- [ ] CSV injection test: a notes value starting with `=` is prefixed with a space in the output

---

## Error Response Format

```json
// 404 — edition not found
{
  "success": false,
  "data": null,
  "error": { "message": "No se encontró Edición de Campamento con ID '...'", "code": "NOT_FOUND" }
}
```

The export endpoint returns `text/csv` on success (200) or `ApiResponse<object>` JSON on 404. No 400 validation errors (all params are optional).

---

## Dependencies

No new NuGet packages required. Uses only BCL types:
- `System.Text.StringBuilder`
- `System.Text.Encoding`
- `System.Text.RegularExpressions.Regex`
- `System.Text.Unicode` (via `NormalizationForm`)

---

## Notes

### Key implementation notes

1. **`GetAdminPagedAsync` uses a manual projection join** (not entity navigation). The new accommodation-type filter must cross-join `db.RegistrationAccommodationPreferences` + `db.CampEditionAccommodations` via `EXISTS` subqueries. EF Core translates `db.SomeSet.Any(...)` inside a LINQ `Where` to SQL `EXISTS (SELECT 1 FROM ... WHERE ...)`.

2. **`GetAllForExportAsync` uses navigation properties** (`.Include()`), so the accommodation-type filter can use `r.AccommodationPreferences.Any(p => ...)` directly — EF Core will translate this to a JOIN/EXISTS at query build time.

3. **Constructor change breaks the existing test** `AdminRegistrationServiceTests` — update its `_sut` construction to pass an additional `Substitute.For<ICampEditionExtrasRepository>()`.

4. **File name normalization**: Use `Regex.Replace` + `NormalizationForm.FormD` to strip diacritics from the camp name for the filename slug. Do NOT use `string.Normalize()` alone — it does not remove the diacritic characters themselves.

5. **UTF-8 BOM**: `'﻿'` prepended as a `char` at the start of the `StringBuilder` string. When `Encoding.UTF8.GetBytes()` encodes it, the BOM is written correctly. Alternatively use `new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)`.

6. **`Results.File(byte[], contentType, fileDownloadName)`**: This is the correct Minimal API helper for binary file responses. It sets `Content-Disposition: attachment; filename="..."` automatically.

7. **No pagination on export**: The export endpoint intentionally fetches all records matching the filters. This is acceptable for camp editions (≤ 500 registrations per edition, with a perf target of < 5s).

### Business rules

- Export is restricted to Admin/Board roles — enforced by the `adminListGroup.RequireAuthorization(...)` applied at group level.
- Extras filter is OR-logic within the list: a registration is included if it selected **any** of the provided extras.
- Accommodation type filter is also OR-logic: a registration is included if **any** of its preferences matches one of the given types.
- Both filters are AND-composed with each other and with status/search.

### RGPD

The CSV contains personal data (name, email, phone, special needs). Access is restricted to Admin/Board roles. No additional audit log required for this MVP.
