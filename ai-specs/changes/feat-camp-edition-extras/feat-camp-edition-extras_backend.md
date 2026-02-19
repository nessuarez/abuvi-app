# Backend Implementation Plan: feat-camp-edition-extras — Camp Edition Extras Management

## Overview

This plan covers the full backend implementation of the **Camp Edition Extras** feature, which lets board members define and manage optional add-ons (e.g., t-shirts, excursions, meals) for specific camp editions.

The feature follows **Vertical Slice Architecture**: all code is co-located in `src/Abuvi.API/Features/Camps/` alongside the existing camp/edition code, since extras are tightly coupled to the `CampEdition` entity. No new top-level feature folder is needed.

**Important pre-conditions:**
- `CampEditionExtra` entity and its EF Core configuration (`CampEditionExtraConfiguration.cs`) already exist.
- The `PricingType` and `PricingPeriod` enums already exist in `CampsModels.cs` (named `PricingType` / `PricingPeriod`, not `ExtraPricingType` / `ExtraPricingPeriod` as in the spec — use the existing names).
- `AbuviDbContext` already has `public DbSet<CampEditionExtra> CampEditionExtras => Set<CampEditionExtra>();`.
- The database migration already includes the `camp_edition_extras` table.
- **No new migration is needed.**

---

## Architecture Context

**Feature slice:** `src/Abuvi.API/Features/Camps/`

### Files to create

| File | Purpose |
|------|---------|
| `Features/Camps/ICampEditionExtrasRepository.cs` | Repository interface |
| `Features/Camps/CampEditionExtrasRepository.cs` | EF Core repository implementation |
| `Features/Camps/CampEditionExtrasService.cs` | Business logic service |
| `Features/Camps/CampEditionExtrasValidator.cs` | FluentValidation validators |

### Files to modify

| File | Change |
|------|--------|
| `Features/Camps/CampsModels.cs` | Add Request/Response DTOs for extras |
| `Features/Camps/CampsEndpoints.cs` | Add extras endpoints to `MapCampsEndpoints()` |
| `Program.cs` | Register repository + service in DI |
| `Abuvi.Tests/Unit/Features/Camps/CampEditionExtrasServiceTests.cs` | *(create)* Unit tests |
| `Abuvi.Tests/Unit/Features/Camps/CampEditionExtrasValidatorTests.cs` | *(create)* Validator tests |
| `Abuvi.Tests/Integration/Features/Camps/CampEditionExtrasEndpointsTests.cs` | *(create)* Integration tests |

### Cross-cutting concerns

- `ApiResponse<T>` envelope (already in `Common/Models/`)
- `ValidationFilter<T>` endpoint filter (already in `Common/Filters/`)
- Role-based authorization: `Admin`/`Board` for write, `Admin`/`Board`/`Member` for reads

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch.
- **Branch Naming**: `feature/feat-camp-edition-extras-backend`
- **Implementation Steps**:
  1. Ensure you're on the latest `main` (or current base branch, which is `feature/feat-camps-accommodation`).
  2. `git pull origin feature/feat-camps-accommodation` (or `main` as appropriate).
  3. `git checkout -b feature/feat-camp-edition-extras-backend`
  4. Verify: `git branch`
- **Notes**: This must be the FIRST step. The current branch is `feature/feat-camps-accommodation`; check with the user whether to branch from `main` or from that branch.

---

### Step 1: Add Request/Response DTOs to CampsModels.cs

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`
- **Action**: Append the following records immediately after the existing `PricingPeriod` enum (around line 226), before the `AssociationSettings` class.
- **Implementation Steps**:
  1. Add `CampEditionExtraResponse` record.
  2. Add `CreateCampEditionExtraRequest` record.
  3. Add `UpdateCampEditionExtraRequest` record.

```csharp
// ── Camp Edition Extras DTOs ──────────────────────────────────────────────────

public record CampEditionExtraResponse(
    Guid Id,
    Guid CampEditionId,
    string Name,
    string? Description,
    decimal Price,
    PricingType PricingType,
    PricingPeriod PricingPeriod,
    bool IsRequired,
    bool IsActive,
    int? MaxQuantity,
    int CurrentQuantitySold,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateCampEditionExtraRequest(
    string Name,
    string? Description,
    decimal Price,
    PricingType PricingType,
    PricingPeriod PricingPeriod,
    bool IsRequired,
    int? MaxQuantity
);

public record UpdateCampEditionExtraRequest(
    string Name,
    string? Description,
    decimal Price,
    bool IsRequired,
    bool IsActive,
    int? MaxQuantity
);
```

- **Implementation Notes**:
  - Note the spec uses `ExtraPricingType`/`ExtraPricingPeriod`, but the codebase already uses `PricingType`/`PricingPeriod` — use the existing enums.
  - `CampEditionId` is NOT in `CreateCampEditionExtraRequest` because it comes from the route parameter `{editionId}`.

---

### Step 2: Create ICampEditionExtrasRepository

- **File**: `src/Abuvi.API/Features/Camps/ICampEditionExtrasRepository.cs`
- **Action**: Define the repository interface.

```csharp
namespace Abuvi.API.Features.Camps;

public interface ICampEditionExtrasRepository
{
    Task<CampEditionExtra?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<CampEditionExtra>> GetByCampEditionAsync(Guid campEditionId, bool? activeOnly, CancellationToken ct = default);
    Task<int> GetQuantitySoldAsync(Guid extraId, CancellationToken ct = default);
    Task AddAsync(CampEditionExtra extra, CancellationToken ct = default);
    Task UpdateAsync(CampEditionExtra extra, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

- **Implementation Notes**:
  - `GetQuantitySoldAsync` returns the sum of quantities from `registration_extras` where `camp_edition_extra_id = extraId`. Since the `RegistrationExtra` entity / DbSet may not yet exist, see Step 3 note below.
  - `bool? activeOnly` — `null` = return all; `true` = only active; `false` = only inactive.

---

### Step 3: Implement CampEditionExtrasRepository

- **File**: `src/Abuvi.API/Features/Camps/CampEditionExtrasRepository.cs`
- **Action**: Implement the repository using EF Core.

```csharp
using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Camps;

public class CampEditionExtrasRepository(AbuviDbContext db) : ICampEditionExtrasRepository
{
    public async Task<CampEditionExtra?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.CampEditionExtras
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<List<CampEditionExtra>> GetByCampEditionAsync(
        Guid campEditionId,
        bool? activeOnly,
        CancellationToken ct = default)
    {
        var query = db.CampEditionExtras
            .AsNoTracking()
            .Where(e => e.CampEditionId == campEditionId);

        if (activeOnly.HasValue)
            query = query.Where(e => e.IsActive == activeOnly.Value);

        return await query
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<int> GetQuantitySoldAsync(Guid extraId, CancellationToken ct = default)
    {
        // registration_extras table does not have a DbSet yet;
        // query raw SQL or return 0 until registrations feature is implemented.
        // Use FromSql when RegistrationExtra entity is available.
        // For now, return 0 as a safe placeholder.
        await Task.CompletedTask;
        return 0;
    }

    public async Task AddAsync(CampEditionExtra extra, CancellationToken ct = default)
    {
        db.CampEditionExtras.Add(extra);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(CampEditionExtra extra, CancellationToken ct = default)
    {
        db.CampEditionExtras.Update(extra);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var extra = await db.CampEditionExtras.FindAsync([id], ct);
        if (extra is not null)
        {
            db.CampEditionExtras.Remove(extra);
            await db.SaveChangesAsync(ct);
        }
    }
}
```

- **Implementation Notes**:
  - `GetQuantitySoldAsync` returns `0` as a placeholder until the `RegistrationExtras` DbSet exists. This is an intentional, documented simplification — the spec acknowledges that full registration integration is a separate concern (Sprint 2). Add a `// TODO: implement when RegistrationExtras DbSet is available` comment.
  - All reads use `AsNoTracking()` for performance.

---

### Step 4: Create FluentValidation Validators

- **File**: `src/Abuvi.API/Features/Camps/CampEditionExtrasValidator.cs`
- **Action**: Create validators for both request DTOs. All `.WithMessage()` calls use **Spanish** per project standards.

```csharp
using FluentValidation;

namespace Abuvi.API.Features.Camps;

public class CreateCampEditionExtraRequestValidator
    : AbstractValidator<CreateCampEditionExtraRequest>
{
    public CreateCampEditionExtraRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio")
            .MaximumLength(200).WithMessage("El nombre no puede superar los 200 caracteres");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("La descripción no puede superar los 1000 caracteres");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("El precio debe ser 0 o mayor")
            .PrecisionScale(10, 2, ignoreTrailingZeros: true)
            .WithMessage("El precio no puede tener más de 2 decimales");

        RuleFor(x => x.PricingType)
            .IsInEnum().WithMessage("El tipo de precio no es válido");

        RuleFor(x => x.PricingPeriod)
            .IsInEnum().WithMessage("El período de precio no es válido");

        RuleFor(x => x.MaxQuantity)
            .GreaterThan(0).WithMessage("La cantidad máxima debe ser mayor que 0")
            .When(x => x.MaxQuantity.HasValue);
    }
}

public class UpdateCampEditionExtraRequestValidator
    : AbstractValidator<UpdateCampEditionExtraRequest>
{
    public UpdateCampEditionExtraRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio")
            .MaximumLength(200).WithMessage("El nombre no puede superar los 200 caracteres");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("La descripción no puede superar los 1000 caracteres");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("El precio debe ser 0 o mayor")
            .PrecisionScale(10, 2, ignoreTrailingZeros: true)
            .WithMessage("El precio no puede tener más de 2 decimales");

        RuleFor(x => x.MaxQuantity)
            .GreaterThan(0).WithMessage("La cantidad máxima debe ser mayor que 0")
            .When(x => x.MaxQuantity.HasValue);
    }
}
```

---

### Step 5: Implement CampEditionExtrasService

- **File**: `src/Abuvi.API/Features/Camps/CampEditionExtrasService.cs`
- **Action**: Implement all business operations.

```csharp
namespace Abuvi.API.Features.Camps;

public class CampEditionExtrasService(
    ICampEditionExtrasRepository repository,
    ICampEditionsRepository editionsRepository)
{
    public async Task<List<CampEditionExtraResponse>> GetByEditionAsync(
        Guid campEditionId,
        bool? activeOnly,
        CancellationToken ct = default)
    {
        var extras = await repository.GetByCampEditionAsync(campEditionId, activeOnly, ct);
        var result = new List<CampEditionExtraResponse>(extras.Count);

        foreach (var extra in extras)
        {
            var sold = await repository.GetQuantitySoldAsync(extra.Id, ct);
            result.Add(extra.ToResponse(sold));
        }

        return result;
    }

    public async Task<CampEditionExtraResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var extra = await repository.GetByIdAsync(id, ct);
        if (extra is null) return null;

        var sold = await repository.GetQuantitySoldAsync(extra.Id, ct);
        return extra.ToResponse(sold);
    }

    public async Task<CampEditionExtraResponse> CreateAsync(
        Guid campEditionId,
        CreateCampEditionExtraRequest request,
        CancellationToken ct = default)
    {
        var edition = await editionsRepository.GetByIdAsync(campEditionId, ct);
        if (edition is null)
            throw new InvalidOperationException("La edición de campamento no fue encontrada");

        if (edition.Status is CampEditionStatus.Completed or CampEditionStatus.Closed)
            throw new InvalidOperationException(
                "No se pueden añadir extras a una edición cerrada o completada");

        var extra = new CampEditionExtra
        {
            Id = Guid.NewGuid(),
            CampEditionId = campEditionId,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            PricingType = request.PricingType,
            PricingPeriod = request.PricingPeriod,
            IsRequired = request.IsRequired,
            IsActive = true,
            MaxQuantity = request.MaxQuantity,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(extra, ct);
        return extra.ToResponse(currentQuantitySold: 0);
    }

    public async Task<CampEditionExtraResponse> UpdateAsync(
        Guid id,
        UpdateCampEditionExtraRequest request,
        CancellationToken ct = default)
    {
        var extra = await repository.GetByIdAsync(id, ct);
        if (extra is null)
            throw new InvalidOperationException("El extra de campamento no fue encontrado");

        var sold = await repository.GetQuantitySoldAsync(id, ct);

        if (request.MaxQuantity.HasValue && sold > request.MaxQuantity.Value)
            throw new InvalidOperationException(
                $"No se puede reducir la cantidad máxima a {request.MaxQuantity} " +
                $"porque ya se han vendido {sold} unidades");

        if (sold > 0 && request.Price != extra.Price)
            throw new InvalidOperationException(
                "No se puede cambiar el precio de un extra que ya ha sido adquirido. " +
                "Considera crear un nuevo extra en su lugar");

        extra.Name = request.Name;
        extra.Description = request.Description;
        extra.Price = request.Price;
        extra.IsRequired = request.IsRequired;
        extra.IsActive = request.IsActive;
        extra.MaxQuantity = request.MaxQuantity;
        extra.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(extra, ct);
        return extra.ToResponse(sold);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var extra = await repository.GetByIdAsync(id, ct);
        if (extra is null) return false;

        var sold = await repository.GetQuantitySoldAsync(id, ct);
        if (sold > 0)
            throw new InvalidOperationException(
                $"No se puede eliminar el extra '{extra.Name}' porque ha sido " +
                $"seleccionado en {sold} inscripción/inscripciones. " +
                "Considera desactivarlo en su lugar");

        await repository.DeleteAsync(id, ct);
        return true;
    }

    public async Task<CampEditionExtraResponse> ActivateAsync(Guid id, CancellationToken ct = default)
    {
        var extra = await repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("El extra de campamento no fue encontrado");

        extra.IsActive = true;
        extra.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(extra, ct);

        var sold = await repository.GetQuantitySoldAsync(id, ct);
        return extra.ToResponse(sold);
    }

    public async Task<CampEditionExtraResponse> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var extra = await repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("El extra de campamento no fue encontrado");

        extra.IsActive = false;
        extra.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(extra, ct);

        var sold = await repository.GetQuantitySoldAsync(id, ct);
        return extra.ToResponse(sold);
    }

    public async Task<bool> IsAvailableAsync(
        Guid extraId,
        int requestedQuantity,
        CancellationToken ct = default)
    {
        var extra = await repository.GetByIdAsync(extraId, ct);
        if (extra is null || !extra.IsActive) return false;
        if (!extra.MaxQuantity.HasValue) return true; // unlimited

        var sold = await repository.GetQuantitySoldAsync(extraId, ct);
        return (extra.MaxQuantity.Value - sold) >= requestedQuantity;
    }
}

/// <summary>Extension method to map entity to response DTO.</summary>
internal static class CampEditionExtraExtensions
{
    public static CampEditionExtraResponse ToResponse(
        this CampEditionExtra extra,
        int currentQuantitySold)
        => new(
            extra.Id,
            extra.CampEditionId,
            extra.Name,
            extra.Description,
            extra.Price,
            extra.PricingType,
            extra.PricingPeriod,
            extra.IsRequired,
            extra.IsActive,
            extra.MaxQuantity,
            currentQuantitySold,
            extra.CreatedAt,
            extra.UpdatedAt
        );
}
```

- **Implementation Notes**:
  - The `ToResponse` extension is in the same file (`CampEditionExtrasService.cs`) as an `internal static` class to keep it co-located with the service that uses it, following the pattern of other services in this codebase.
  - All exception messages are in **Spanish** per project standards.
  - `IsAvailableAsync` is a helper for the future registration flow — expose it from the service so it can be called by the registration service without breaking the slice boundary.

---

### Step 6: Add Extras Endpoints to CampsEndpoints.cs

- **File**: `src/Abuvi.API/Features/Camps/CampsEndpoints.cs`
- **Action**: Append a new section for extras endpoints inside the existing `MapCampsEndpoints()` extension method, after the existing editions endpoints block.

**Endpoint groups to add:**

```csharp
// ── Camp Edition Extras (Board+ write) ───────────────────────────────────────
var extrasWriteGroup = app.MapGroup("/api/camps/editions/{editionId:guid}/extras")
    .WithTags("Camp Edition Extras")
    .WithOpenApi()
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

extrasWriteGroup.MapPost("/", CreateExtra)
    .WithName("CreateCampEditionExtra")
    .WithSummary("Create a new extra for a camp edition")
    .AddEndpointFilter<ValidationFilter<CreateCampEditionExtraRequest>>()
    .Produces<ApiResponse<CampEditionExtraResponse>>(StatusCodes.Status201Created)
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status401Unauthorized)
    .Produces(StatusCodes.Status403Forbidden)
    .Produces(StatusCodes.Status404NotFound);

// ── Camp Edition Extras (Member+ read) ───────────────────────────────────────
var extrasReadGroup = app.MapGroup("/api/camps/editions/{editionId:guid}/extras")
    .WithTags("Camp Edition Extras")
    .WithOpenApi()
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board", "Member"));

extrasReadGroup.MapGet("/", GetExtrasByEdition)
    .WithName("GetCampEditionExtras")
    .WithSummary("List all extras for a camp edition")
    .Produces<ApiResponse<List<CampEditionExtraResponse>>>()
    .Produces(StatusCodes.Status401Unauthorized);

// ── Camp Edition Extras by ID (Board+ write) ─────────────────────────────────
var extrasByIdWriteGroup = app.MapGroup("/api/camps/editions/extras")
    .WithTags("Camp Edition Extras")
    .WithOpenApi()
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

extrasByIdWriteGroup.MapPut("/{id:guid}", UpdateExtra)
    .WithName("UpdateCampEditionExtra")
    .WithSummary("Update a camp edition extra")
    .AddEndpointFilter<ValidationFilter<UpdateCampEditionExtraRequest>>()
    .Produces<ApiResponse<CampEditionExtraResponse>>()
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status401Unauthorized)
    .Produces(StatusCodes.Status403Forbidden)
    .Produces(StatusCodes.Status404NotFound);

extrasByIdWriteGroup.MapDelete("/{id:guid}", DeleteExtra)
    .WithName("DeleteCampEditionExtra")
    .WithSummary("Delete a camp edition extra (only if not sold)")
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status401Unauthorized)
    .Produces(StatusCodes.Status403Forbidden)
    .Produces(StatusCodes.Status404NotFound);

extrasByIdWriteGroup.MapPatch("/{id:guid}/activate", ActivateExtra)
    .WithName("ActivateCampEditionExtra")
    .WithSummary("Activate a camp edition extra")
    .Produces<ApiResponse<CampEditionExtraResponse>>()
    .Produces(StatusCodes.Status401Unauthorized)
    .Produces(StatusCodes.Status403Forbidden)
    .Produces(StatusCodes.Status404NotFound);

extrasByIdWriteGroup.MapPatch("/{id:guid}/deactivate", DeactivateExtra)
    .WithName("DeactivateCampEditionExtra")
    .WithSummary("Deactivate a camp edition extra")
    .Produces<ApiResponse<CampEditionExtraResponse>>()
    .Produces(StatusCodes.Status401Unauthorized)
    .Produces(StatusCodes.Status403Forbidden)
    .Produces(StatusCodes.Status404NotFound);

// ── Camp Edition Extras by ID (Member+ read) ─────────────────────────────────
var extrasByIdReadGroup = app.MapGroup("/api/camps/editions/extras")
    .WithTags("Camp Edition Extras")
    .WithOpenApi()
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board", "Member"));

extrasByIdReadGroup.MapGet("/{id:guid}", GetExtraById)
    .WithName("GetCampEditionExtraById")
    .WithSummary("Get a specific camp edition extra by ID")
    .Produces<ApiResponse<CampEditionExtraResponse>>()
    .Produces(StatusCodes.Status401Unauthorized)
    .Produces(StatusCodes.Status404NotFound);
```

**Private handler methods to add** (also in `CampsEndpoints.cs`):

```csharp
private static async Task<IResult> GetExtrasByEdition(
    Guid editionId,
    [FromQuery] bool? activeOnly,
    [FromServices] CampEditionExtrasService service,
    CancellationToken ct)
{
    var extras = await service.GetByEditionAsync(editionId, activeOnly, ct);
    return Results.Ok(ApiResponse<List<CampEditionExtraResponse>>.Ok(extras));
}

private static async Task<IResult> GetExtraById(
    Guid id,
    [FromServices] CampEditionExtrasService service,
    CancellationToken ct)
{
    var extra = await service.GetByIdAsync(id, ct);
    return extra is not null
        ? Results.Ok(ApiResponse<CampEditionExtraResponse>.Ok(extra))
        : Results.NotFound(ApiResponse<CampEditionExtraResponse>.NotFound(
            $"Extra con ID '{id}' no encontrado"));
}

private static async Task<IResult> CreateExtra(
    Guid editionId,
    CreateCampEditionExtraRequest request,
    [FromServices] CampEditionExtrasService service,
    CancellationToken ct)
{
    try
    {
        var extra = await service.CreateAsync(editionId, request, ct);
        return Results.Created(
            $"/api/camps/editions/extras/{extra.Id}",
            ApiResponse<CampEditionExtraResponse>.Ok(extra));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(
            ApiResponse<CampEditionExtraResponse>.Fail(ex.Message, "OPERATION_ERROR"));
    }
}

private static async Task<IResult> UpdateExtra(
    Guid id,
    UpdateCampEditionExtraRequest request,
    [FromServices] CampEditionExtrasService service,
    CancellationToken ct)
{
    try
    {
        var extra = await service.UpdateAsync(id, request, ct);
        return Results.Ok(ApiResponse<CampEditionExtraResponse>.Ok(extra));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(
            ApiResponse<CampEditionExtraResponse>.Fail(ex.Message, "OPERATION_ERROR"));
    }
}

private static async Task<IResult> DeleteExtra(
    Guid id,
    [FromServices] CampEditionExtrasService service,
    CancellationToken ct)
{
    try
    {
        var deleted = await service.DeleteAsync(id, ct);
        return deleted
            ? Results.NoContent()
            : Results.NotFound(ApiResponse<object>.NotFound(
                $"Extra con ID '{id}' no encontrado"));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(ApiResponse<object>.Fail(ex.Message, "OPERATION_ERROR"));
    }
}

private static async Task<IResult> ActivateExtra(
    Guid id,
    [FromServices] CampEditionExtrasService service,
    CancellationToken ct)
{
    try
    {
        var extra = await service.ActivateAsync(id, ct);
        return Results.Ok(ApiResponse<CampEditionExtraResponse>.Ok(extra));
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
}

private static async Task<IResult> DeactivateExtra(
    Guid id,
    [FromServices] CampEditionExtrasService service,
    CancellationToken ct)
{
    try
    {
        var extra = await service.DeactivateAsync(id, ct);
        return Results.Ok(ApiResponse<CampEditionExtraResponse>.Ok(extra));
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
}
```

- **Implementation Notes**:
  - Routing follows the spec exactly: `GET /api/camps/editions/{editionId}/extras` and `GET /api/camps/editions/extras/{id}`.
  - `[FromServices]` is needed for services injected in Minimal API handlers that aren't route/query/body params.
  - Two separate groups per path prefix allow different auth policies: Board+ for mutations, Member+ for reads.
  - The `NotFound` result for activate/deactivate is appropriate since those operations throw when the entity is missing.

---

### Step 7: Register Services in Program.cs

- **File**: `src/Abuvi.API/Program.cs`
- **Action**: Add registrations after line 155 (after `CampPhotosService`).

```csharp
builder.Services.AddScoped<ICampEditionExtrasRepository, CampEditionExtrasRepository>();
builder.Services.AddScoped<CampEditionExtrasService>();
```

No change to `app.Map*` calls is needed — all extras endpoints are added to `MapCampsEndpoints()`.

---

### Step 8: Write Unit Tests

- **File**: `src/Abuvi.Tests/Unit/Features/Camps/CampEditionExtrasServiceTests.cs`
- **Action**: Create unit tests for `CampEditionExtrasService` following the AAA pattern and `MethodName_StateUnderTest_ExpectedBehavior` naming.

**Test class structure:**

```csharp
using Abuvi.API.Features.Camps;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

public class CampEditionExtrasServiceTests
{
    private readonly ICampEditionExtrasRepository _repository;
    private readonly ICampEditionsRepository _editionsRepository;
    private readonly CampEditionExtrasService _sut;

    public CampEditionExtrasServiceTests()
    {
        _repository = Substitute.For<ICampEditionExtrasRepository>();
        _editionsRepository = Substitute.For<ICampEditionsRepository>();
        _sut = new CampEditionExtrasService(_repository, _editionsRepository);
    }

    // ── CreateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesExtraAndReturnsResponse()

    [Fact]
    public async Task CreateAsync_WhenEditionNotFound_ThrowsInvalidOperationException()

    [Fact]
    public async Task CreateAsync_WhenEditionIsClosed_ThrowsInvalidOperationException()

    [Fact]
    public async Task CreateAsync_WhenEditionIsCompleted_ThrowsInvalidOperationException()

    [Fact]
    public async Task CreateAsync_WithZeroPrice_CreatesExtraSuccessfully()

    [Fact]
    public async Task CreateAsync_WithMaxQuantity_SetsMaxQuantity()

    [Fact]
    public async Task CreateAsync_WithNullMaxQuantity_AllowsUnlimited()

    // ── UpdateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesExtraAndReturnsResponse()

    [Fact]
    public async Task UpdateAsync_WhenExtraNotFound_ThrowsInvalidOperationException()

    [Fact]
    public async Task UpdateAsync_WhenReducingMaxQuantityBelowSold_ThrowsInvalidOperationException()

    [Fact]
    public async Task UpdateAsync_WhenChangingPriceOnSoldExtra_ThrowsInvalidOperationException()

    [Fact]
    public async Task UpdateAsync_WhenSoldIsZeroAndChangingPrice_AllowsUpdate()

    [Fact]
    public async Task UpdateAsync_WhenReducingMaxQuantityAboveSold_AllowsUpdate()

    [Fact]
    public async Task UpdateAsync_CanDeactivateExtra()

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_WhenNotSold_DeletesAndReturnsTrue()

    [Fact]
    public async Task DeleteAsync_WhenExtraNotFound_ReturnsFalse()

    [Fact]
    public async Task DeleteAsync_WhenExtraHasSold_ThrowsInvalidOperationException()

    // ── GetByEditionAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetByEditionAsync_ReturnsExtrasWithQuantitySold()

    [Fact]
    public async Task GetByEditionAsync_WithActiveOnly_PassesFilterToRepository()

    // ── IsAvailableAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task IsAvailableAsync_WhenUnlimited_ReturnsTrue()

    [Fact]
    public async Task IsAvailableAsync_WhenQuantityAvailable_ReturnsTrue()

    [Fact]
    public async Task IsAvailableAsync_WhenQuantityExceeded_ReturnsFalse()

    [Fact]
    public async Task IsAvailableAsync_WhenExtraNotFound_ReturnsFalse()

    [Fact]
    public async Task IsAvailableAsync_WhenExtraInactive_ReturnsFalse()

    // ── ActivateAsync / DeactivateAsync ───────────────────────────────────────

    [Fact]
    public async Task ActivateAsync_SetsIsActiveToTrue()

    [Fact]
    public async Task DeactivateAsync_SetsIsActiveToFalse()

    [Fact]
    public async Task ActivateAsync_WhenExtraNotFound_ThrowsInvalidOperationException()
}
```

- **File**: `src/Abuvi.Tests/Unit/Features/Camps/CampEditionExtrasValidatorTests.cs`
- **Action**: Test validators for both request DTOs.

**Key validator test cases:**

```text
CreateCampEditionExtraRequestValidator:
  - ValidRequest_PassesValidation
  - WhenNameIsEmpty_FailsValidation
  - WhenNameExceeds200Chars_FailsValidation
  - WhenDescriptionExceeds1000Chars_FailsValidation
  - WhenPriceIsNegative_FailsValidation
  - WhenPriceIsZero_PassesValidation
  - WhenMaxQuantityIsZero_FailsValidation
  - WhenMaxQuantityIsPositive_PassesValidation
  - WhenMaxQuantityIsNull_PassesValidation
  - WhenPricingTypeIsInvalidEnum_FailsValidation
  - WhenPricingPeriodIsInvalidEnum_FailsValidation

UpdateCampEditionExtraRequestValidator:
  - ValidRequest_PassesValidation
  - WhenNameIsEmpty_FailsValidation
  - WhenPriceIsNegative_FailsValidation
  - WhenMaxQuantityIsNullAndIsActiveIsTrue_PassesValidation
```

---

### Step 9: Write Integration Tests

- **File**: `src/Abuvi.Tests/Integration/Features/Camps/CampEditionExtrasEndpointsTests.cs`
- **Action**: Integration tests for all endpoints using `WebApplicationFactory<Program>` (same pattern as `CampEditionsEndpointsTests.cs`).

**Test categories and cases:**

```text
POST /api/camps/editions/{editionId}/extras:
  - CreateExtra_WithBoardToken_Returns201Created
  - CreateExtra_WithMemberToken_Returns403Forbidden
  - CreateExtra_WithoutToken_Returns401Unauthorized
  - CreateExtra_WithInvalidData_Returns400BadRequest
  - CreateExtra_WithNonExistentEdition_Returns400BadRequest

GET /api/camps/editions/{editionId}/extras:
  - GetExtrasByEdition_WithMemberToken_Returns200WithList
  - GetExtrasByEdition_WithoutToken_Returns401Unauthorized
  - GetExtrasByEdition_WithActiveOnlyFilter_Returns200

GET /api/camps/editions/extras/{id}:
  - GetExtraById_WithExistingId_Returns200
  - GetExtraById_WithNonExistentId_Returns404
  - GetExtraById_WithMemberToken_Returns200
  - GetExtraById_WithoutToken_Returns401Unauthorized

PUT /api/camps/editions/extras/{id}:
  - UpdateExtra_WithBoardToken_Returns200
  - UpdateExtra_WithMemberToken_Returns403Forbidden
  - UpdateExtra_WithInvalidData_Returns400BadRequest
  - UpdateExtra_WithNonExistentId_Returns400BadRequest (service throws)

DELETE /api/camps/editions/extras/{id}:
  - DeleteExtra_WithBoardToken_Returns204NoContent
  - DeleteExtra_WithMemberToken_Returns403Forbidden
  - DeleteExtra_WithNonExistentId_Returns404NotFound

PATCH /api/camps/editions/extras/{id}/activate:
  - ActivateExtra_WithBoardToken_Returns200
  - ActivateExtra_WithMemberToken_Returns403Forbidden

PATCH /api/camps/editions/extras/{id}/deactivate:
  - DeactivateExtra_WithBoardToken_Returns200
  - DeactivateExtra_WithMemberToken_Returns403Forbidden
```

**Reuse the helper pattern** from `CampEditionsEndpointsTests.cs`:
- `GetAdminTokenAsync()` / `GetMemberTokenAsync()` / `CreateCampAsync()` / `ProposeEditionAsync()` are already in the integration test file — extract them to a shared `CampTestHelpers` static class in `Abuvi.Tests/Helpers/` if more than 2 test files share them, otherwise duplicate for now.

---

### Step 10: Update Technical Documentation

- **Action**: Review all changes and update the following documents.
- **Implementation Steps**:

  1. **`ai-specs/specs/api-spec.yml`** (if it exists) — Add the 7 new endpoints with their request/response schemas.
  2. **`ai-specs/changes/feat-camp-edition-extras/camp-edition-extras.md`** — Update `**Status:**` from `❌ NOT STARTED` to `✅ IMPLEMENTED` and mark implementation tasks as done.
  3. **`ai-specs/specs/data-model.md`** — Verify `camp_edition_extras` is already documented; if not, add it. No migration needed since the table already exists.

- **Notes**: All documentation must be in **English** per `documentation-standards.mdc`.

---

## Implementation Order

1. **Step 0** — Create feature branch `feature/feat-camp-edition-extras-backend`
2. **Step 1** — Add DTOs to `CampsModels.cs`
3. **Step 2** — Create `ICampEditionExtrasRepository`
4. **Step 3** — Implement `CampEditionExtrasRepository`
5. **Step 4** — Create `CampEditionExtrasValidator.cs`
6. **Step 5** — Implement `CampEditionExtrasService.cs`
7. **Step 6** — Add endpoints to `CampsEndpoints.cs`
8. **Step 7** — Register services in `Program.cs`
9. **Step 8** — Write unit tests
10. **Step 9** — Write integration tests
11. **Step 10** — Update technical documentation

---

## Testing Checklist

- [ ] `CampEditionExtrasServiceTests` — 20+ unit tests (AAA pattern, NSubstitute mocks)
- [ ] `CampEditionExtrasValidatorTests` — 15+ validator tests
- [ ] `CampEditionExtrasEndpointsTests` — 20+ integration tests covering all HTTP status codes
- [ ] All tests pass: `dotnet test`
- [ ] Test coverage ≥ 90% for new code
- [ ] Manual smoke test via Swagger UI: create, list, update, activate, deactivate, delete

---

## Error Response Format

All responses use the `ApiResponse<T>` envelope:

```json
// Success (200 OK)
{ "success": true, "data": { ... }, "error": null }

// Created (201)
{ "success": true, "data": { ... }, "error": null }

// Not found (404)
{ "success": false, "data": null, "error": { "message": "...", "code": "NOT_FOUND" } }

// Business rule violation / invalid operation (400)
{ "success": false, "data": null, "error": { "message": "...", "code": "OPERATION_ERROR" } }

// Validation error (400)
{ "success": false, "data": null, "error": { "message": "Validation failed", "code": "VALIDATION_ERROR" } }
```

HTTP status code mapping:
- `201 Created` — POST success
- `200 OK` — GET / PUT / PATCH success
- `204 No Content` — DELETE success
- `400 Bad Request` — Validation failure or business rule violation
- `401 Unauthorized` — Missing or invalid JWT
- `403 Forbidden` — Insufficient role
- `404 Not Found` — Entity not found

---

## Dependencies

No new NuGet packages needed — all required packages are already referenced.

No EF Core migration needed — `camp_edition_extras` table already exists.

```bash
# Run tests
dotnet test src/Abuvi.Tests/Abuvi.Tests.csproj

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## Notes

1. **Existing types**: Use `PricingType` and `PricingPeriod` (already in `CampsModels.cs`), NOT `ExtraPricingType`/`ExtraPricingPeriod` as the spec document suggests.
2. **No migration**: The database table and EF Core configuration already exist. Do NOT run `dotnet ef migrations add`.
3. **Registration integration (placeholder)**: `GetQuantitySoldAsync` returns `0` until the `RegistrationExtras` feature is implemented. Mark with `// TODO` comment.
4. **Validation messages in Spanish**: All FluentValidation `.WithMessage()` must use Spanish text per `backend-standards.mdc`.
5. **Log messages in English**: All `ILogger` structured log messages stay in English.
6. **Primary constructor syntax**: Use `public class CampEditionExtrasService(...)` pattern as per project conventions.
7. **File-scoped namespaces**: Use `namespace Abuvi.API.Features.Camps;` style.
8. **`IsAvailableAsync`** is not exposed via API endpoint — it is a service utility method intended for use by the future Registrations feature.
9. **Pricing calculations** (`ExtraPriceCalculator` from the spec) are explicitly out of scope for this backend ticket. The spec separates Sprint 3 for pricing — only the CRUD and availability check are in scope here.

---

## Next Steps After Implementation

- Frontend implementation of Extras management UI (separate ticket).
- Registrations feature: call `IsAvailableAsync` and `GetQuantitySoldAsync` during registration flow.
- Sprint 3: Implement `ExtraPriceCalculator` utility and integrate into registration pricing.

---

## Implementation Verification

- [ ] **Code Quality**: No compiler warnings, nullable reference types satisfied, `TreatWarningsAsErrors` passes.
- [ ] **Functionality**: All 7 endpoints return correct HTTP status codes (manual Swagger test).
- [ ] **Testing**: 90%+ coverage, all unit and integration tests pass.
- [ ] **Integration**: No EF Core migration errors, existing tests still pass.
- [ ] **Documentation**: `camp-edition-extras.md` status updated, API spec updated.
