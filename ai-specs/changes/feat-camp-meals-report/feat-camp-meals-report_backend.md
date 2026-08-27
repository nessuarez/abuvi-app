# Backend Implementation Plan: feat-camp-meals-report — Camp Meals ("Comensales") Report

## Overview

Adds a new sub-feature inside `src/Abuvi.API/Features/Camps/` that computes, per `CampEdition`,
a day × meal × age-category diner report from existing registrations, supports manual
add/remove adjustments, and exports the result as an `.xlsx` file.

**No Excel library exists in this codebase yet.** This plan adds **ClosedXML** (MIT-licensed,
no Office/COM dependency, works cross-platform under .NET — the standard choice for
server-side `.xlsx` generation in .NET) as a new NuGet dependency.

---

## Architecture Context

**Feature slice:** `src/Abuvi.API/Features/Camps/` (new files, alongside the existing
`CampEditionExtras*` files — same precedent, same folder).

Two existing pieces are reused, not duplicated:

- `RegistrationPricingService.GetPeriodDays(AttendancePeriod, CampEdition, DateOnly? visitStart, DateOnly? visitEnd)`
  — resolves the `(DateOnly Start, DateOnly End)` a `RegistrationMember` is present, per
  `src/Abuvi.API/Features/Registrations/RegistrationPricingService.cs:120-148`. **Check its
  current accessibility** — if it's `private`, promote it to `public` (or `internal` if the
  Camps feature already references the Registrations assembly-internally) so this feature can
  call it without copying the period-resolution logic.
- `RegistrationMember.AgeCategory` (already snapshotted at registration time) — used directly
  for the baseline breakdown, no recomputation needed for already-registered members. Only
  manually added extra diners need an explicit `AgeCategory` chosen by the board member.

### Files to create

| File | Purpose |
|---|---|
| `Features/Camps/CampMealsModels.cs` | `MealType` enum, `CampEditionExtraDiner` / `CampEditionMealExclusion` entities, all request/response DTOs |
| `Features/Camps/ICampMealsRepository.cs` | Repository interface |
| `Features/Camps/CampMealsRepository.cs` | EF Core implementation |
| `Features/Camps/CampMealsService.cs` | Report computation + CRUD orchestration |
| `Features/Camps/CampMealsExcelExporter.cs` | ClosedXML workbook builder |
| `Features/Camps/CampMealsValidators.cs` | FluentValidation validators |
| `Features/Camps/CampMealsEndpoints.cs` | Minimal API endpoints (kept in its own file — see note below) |
| `Data/Configurations/CampEditionExtraDinerConfiguration.cs` | EF Core Fluent API config |
| `Data/Configurations/CampEditionMealExclusionConfiguration.cs` | EF Core Fluent API config |
| `Abuvi.Tests/Unit/Features/Camps/CampMealsServiceTests.cs` | Unit tests |
| `Abuvi.Tests/Unit/Features/Camps/CampMealsValidatorTests.cs` | Validator tests |
| `Abuvi.Tests/Unit/Features/Camps/CampMealsExcelExporterTests.cs` | Exporter structural tests |
| `Abuvi.Tests/Integration/Features/Camps/CampMealsEndpointsTests.cs` | Integration tests |

### Files to modify

| File | Change |
|---|---|
| `Abuvi.API.csproj` | Add `<PackageReference Include="ClosedXML" Version="0.104.*" />` (check latest stable at implementation time) |
| `Data/AbuviDbContext.cs` | Add `DbSet<CampEditionExtraDiner> CampEditionExtraDiners` and `DbSet<CampEditionMealExclusion> CampEditionMealExclusions` |
| `Program.cs` | Register repository + service; call `app.MapCampMealsEndpoints();` |
| `src/Abuvi.API/Features/Registrations/RegistrationPricingService.cs` | Only if `GetPeriodDays` is currently `private` — widen visibility |

### Why a separate `CampMealsEndpoints.cs` instead of appending to `CampsEndpoints.cs`

The Extras feature appended its 7 endpoints to the existing `CampsEndpoints.cs`. This feature
adds 9 endpoints plus non-trivial handler bodies (report computation, file export, attendee
lookup). Keeping them in their own file (still mapped from `Program.cs`, same pattern as
`CampsEndpoints.MapCampsEndpoints()`) keeps both files navigable. This is a deliberate,
documented deviation from the Extras precedent — flag it in review if the team prefers strict
single-file-per-feature instead.

---

## Domain Models

```csharp
namespace Abuvi.API.Features.Camps;

public enum MealType
{
    Breakfast, // Desayuno
    Lunch,     // Comida
    Snack,     // Merienda
    Dinner     // Cena
}

public class CampEditionExtraDiner
{
    public Guid Id { get; set; }
    public Guid CampEditionId { get; set; }
    public DateOnly Date { get; set; }
    public MealType MealType { get; set; }
    public AgeCategory AgeCategory { get; set; }
    public int Count { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }

    public CampEdition CampEdition { get; set; } = null!;
}

public class CampEditionMealExclusion
{
    public Guid Id { get; set; }
    public Guid CampEditionId { get; set; }
    public Guid RegistrationMemberId { get; set; }
    public DateOnly Date { get; set; }
    public MealType MealType { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }

    public CampEdition CampEdition { get; set; } = null!;
    public RegistrationMember RegistrationMember { get; set; } = null!;
}
```

`AgeCategory` is the existing enum from `Features/Registrations/RegistrationsModels.cs` —
reference it via `using Abuvi.API.Features.Registrations;` in `CampMealsModels.cs`.

### EF Core configuration

```csharp
// Data/Configurations/CampEditionExtraDinerConfiguration.cs
public class CampEditionExtraDinerConfiguration : IEntityTypeConfiguration<CampEditionExtraDiner>
{
    public void Configure(EntityTypeBuilder<CampEditionExtraDiner> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.MealType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.AgeCategory).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.CampEdition)
            .WithMany()
            .HasForeignKey(x => x.CampEditionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.CampEditionId, x.Date, x.MealType });
    }
}

// Data/Configurations/CampEditionMealExclusionConfiguration.cs
public class CampEditionMealExclusionConfiguration : IEntityTypeConfiguration<CampEditionMealExclusion>
{
    public void Configure(EntityTypeBuilder<CampEditionMealExclusion> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.MealType).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.CampEdition)
            .WithMany()
            .HasForeignKey(x => x.CampEditionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RegistrationMember)
            .WithMany()
            .HasForeignKey(x => x.RegistrationMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        // A member can only be excluded once from the same day+meal.
        builder.HasIndex(x => new { x.RegistrationMemberId, x.Date, x.MealType }).IsUnique();
    }
}
```

Run `dotnet ef migrations add AddCampMealsReporting --project src/Abuvi.API` after both entities
and DbSets are wired up; review the generated migration before applying.

---

## DTOs

```csharp
public record CampMealsReportResponse(
    Guid CampEditionId,
    List<CampMealsDayResponse> Days
);

public record CampMealsDayResponse(
    DateOnly Date,
    List<CampMealsMealResponse> Meals
);

public record CampMealsMealResponse(
    MealType MealType,
    List<CampMealsAgeCategoryCount> Counts,
    int Total
);

public record CampMealsAgeCategoryCount(
    AgeCategory AgeCategory,
    int BaseCount,   // registered attendees present that day, minus exclusions for that meal
    int ExtraCount,  // manually added diners for that day/meal/age category
    int Total        // BaseCount + ExtraCount
);

public record CreateExtraDinerRequest(
    DateOnly Date,
    MealType MealType,
    AgeCategory AgeCategory,
    int Count,
    string? Notes
);

public record ExtraDinerResponse(
    Guid Id,
    Guid CampEditionId,
    DateOnly Date,
    MealType MealType,
    AgeCategory AgeCategory,
    int Count,
    string? Notes,
    DateTime CreatedAt,
    string CreatedByName
);

public record CreateMealExclusionRequest(
    Guid RegistrationMemberId,
    DateOnly Date,
    MealType MealType,
    string? Reason
);

public record MealExclusionResponse(
    Guid Id,
    Guid CampEditionId,
    Guid RegistrationMemberId,
    string MemberFullName,
    DateOnly Date,
    MealType MealType,
    string? Reason,
    DateTime CreatedAt,
    string CreatedByName
);

public record MealAttendeeResponse(
    Guid RegistrationMemberId,
    string FullName,
    AgeCategory AgeCategory,
    bool IsExcluded,
    Guid? ExclusionId
);
```

---

## Repository

```csharp
public interface ICampMealsRepository
{
    Task<CampEdition?> GetEditionAsync(Guid campEditionId, CancellationToken ct = default);

    /// Active (non-cancelled) registrations with members, for baseline attendance computation.
    Task<List<Registration>> GetActiveRegistrationsWithMembersAsync(Guid campEditionId, CancellationToken ct = default);

    Task<List<CampEditionExtraDiner>> GetExtraDinersAsync(Guid campEditionId, CancellationToken ct = default);
    Task<CampEditionExtraDiner?> GetExtraDinerByIdAsync(Guid id, CancellationToken ct = default);
    Task AddExtraDinerAsync(CampEditionExtraDiner entity, CancellationToken ct = default);
    Task DeleteExtraDinerAsync(Guid id, CancellationToken ct = default);

    Task<List<CampEditionMealExclusion>> GetExclusionsAsync(Guid campEditionId, CancellationToken ct = default);
    Task<CampEditionMealExclusion?> GetExclusionByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExclusionExistsAsync(Guid registrationMemberId, DateOnly date, MealType mealType, CancellationToken ct = default);
    Task AddExclusionAsync(CampEditionMealExclusion entity, CancellationToken ct = default);
    Task DeleteExclusionAsync(Guid id, CancellationToken ct = default);

    Task<RegistrationMember?> GetRegistrationMemberAsync(Guid registrationMemberId, CancellationToken ct = default);
}
```

Implementation notes:

- `GetActiveRegistrationsWithMembersAsync` filters `Status != RegistrationStatus.Cancelled` and
  `.Include(r => r.Members)`, `AsNoTracking()`, matching the convention already used in
  `AccommodationAssignmentsRepository.cs:18` for excluding cancelled registrations from headcounts.
- `GetRegistrationMemberAsync` is needed by the exclusion validator to confirm the member belongs
  to a non-cancelled registration of this edition, and to resolve `AgeCategory`/full name for the
  attendees list.

---

## Service: `CampMealsService`

### Report computation

```csharp
public async Task<CampMealsReportResponse> GetReportAsync(Guid campEditionId, CancellationToken ct = default)
{
    var edition = await repository.GetEditionAsync(campEditionId, ct)
        ?? throw new InvalidOperationException("La edición de campamento no fue encontrada");

    var registrations = await repository.GetActiveRegistrationsWithMembersAsync(campEditionId, ct);
    var extraDiners = await repository.GetExtraDinersAsync(campEditionId, ct);
    var exclusions = await repository.GetExclusionsAsync(campEditionId, ct);

    var exclusionLookup = exclusions
        .ToLookup(x => (x.RegistrationMemberId, x.Date, x.MealType));

    var days = new List<CampMealsDayResponse>();
    for (var date = DateOnly.FromDateTime(edition.StartDate); date <= DateOnly.FromDateTime(edition.EndDate); date = date.AddDays(1))
    {
        var meals = new List<CampMealsMealResponse>();
        foreach (var mealType in Enum.GetValues<MealType>())
        {
            var baseCounts = new Dictionary<AgeCategory, int>();

            foreach (var registration in registrations)
            foreach (var member in registration.Members)
            {
                var (start, end) = RegistrationPricingService.GetPeriodDays(
                    member.AttendancePeriod, edition, member.VisitStartDate, member.VisitEndDate);

                if (date < start || date > end) continue;
                if (exclusionLookup.Contains((member.Id, date, mealType))) continue;

                baseCounts[member.AgeCategory] = baseCounts.GetValueOrDefault(member.AgeCategory) + 1;
            }

            var extraCounts = extraDiners
                .Where(x => x.Date == date && x.MealType == mealType)
                .GroupBy(x => x.AgeCategory)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));

            var counts = Enum.GetValues<AgeCategory>()
                .Select(category =>
                {
                    var baseCount = baseCounts.GetValueOrDefault(category);
                    var extraCount = extraCounts.GetValueOrDefault(category);
                    return new CampMealsAgeCategoryCount(category, baseCount, extraCount, baseCount + extraCount);
                })
                .ToList();

            meals.Add(new CampMealsMealResponse(mealType, counts, counts.Sum(c => c.Total)));
        }

        days.Add(new CampMealsDayResponse(date, meals));
    }

    return new CampMealsReportResponse(campEditionId, days);
}
```

Implementation notes:

- `GetPeriodDays` is currently a `RegistrationPricingService` instance/static method depending
  on its actual signature — check whether it needs the service injected or can be called
  statically; adjust the snippet accordingly during implementation (this plan assumes it's safe
  to call without side effects, since it's a pure date calculation).
- This is O(registrations × members × days × 4 meals) — for ABUVI's camp sizes (low hundreds of
  people, 1-2 weeks) this is a few thousand iterations per request; no caching needed for a v1.
  If this ever becomes a real DataTable-style admin page hit repeatedly, consider caching the
  per-member day range once per request instead of recomputing per meal (4× redundant calls
  currently) — flagged as a possible optimization, not required for correctness.

### Attendees for a given day/meal (populates the "who can I exclude" picker)

```csharp
public async Task<List<MealAttendeeResponse>> GetAttendeesAsync(
    Guid campEditionId, DateOnly date, MealType mealType, CancellationToken ct = default)
{
    var edition = await repository.GetEditionAsync(campEditionId, ct)
        ?? throw new InvalidOperationException("La edición de campamento no fue encontrada");

    var registrations = await repository.GetActiveRegistrationsWithMembersAsync(campEditionId, ct);
    var exclusions = await repository.GetExclusionsAsync(campEditionId, ct);
    var exclusionLookup = exclusions.ToDictionary(x => (x.RegistrationMemberId, x.Date, x.MealType), x => x.Id);

    return registrations
        .SelectMany(r => r.Members)
        .Where(member =>
        {
            var (start, end) = RegistrationPricingService.GetPeriodDays(
                member.AttendancePeriod, edition, member.VisitStartDate, member.VisitEndDate);
            return date >= start && date <= end;
        })
        .Select(member =>
        {
            exclusionLookup.TryGetValue((member.Id, date, mealType), out var exclusionId);
            return new MealAttendeeResponse(
                member.Id,
                $"{member.FamilyMember.FirstName} {member.FamilyMember.LastName}",
                member.AgeCategory,
                exclusionId != Guid.Empty,
                exclusionId == Guid.Empty ? null : exclusionId);
        })
        .OrderBy(a => a.FullName)
        .ToList();
}
```

Note: this requires `RegistrationMember` to have (or load) a navigation to `FamilyMember` for the
name — check the existing `Registration`/`RegistrationMember` includes elsewhere in the codebase
(e.g. `RegistrationsService.cs`) for the established way to get a member's display name; reuse
that instead of re-deriving it here.

### Extra diners CRUD

```csharp
public async Task<ExtraDinerResponse> AddExtraDinerAsync(
    Guid campEditionId, CreateExtraDinerRequest request, Guid currentUserId, CancellationToken ct = default)
{
    var edition = await repository.GetEditionAsync(campEditionId, ct)
        ?? throw new InvalidOperationException("La edición de campamento no fue encontrada");

    var editionStart = DateOnly.FromDateTime(edition.StartDate);
    var editionEnd = DateOnly.FromDateTime(edition.EndDate);
    if (request.Date < editionStart || request.Date > editionEnd)
        throw new InvalidOperationException("La fecha debe estar dentro de las fechas del campamento");

    var entity = new CampEditionExtraDiner
    {
        Id = Guid.NewGuid(),
        CampEditionId = campEditionId,
        Date = request.Date,
        MealType = request.MealType,
        AgeCategory = request.AgeCategory,
        Count = request.Count,
        Notes = request.Notes,
        CreatedAt = DateTime.UtcNow,
        CreatedByUserId = currentUserId
    };

    await repository.AddExtraDinerAsync(entity, ct);
    return entity.ToResponse(createdByName: /* resolve from currentUserId */ "");
}

public async Task<bool> DeleteExtraDinerAsync(Guid id, CancellationToken ct = default)
{
    var entity = await repository.GetExtraDinerByIdAsync(id, ct);
    if (entity is null) return false;
    await repository.DeleteExtraDinerAsync(id, ct);
    return true;
}
```

The `CreatedByName` resolution (user id → display name) should reuse whatever pattern the
codebase already has for "who created this" fields — check `Memories` or `PhotoAlbums` features
for a precedent (they already track uploader/approver identities) rather than re-inventing a
user lookup here.

### Meal exclusions CRUD

```csharp
public async Task<MealExclusionResponse> AddExclusionAsync(
    Guid campEditionId, CreateMealExclusionRequest request, Guid currentUserId, CancellationToken ct = default)
{
    var edition = await repository.GetEditionAsync(campEditionId, ct)
        ?? throw new InvalidOperationException("La edición de campamento no fue encontrada");

    var member = await repository.GetRegistrationMemberAsync(request.RegistrationMemberId, ct)
        ?? throw new InvalidOperationException("El miembro de la inscripción no fue encontrado");

    var (start, end) = RegistrationPricingService.GetPeriodDays(
        member.AttendancePeriod, edition, member.VisitStartDate, member.VisitEndDate);

    if (request.Date < start || request.Date > end)
        throw new InvalidOperationException("La persona no está presente ese día según su inscripción");

    if (await repository.ExclusionExistsAsync(request.RegistrationMemberId, request.Date, request.MealType, ct))
        throw new InvalidOperationException("Esta persona ya está excluida de esa comida ese día");

    var entity = new CampEditionMealExclusion
    {
        Id = Guid.NewGuid(),
        CampEditionId = campEditionId,
        RegistrationMemberId = request.RegistrationMemberId,
        Date = request.Date,
        MealType = request.MealType,
        Reason = request.Reason,
        CreatedAt = DateTime.UtcNow,
        CreatedByUserId = currentUserId
    };

    await repository.AddExclusionAsync(entity, ct);
    return entity.ToResponse(memberFullName: /* resolve */ "", createdByName: /* resolve */ "");
}

public async Task<bool> DeleteExclusionAsync(Guid id, CancellationToken ct = default)
{
    var entity = await repository.GetExclusionByIdAsync(id, ct);
    if (entity is null) return false;
    await repository.DeleteExclusionAsync(id, ct);
    return true;
}
```

### Excel export

```csharp
public async Task<byte[]> ExportAsync(Guid campEditionId, CancellationToken ct = default)
{
    var report = await GetReportAsync(campEditionId, ct);
    var edition = await repository.GetEditionAsync(campEditionId, ct)
        ?? throw new InvalidOperationException("La edición de campamento no fue encontrada");

    return CampMealsExcelExporter.Build(edition, report);
}
```

`CampMealsExcelExporter.Build` (static, in its own file so it has no DI dependencies and is
trivially unit-testable):

- Sheet "Resumen".
- Header rows: one column per (meal × age category) plus a per-meal total and a day total,
  e.g. `Desayuno / Bebés | Desayuno / Niños | Desayuno / Adultos | Desayuno / Total | Comida / ... | Total día`.
- One row per calendar day (`DateOnly` formatted `dd/MM/yyyy`), in edition date order.
- Values are plain integers — no formulas needed since totals are already computed server-side
  (keeps the workbook simple and avoids formula-recalculation edge cases in different Excel
  versions).
- Use `ClosedXML.Excel.XLWorkbook`, write to a `MemoryStream`, return `.ToArray()`.

---

## Validators

```csharp
public class CreateExtraDinerRequestValidator : AbstractValidator<CreateExtraDinerRequest>
{
    public CreateExtraDinerRequestValidator()
    {
        RuleFor(x => x.MealType).IsInEnum().WithMessage("El tipo de comida no es válido");
        RuleFor(x => x.AgeCategory).IsInEnum().WithMessage("El rango de edad no es válido");
        RuleFor(x => x.Count).GreaterThan(0).WithMessage("El número de personas debe ser mayor que 0");
        RuleFor(x => x.Notes).MaximumLength(500).WithMessage("Las notas no pueden superar los 500 caracteres");
    }
}

public class CreateMealExclusionRequestValidator : AbstractValidator<CreateMealExclusionRequest>
{
    public CreateMealExclusionRequestValidator()
    {
        RuleFor(x => x.RegistrationMemberId).NotEmpty().WithMessage("Debes seleccionar una persona");
        RuleFor(x => x.MealType).IsInEnum().WithMessage("El tipo de comida no es válido");
        RuleFor(x => x.Reason).MaximumLength(500).WithMessage("El motivo no puede superar los 500 caracteres");
    }
}
```

Date-range and cross-entity checks (date within edition dates, member present that day, no
duplicate exclusion) stay in the **service**, not the validator, because they need database
access — consistent with how `CampEditionExtrasService.CreateAsync` checks edition status
rather than a validator doing it (see `feat-camp-edition-extras_backend.md:190-196`).

---

## API Endpoints

All endpoints require `Board`/`Admin` — this is operational catering data, not member-facing.

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/camps/editions/{editionId}/meals/report` | Full computed report (JSON) |
| GET | `/api/camps/editions/{editionId}/meals/export` | Same report as an `.xlsx` file download |
| GET | `/api/camps/editions/{editionId}/meals/attendees?date=&mealType=` | Registered attendees present that day/meal, with exclusion state, for the UI picker |
| GET | `/api/camps/editions/{editionId}/meals/extra-diners` | List manual extra-diner entries |
| POST | `/api/camps/editions/{editionId}/meals/extra-diners` | Add a manual extra-diner entry |
| DELETE | `/api/camps/editions/meals/extra-diners/{id}` | Remove an extra-diner entry |
| GET | `/api/camps/editions/{editionId}/meals/exclusions` | List meal exclusions |
| POST | `/api/camps/editions/{editionId}/meals/exclusions` | Exclude a registered member from one day+meal |
| DELETE | `/api/camps/editions/meals/exclusions/{id}` | Remove an exclusion (re-includes the member) |

```csharp
public static class CampMealsEndpoints
{
    public static void MapCampMealsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/camps/editions/{editionId:guid}/meals")
            .WithTags("Camp Meals Report")
            .WithOpenApi()
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

        group.MapGet("/report", GetReport)
            .WithName("GetCampMealsReport")
            .Produces<ApiResponse<CampMealsReportResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/export", ExportReport)
            .WithName("ExportCampMealsReport");
            // Returns a file (Results.File), not an ApiResponse<T> envelope.

        group.MapGet("/attendees", GetAttendees)
            .WithName("GetCampMealsAttendees")
            .Produces<ApiResponse<List<MealAttendeeResponse>>>();

        group.MapGet("/extra-diners", GetExtraDiners)
            .WithName("GetCampMealsExtraDiners")
            .Produces<ApiResponse<List<ExtraDinerResponse>>>();

        group.MapPost("/extra-diners", CreateExtraDiner)
            .WithName("CreateCampMealsExtraDiner")
            .AddEndpointFilter<ValidationFilter<CreateExtraDinerRequest>>()
            .Produces<ApiResponse<ExtraDinerResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/exclusions", GetExclusions)
            .WithName("GetCampMealsExclusions")
            .Produces<ApiResponse<List<MealExclusionResponse>>>();

        group.MapPost("/exclusions", CreateExclusion)
            .WithName("CreateCampMealsExclusion")
            .AddEndpointFilter<ValidationFilter<CreateMealExclusionRequest>>()
            .Produces<ApiResponse<MealExclusionResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        var byIdGroup = app.MapGroup("/api/camps/editions/meals")
            .WithTags("Camp Meals Report")
            .WithOpenApi()
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

        byIdGroup.MapDelete("/extra-diners/{id:guid}", DeleteExtraDiner)
            .WithName("DeleteCampMealsExtraDiner")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        byIdGroup.MapDelete("/exclusions/{id:guid}", DeleteExclusion)
            .WithName("DeleteCampMealsExclusion")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetReport(
        Guid editionId, [FromServices] CampMealsService service, CancellationToken ct)
    {
        try
        {
            var report = await service.GetReportAsync(editionId, ct);
            return Results.Ok(ApiResponse<CampMealsReportResponse>.Ok(report));
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
    }

    private static async Task<IResult> ExportReport(
        Guid editionId, [FromServices] CampMealsService service, CancellationToken ct)
    {
        try
        {
            var bytes = await service.ExportAsync(editionId, ct);
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"comensales-{editionId}.xlsx");
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
    }

    private static async Task<IResult> GetAttendees(
        Guid editionId, [FromQuery] DateOnly date, [FromQuery] MealType mealType,
        [FromServices] CampMealsService service, CancellationToken ct)
    {
        var attendees = await service.GetAttendeesAsync(editionId, date, mealType, ct);
        return Results.Ok(ApiResponse<List<MealAttendeeResponse>>.Ok(attendees));
    }

    private static async Task<IResult> GetExtraDiners(
        Guid editionId, [FromServices] CampMealsService service, CancellationToken ct)
    {
        var diners = await service.GetExtraDinersAsync(editionId, ct);
        return Results.Ok(ApiResponse<List<ExtraDinerResponse>>.Ok(diners));
    }

    private static async Task<IResult> CreateExtraDiner(
        Guid editionId, CreateExtraDinerRequest request, ClaimsPrincipal user,
        [FromServices] CampMealsService service, CancellationToken ct)
    {
        try
        {
            var diner = await service.AddExtraDinerAsync(editionId, request, user.GetUserId(), ct);
            return Results.Created($"/api/camps/editions/meals/extra-diners/{diner.Id}",
                ApiResponse<ExtraDinerResponse>.Ok(diner));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ApiResponse<object>.Fail(ex.Message, "OPERATION_ERROR"));
        }
    }

    private static async Task<IResult> DeleteExtraDiner(
        Guid id, [FromServices] CampMealsService service, CancellationToken ct)
    {
        var deleted = await service.DeleteExtraDinerAsync(id, ct);
        return deleted
            ? Results.NoContent()
            : Results.NotFound(ApiResponse<object>.NotFound($"Registro con ID '{id}' no encontrado"));
    }

    private static async Task<IResult> GetExclusions(
        Guid editionId, [FromServices] CampMealsService service, CancellationToken ct)
    {
        var exclusions = await service.GetExclusionsAsync(editionId, ct);
        return Results.Ok(ApiResponse<List<MealExclusionResponse>>.Ok(exclusions));
    }

    private static async Task<IResult> CreateExclusion(
        Guid editionId, CreateMealExclusionRequest request, ClaimsPrincipal user,
        [FromServices] CampMealsService service, CancellationToken ct)
    {
        try
        {
            var exclusion = await service.AddExclusionAsync(editionId, request, user.GetUserId(), ct);
            return Results.Created($"/api/camps/editions/meals/exclusions/{exclusion.Id}",
                ApiResponse<MealExclusionResponse>.Ok(exclusion));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ApiResponse<object>.Fail(ex.Message, "OPERATION_ERROR"));
        }
    }

    private static async Task<IResult> DeleteExclusion(
        Guid id, [FromServices] CampMealsService service, CancellationToken ct)
    {
        var deleted = await service.DeleteExclusionAsync(id, ct);
        return deleted
            ? Results.NoContent()
            : Results.NotFound(ApiResponse<object>.NotFound($"Registro con ID '{id}' no encontrado"));
    }
}
```

`user.GetUserId()` — reuse whatever extension method the codebase already uses elsewhere to
pull the current user's id out of `ClaimsPrincipal` (already referenced in
`backend-standards.mdc`'s rate-limiting example as `httpContext.User.GetUserId()`); don't
reintroduce a second way of doing this.

---

## Program.cs registration

```csharp
builder.Services.AddScoped<ICampMealsRepository, CampMealsRepository>();
builder.Services.AddScoped<CampMealsService>();
// ...
app.MapCampMealsEndpoints();
```

---

## Test Coverage

### Unit — `CampMealsServiceTests`

- `GetReportAsync_WhenMemberAttendsComplete_CountsThemOnEveryDay`
- `GetReportAsync_WhenMemberAttendsFirstWeekOnly_ExcludedFromSecondWeekDays`
- `GetReportAsync_WhenMemberIsOnWeekendVisit_OnlyCountedWithinVisitDates`
- `GetReportAsync_WhenRegistrationIsCancelled_MemberNotCounted`
- `GetReportAsync_WhenExclusionExists_RemovesOnlyThatMealOnThatDay`
- `GetReportAsync_WhenExclusionExistsForOneMeal_OtherMealsUnaffected`
- `GetReportAsync_WhenExtraDinerAdded_IncludedInThatMealAndAgeCategoryOnly`
- `GetReportAsync_TotalsSumBaseAndExtraCorrectly`
- `GetReportAsync_WhenEditionNotFound_ThrowsInvalidOperationException`
- `AddExtraDinerAsync_WhenDateOutsideEditionRange_ThrowsInvalidOperationException`
- `AddExtraDinerAsync_WithValidData_PersistsAndReturnsResponse`
- `DeleteExtraDinerAsync_WhenNotFound_ReturnsFalse`
- `AddExclusionAsync_WhenMemberNotPresentThatDay_ThrowsInvalidOperationException`
- `AddExclusionAsync_WhenAlreadyExcluded_ThrowsInvalidOperationException`
- `AddExclusionAsync_WithValidData_PersistsAndReturnsResponse`
- `DeleteExclusionAsync_WhenExists_RemovesAndReturnsTrue`
- `GetAttendeesAsync_MarksAlreadyExcludedMembers_WithExclusionId`

### Unit — `CampMealsExcelExporterTests`

- `Build_ProducesOneRowPerDay`
- `Build_HeaderContainsAllMealAndAgeCategoryColumns`
- `Build_ValuesMatchReportTotals`

### Unit — `CampMealsValidatorTests`

- Standard `NotEmpty`/`IsInEnum`/`GreaterThan(0)`/`MaximumLength` cases for both request DTOs,
  following the existing style in `CampEditionExtrasValidatorTests` (see backend extras spec).

### Integration — `CampMealsEndpointsTests`

- `GetReport_WithBoardToken_Returns200`
- `GetReport_WithMemberToken_Returns403Forbidden`
- `ExportReport_WithBoardToken_ReturnsXlsxContentType`
- `CreateExtraDiner_WithValidData_Returns201`
- `CreateExtraDiner_WithInvalidData_Returns400`
- `CreateExclusion_ForMemberNotPresentThatDay_Returns400`
- `DeleteExclusion_WhenExists_Returns204`
- `DeleteExclusion_WhenNotFound_Returns404`

Coverage target: ≥90% for new code, consistent with `backend-standards.mdc`.

---

## Non-Functional Requirements

- **Security**: Board/Admin only on every endpoint — this is operational/catering data.
- **Performance**: computation is O(members × days × meals); trivial at ABUVI's scale (low
  hundreds of registrants, 1-2 week camps). No caching required for v1.
- **RGPD**: `MealExclusionResponse` and the attendees endpoint expose member full names to
  Board/Admin — same access level these users already have on the full registration list, so
  no new data-exposure surface is introduced.
- **i18n**: all validation and exception messages in Spanish; logs and code in English, per
  `backend-standards.mdc`.

---

## Implementation Order

1. Add `ClosedXML` package reference.
2. Add entities + EF Core configurations + DbSets; create and review the migration.
3. Implement `ICampMealsRepository` / `CampMealsRepository`.
4. Implement validators.
5. Implement `CampMealsService` (report computation first, then CRUD, then export).
6. Implement `CampMealsExcelExporter`.
7. Implement `CampMealsEndpoints` and register in `Program.cs`.
8. Unit tests (service, exporter, validators).
9. Integration tests.
10. Update `ai-specs/changes/INDEX.md` status and this folder's overview doc.

---

## Document Control

- **Version**: 1.0
- **Created**: 2026-08-26
- **Status**: ❌ Not Started
- **Dependencies**: none blocking — builds entirely on existing `CampEdition`, `Registration`,
  `RegistrationMember`, and `RegistrationPricingService`.
