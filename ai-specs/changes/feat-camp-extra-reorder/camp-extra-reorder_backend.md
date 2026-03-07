# Backend Implementation Plan: camp-extra-reorder — CampEditionExtra Manual Reordering

## Overview

Add a `SortOrder` field to `CampEditionExtra` to allow administrators to manually reorder extras for better grouping and readability during camp registration. This follows the **existing pattern** already implemented in `CampEditionAccommodation` (which has `SortOrder` with identical requirements).

**Architecture**: Vertical Slice Architecture — all changes within `src/Abuvi.API/Features/Camps/`.

## Architecture Context

- **Feature slice**: `src/Abuvi.API/Features/Camps/`
- **Reference implementation**: `CampEditionAccommodation.SortOrder` — follow this pattern exactly
- **Files to modify**:
  - `CampsModels.cs` — Entity + DTOs
  - `CampEditionExtraConfiguration.cs` — EF Core configuration
  - `CampEditionExtrasRepository.cs` — Ordering + bulk update
  - `CampEditionExtrasService.cs` — Mapping + reorder logic
  - `CampsValidators.cs` — SortOrder validation
  - `CampsEndpoints.cs` — Reorder endpoint
- **Files to create**:
  - New EF Core migration
- **Test files to update**:
  - `CampEditionExtrasServiceTests.cs`
  - `CampEditionExtrasValidatorTests.cs`
  - `CampEditionExtrasEndpointsTests.cs`
- **Documentation to update**:
  - `ai-specs/specs/data-model.md`

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/camp-extra-reorder-backend`
- **Implementation Steps**:
  1. Ensure on latest `dev` branch: `git checkout dev && git pull origin dev`
  2. Create new branch: `git checkout -b feature/camp-extra-reorder-backend`
  3. Verify: `git branch`

---

### Step 1: Update Entity Model

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`
- **Action**: Add `SortOrder` property to `CampEditionExtra` class

**Implementation Steps**:

1. Add `SortOrder` property to `CampEditionExtra` entity (after `MaxQuantity`, before `RequiresUserInput`):
   ```csharp
   public int SortOrder { get; set; } = 0;
   ```
   Reference: `CampEditionAccommodation` line 360 uses the identical pattern.

2. Add `SortOrder` to `CampEditionExtraResponse` record:
   ```csharp
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
       bool RequiresUserInput,
       string? UserInputLabel,
       int SortOrder,              // <-- ADD
       int CurrentQuantitySold,
       DateTime CreatedAt,
       DateTime UpdatedAt
   );
   ```

3. Add `SortOrder` to `CreateCampEditionExtraRequest` record (with default 0):
   ```csharp
   public record CreateCampEditionExtraRequest(
       string Name,
       string? Description,
       decimal Price,
       PricingType PricingType,
       PricingPeriod PricingPeriod,
       bool IsRequired,
       int? MaxQuantity,
       bool RequiresUserInput = false,
       string? UserInputLabel = null,
       int SortOrder = 0           // <-- ADD (at end with default)
   );
   ```

4. Add `SortOrder` to `UpdateCampEditionExtraRequest` record:
   ```csharp
   public record UpdateCampEditionExtraRequest(
       string Name,
       string? Description,
       decimal Price,
       bool IsRequired,
       bool IsActive,
       int? MaxQuantity,
       bool RequiresUserInput = false,
       string? UserInputLabel = null,
       int SortOrder = 0           // <-- ADD (at end with default)
   );
   ```

5. Add `ReorderCampEditionExtrasRequest` record (near the other extra DTOs):
   ```csharp
   public record ReorderCampEditionExtrasRequest(
       List<Guid> OrderedIds
   );
   ```

- **Notes**: The default value of `0` ensures backward compatibility — existing extras without explicit `SortOrder` will all have `0`, and the `ThenBy(CreatedAt)` in the repository preserves their original order.

---

### Step 2: Update EF Core Configuration

- **File**: `src/Abuvi.API/Data/Configurations/CampEditionExtraConfiguration.cs`
- **Action**: Add `SortOrder` column mapping with check constraint

**Implementation Steps**:

1. Add property configuration (follow `CampEditionAccommodationConfiguration` pattern):
   ```csharp
   builder.Property(e => e.SortOrder)
       .HasColumnName("sort_order")
       .IsRequired()
       .HasDefaultValue(0);
   ```

2. Add check constraint (same as accommodation pattern):
   ```csharp
   builder.HasCheckConstraint("ck_camp_edition_extras_sort_order", "sort_order >= 0");
   ```

- **Reference**: `CampEditionAccommodationConfiguration.cs` lines 47-54 for the exact pattern.

---

### Step 3: Update Validators

- **File**: `src/Abuvi.API/Features/Camps/CampsValidators.cs`
- **Action**: Add `SortOrder` validation to both Create and Update validators

**Implementation Steps**:

1. In `CreateCampEditionExtraRequestValidator`, add:
   ```csharp
   RuleFor(x => x.SortOrder)
       .GreaterThanOrEqualTo(0).WithMessage("Sort order must be greater than or equal to 0");
   ```

2. In `UpdateCampEditionExtraRequestValidator`, add:
   ```csharp
   RuleFor(x => x.SortOrder)
       .GreaterThanOrEqualTo(0).WithMessage("Sort order must be greater than or equal to 0");
   ```

3. Add `ReorderCampEditionExtrasRequestValidator`:
   ```csharp
   public class ReorderCampEditionExtrasRequestValidator
       : AbstractValidator<ReorderCampEditionExtrasRequest>
   {
       public ReorderCampEditionExtrasRequestValidator()
       {
           RuleFor(x => x.OrderedIds)
               .NotEmpty().WithMessage("Ordered IDs list is required");

           RuleForEach(x => x.OrderedIds)
               .NotEmpty().WithMessage("Each ID must be a valid GUID");
       }
   }
   ```

- **Reference**: `CreateCampEditionAccommodationRequestValidator` lines 408-410 for the exact validation pattern.

---

### Step 4: Update Repository

- **File**: `src/Abuvi.API/Features/Camps/CampEditionExtrasRepository.cs`
- **Action**: Change ordering and add bulk update method

**Implementation Steps**:

1. In `GetByCampEditionAsync`, change ordering from:
   ```csharp
   .OrderBy(e => e.CreatedAt)
   ```
   To:
   ```csharp
   .OrderBy(e => e.SortOrder)
   .ThenBy(e => e.CreatedAt)
   ```

2. Add `UpdateManyAsync` method for bulk reorder (add to interface `ICampEditionExtrasRepository` too):
   ```csharp
   public async Task UpdateManyAsync(List<CampEditionExtra> extras, CancellationToken ct = default)
   {
       db.CampEditionExtras.UpdateRange(extras);
       await db.SaveChangesAsync(ct);
   }
   ```

3. Add method to get tracked extras for a camp edition (needed for reorder - we need tracked entities):
   ```csharp
   public async Task<List<CampEditionExtra>> GetByCampEditionTrackedAsync(
       Guid campEditionId,
       CancellationToken ct = default)
   {
       return await db.CampEditionExtras
           .Where(e => e.CampEditionId == campEditionId)
           .ToListAsync(ct);
   }
   ```

4. Update the `ICampEditionExtrasRepository` interface to include the new methods.

---

### Step 5: Update Service

- **File**: `src/Abuvi.API/Features/Camps/CampEditionExtrasService.cs`
- **Action**: Map `SortOrder` in create/update, add reorder method

**Implementation Steps**:

1. In `CreateAsync`, map `SortOrder` from request to entity:
   ```csharp
   SortOrder = request.SortOrder,
   ```

2. In `UpdateAsync`, map `SortOrder` from request to entity:
   ```csharp
   extra.SortOrder = request.SortOrder;
   ```

3. In the `ToResponse()` extension method, add `SortOrder` to the response mapping:
   ```csharp
   SortOrder: extra.SortOrder,
   ```
   Ensure the parameter position matches the updated `CampEditionExtraResponse` record.

4. Add `ReorderAsync` method to service (and `ICampEditionExtrasService` interface):
   ```csharp
   public async Task<IResult> ReorderAsync(
       Guid campEditionId,
       ReorderCampEditionExtrasRequest request,
       CancellationToken ct = default)
   {
       // 1. Fetch all extras for this edition (tracked)
       var extras = await repository.GetByCampEditionTrackedAsync(campEditionId, ct);

       if (extras.Count == 0)
           return TypedResults.NotFound(ApiResponse<object>.Error("No extras found for this camp edition"));

       // 2. Validate all IDs belong to this edition
       var extraIds = extras.Select(e => e.Id).ToHashSet();
       var requestIds = request.OrderedIds.ToHashSet();

       if (!requestIds.SetEquals(extraIds))
           return TypedResults.BadRequest(ApiResponse<object>.Error(
               "Ordered IDs must contain exactly all extras for this camp edition"));

       // 3. Assign SortOrder based on position
       for (var i = 0; i < request.OrderedIds.Count; i++)
       {
           var extra = extras.First(e => e.Id == request.OrderedIds[i]);
           extra.SortOrder = i;
           extra.UpdatedAt = DateTime.UtcNow;
       }

       // 4. Save all changes
       await repository.UpdateManyAsync(extras, ct);

       return TypedResults.Ok(ApiResponse<string>.Success("Extras reordered successfully"));
   }
   ```

- **Business Rules**:
  - All extra IDs in the request must belong to the specified `campEditionId`
  - The request must contain **exactly** all extras for the edition (no partial reorders)
  - SortOrder values are assigned as 0, 1, 2, ... based on array position

---

### Step 6: Add Reorder Endpoint

- **File**: `src/Abuvi.API/Features/Camps/CampsEndpoints.cs`
- **Action**: Add reorder endpoint to the extras write group

**Implementation Steps**:

1. Add reorder endpoint in the extras write group (alongside existing POST/PUT/DELETE/PATCH):
   ```csharp
   extrasWriteGroup.MapPut("/{editionId:guid}/extras/reorder", async (
       Guid editionId,
       [FromBody] ReorderCampEditionExtrasRequest request,
       ICampEditionExtrasService service,
       CancellationToken ct) =>
   {
       return await service.ReorderAsync(editionId, request, ct);
   })
   .WithName("ReorderCampEditionExtras")
   .WithDescription("Reorder extras for a camp edition")
   .AddEndpointFilter<ValidationFilter<ReorderCampEditionExtrasRequest>>()
   .Produces<ApiResponse<string>>(200)
   .Produces<ApiResponse<object>>(400)
   .Produces<ApiResponse<object>>(404);
   ```

- **Route**: `PUT /api/camps/editions/{editionId}/extras/reorder`
- **Authorization**: Board+ (same as other write operations on extras)
- **Notes**: This endpoint sits in the write group which already has Board+ authorization configured.

---

### Step 7: Create EF Core Migration

- **Action**: Generate and apply migration for the new `sort_order` column

**Implementation Steps**:

1. Generate migration:
   ```bash
   cd src/Abuvi.API
   dotnet ef migrations add AddSortOrderToCampEditionExtras
   ```

2. Review the generated migration file and verify it contains:
   - `AddColumn("sort_order", table: "camp_edition_extras", type: "integer", nullable: false, defaultValue: 0)`
   - `AddCheckConstraint("ck_camp_edition_extras_sort_order", "camp_edition_extras", "sort_order >= 0")`

3. Consider adding a SQL data migration to set initial `sort_order` based on `created_at` ordering (optional but recommended):
   ```sql
   -- In the Up() method, after AddColumn:
   migrationBuilder.Sql(@"
       WITH ranked AS (
           SELECT id, ROW_NUMBER() OVER (PARTITION BY camp_edition_id ORDER BY created_at) - 1 AS rn
           FROM camp_edition_extras
       )
       UPDATE camp_edition_extras SET sort_order = ranked.rn
       FROM ranked WHERE camp_edition_extras.id = ranked.id;
   ");
   ```

4. Apply migration:
   ```bash
   dotnet ef database update
   ```

---

### Step 8: Write Unit Tests

- **Files**:
  - `src/Abuvi.Tests/Unit/Features/Camps/CampEditionExtrasServiceTests.cs`
  - `src/Abuvi.Tests/Unit/Features/Camps/CampEditionExtrasValidatorTests.cs`

**Implementation Steps**:

#### Service Tests (`CampEditionExtrasServiceTests.cs`):

1. **ReorderAsync_WithValidIds_ReorderSuccessfully**
   - Setup: Create 3 extras with IDs [A, B, C], mock repository
   - Act: Call `ReorderAsync` with order [C, A, B]
   - Assert: SortOrder set to C=0, A=1, B=2; returns Ok

2. **ReorderAsync_WithMismatchedIds_ReturnsBadRequest**
   - Setup: Create 2 extras [A, B], request has [A, C]
   - Assert: Returns BadRequest with appropriate error message

3. **ReorderAsync_WithNoExtras_ReturnsNotFound**
   - Setup: Empty list from repository
   - Assert: Returns NotFound

4. **ReorderAsync_WithPartialIds_ReturnsBadRequest**
   - Setup: 3 extras exist, request only has 2
   - Assert: Returns BadRequest

5. **CreateAsync_WithSortOrder_SetsSortOrder**
   - Verify SortOrder is mapped from request to entity

6. **UpdateAsync_WithSortOrder_UpdatesSortOrder**
   - Verify SortOrder is updated from request

#### Validator Tests (`CampEditionExtrasValidatorTests.cs`):

1. **CreateValidator_NegativeSortOrder_ShouldFail**
2. **CreateValidator_ZeroSortOrder_ShouldPass**
3. **CreateValidator_PositiveSortOrder_ShouldPass**
4. **UpdateValidator_NegativeSortOrder_ShouldFail**
5. **ReorderValidator_EmptyOrderedIds_ShouldFail**
6. **ReorderValidator_ValidOrderedIds_ShouldPass**

**Testing patterns**: Follow existing test file conventions — xUnit + FluentAssertions + NSubstitute, AAA pattern, descriptive names `MethodName_StateUnderTest_ExpectedBehavior`.

---

### Step 9: Update Technical Documentation

- **Action**: Update data model and documentation

**Implementation Steps**:

1. **Update `ai-specs/specs/data-model.md`** — Add `sortOrder` field to `CampEditionExtra` entity:
   ```
   - `sortOrder`: Display order for the extra within its camp edition (required, integer >= 0, default: 0)
   ```
   Add it after the `maxQuantity` field (line ~526). Also update the Mermaid diagram at the bottom of the file.

2. **Update `ai-specs/specs/data-model.md`** — Add validation rule:
   ```
   - SortOrder must be >= 0
   ```

3. **Verify auto-generated OpenAPI** — After running the app, the new `SortOrder` field and reorder endpoint should appear in the Swagger UI automatically (Minimal API auto-discovery).

---

## Implementation Order

1. Step 0: Create Feature Branch
2. Step 1: Update Entity Model (`CampsModels.cs`)
3. Step 2: Update EF Core Configuration (`CampEditionExtraConfiguration.cs`)
4. Step 3: Update Validators (`CampsValidators.cs`)
5. Step 4: Update Repository (`CampEditionExtrasRepository.cs`)
6. Step 5: Update Service (`CampEditionExtrasService.cs`)
7. Step 6: Add Reorder Endpoint (`CampsEndpoints.cs`)
8. Step 7: Create EF Core Migration
9. Step 8: Write Unit Tests
10. Step 9: Update Technical Documentation

## Testing Checklist

- [ ] All existing `CampEditionExtrasServiceTests` still pass
- [ ] All existing `CampEditionExtrasValidatorTests` still pass
- [ ] New reorder service tests pass (valid, mismatched, empty, partial)
- [ ] New validator tests pass (SortOrder validation, reorder validation)
- [ ] EF Core migration applies and rolls back cleanly
- [ ] Manual API test: `GET /extras` returns items sorted by `SortOrder`
- [ ] Manual API test: `PUT /extras/reorder` reorders successfully
- [ ] Manual API test: `POST /extras` with `SortOrder` creates correctly
- [ ] Manual API test: `PUT /extras/{id}` with `SortOrder` updates correctly
- [ ] 90%+ code coverage maintained

## Error Response Format

All responses use the `ApiResponse<T>` envelope:

| Scenario | HTTP Status | Response |
|----------|-------------|----------|
| Reorder success | 200 | `ApiResponse<string>.Success("Extras reordered successfully")` |
| No extras found | 404 | `ApiResponse<object>.Error("No extras found for this camp edition")` |
| ID mismatch | 400 | `ApiResponse<object>.Error("Ordered IDs must contain exactly all extras...")` |
| Validation error | 400 | Standard FluentValidation error response |
| Create/Update with SortOrder | 200/201 | Standard extra response with `SortOrder` field |

## Dependencies

- **NuGet packages**: No new packages required
- **EF Core migration**: `dotnet ef migrations add AddSortOrderToCampEditionExtras`

## Notes

- **Backward compatibility**: Default `SortOrder = 0` + `ThenBy(CreatedAt)` ensures existing extras maintain their current display order without any data changes
- **Data migration**: The optional SQL in Step 7 assigns sequential `SortOrder` values based on `CreatedAt` — this is recommended so existing extras get distinct sort values immediately
- **All code and artifacts in English** as per `base-standards.mdc`
- **No RGPD/GDPR impact** — `SortOrder` is a non-sensitive display field
- **Pattern consistency**: This implementation exactly mirrors `CampEditionAccommodation.SortOrder` for codebase consistency

## Next Steps After Implementation

1. Frontend implementation (separate ticket) — add drag-and-drop or arrow controls to `CampEditionExtrasList.vue`
2. Integration testing with the frontend reorder flow
3. Verify reorder endpoint is accessible in Swagger documentation
