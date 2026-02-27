# Camp Edition Extras Management

## Overview

This specification covers the implementation of the Camp Edition Extras feature, which allows board members to define and manage optional add-ons or services for specific camp editions (e.g., t-shirts, excursions, meals).

**Related Specs:**

- Parent: [camp-crud-enriched.md](../feat-camps-definition/camp-crud-enriched.md)
- Implementation Plan: [IMPLEMENTATION_PLAN.md](../feat-camps-definition/IMPLEMENTATION_PLAN.md)
- Dependencies: [camp-editions-management.md](../feat-camp-editions-management/camp-editions-management.md)

**Status:** ❌ NOT STARTED - Database table exists, business logic needed

**Includes:** Registration-side integration (merged from `feat-camp-edition-extras-registration`)

---

## Feature Description

Camp Edition Extras are additional items or services that can be purchased as part of camp registration. Examples include:

- T-shirts, hoodies, merchandise
- Optional excursions or activities
- Meal upgrades
- Insurance
- Transportation

### Key Characteristics

1. **Per-Edition Configuration**: Each camp edition can have its own extras
2. **Multiple Pricing Models**: Support for different pricing strategies
3. **Quantity Limits**: Optional maximum quantity limits
4. **Required vs Optional**: Some extras can be mandatory
5. **Active/Inactive State**: Can be enabled/disabled without deletion

---

## Database Schema

**Status:** ✅ Table exists in migration

The `camp_edition_extras` table includes:

```sql
CREATE TABLE camp_edition_extras (
    id UUID PRIMARY KEY,
    camp_edition_id UUID NOT NULL REFERENCES camp_editions(id) ON DELETE CASCADE,
    name VARCHAR(200) NOT NULL,
    description VARCHAR(1000),
    price DECIMAL(10,2) NOT NULL CHECK (price >= 0),
    pricing_type VARCHAR(20) NOT NULL,      -- PerPerson, PerFamily
    pricing_period VARCHAR(20) NOT NULL,     -- OneTime, PerDay
    is_required BOOLEAN NOT NULL DEFAULT false,
    is_active BOOLEAN NOT NULL DEFAULT true,
    max_quantity INT CHECK (max_quantity IS NULL OR max_quantity > 0),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX IX_camp_edition_extras_camp_edition_id
    ON camp_edition_extras(camp_edition_id);
```

---

## Pricing Models

### Pricing Type

| Type | Description | Example |
|------|-------------|---------|
| `PerPerson` | Charged per person | T-shirt: €15/person |
| `PerFamily` | Charged once per family | Insurance: €30/family |

### Pricing Period

| Period | Description | Example |
|--------|-------------|---------|
| `OneTime` | Single charge | T-shirt: €15 (one-time) |
| `PerDay` | Charged per day of camp | Lunch: €8/day × 10 days = €80 |

### Combined Examples

| Extra | Pricing Type | Pricing Period | Price | Calculation |
|-------|--------------|----------------|-------|-------------|
| T-Shirt | PerPerson | OneTime | €15 | Family of 4: €15 × 4 = €60 |
| Camp Insurance | PerFamily | OneTime | €30 | Family of 4: €30 × 1 = €30 |
| Daily Lunch | PerPerson | PerDay | €8 | Family of 4, 10 days: €8 × 4 × 10 = €320 |
| Family Photo Album | PerFamily | OneTime | €25 | Family of 4: €25 × 1 = €25 |

---

## Domain Models

### Entities

```csharp
public class CampEditionExtra
{
    public Guid Id { get; set; }
    public Guid CampEditionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public ExtraPricingType PricingType { get; set; }
    public ExtraPricingPeriod PricingPeriod { get; set; }
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; }
    public int? MaxQuantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public CampEdition CampEdition { get; set; } = null!;
}

public enum ExtraPricingType
{
    PerPerson,
    PerFamily
}

public enum ExtraPricingPeriod
{
    OneTime,
    PerDay
}
```

### DTOs

```csharp
public record CampEditionExtraResponse(
    Guid Id,
    Guid CampEditionId,
    string Name,
    string? Description,
    decimal Price,
    ExtraPricingType PricingType,
    ExtraPricingPeriod PricingPeriod,
    bool IsRequired,
    bool IsActive,
    int? MaxQuantity,
    int? CurrentQuantitySold,    // Calculated from registrations
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateCampEditionExtraRequest(
    Guid CampEditionId,
    string Name,
    string? Description,
    decimal Price,
    ExtraPricingType PricingType,
    ExtraPricingPeriod PricingPeriod,
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

---

## Business Logic

### Service: `CampEditionExtrasService`

#### Create Extra

```csharp
public async Task<CampEditionExtraResponse> CreateAsync(
    CreateCampEditionExtraRequest request,
    CancellationToken ct)
{
    // Verify camp edition exists
    var edition = await _editionsRepository.GetByIdAsync(
        request.CampEditionId, ct);

    if (edition == null)
        throw new NotFoundException("Camp edition not found");

    // Cannot add extras to completed/closed editions
    if (edition.Status is CampEditionStatus.Completed or CampEditionStatus.Closed)
    {
        throw new InvalidOperationException(
            "Cannot add extras to completed or closed editions");
    }

    var extra = new CampEditionExtra
    {
        Id = Guid.NewGuid(),
        CampEditionId = request.CampEditionId,
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

    await _repository.AddAsync(extra, ct);

    return extra.ToResponse(currentQuantitySold: 0);
}
```

#### Update Extra

```csharp
public async Task<CampEditionExtraResponse> UpdateAsync(
    Guid id,
    UpdateCampEditionExtraRequest request,
    CancellationToken ct)
{
    var extra = await _repository.GetByIdAsync(id, ct);

    if (extra == null)
        throw new NotFoundException("Extra not found");

    // Get current usage count
    var quantitySold = await _repository.GetQuantitySoldAsync(id, ct);

    // Warn if reducing max_quantity below current sold
    if (request.MaxQuantity.HasValue &&
        quantitySold > request.MaxQuantity.Value)
    {
        throw new InvalidOperationException(
            $"Cannot reduce max quantity to {request.MaxQuantity} when " +
            $"{quantitySold} units have already been sold");
    }

    // Cannot change price if already sold (warn)
    if (quantitySold > 0 && request.Price != extra.Price)
    {
        throw new InvalidOperationException(
            "Cannot change price for an extra that has already been purchased. " +
            "Consider creating a new extra instead.");
    }

    extra.Name = request.Name;
    extra.Description = request.Description;
    extra.Price = request.Price;
    extra.IsRequired = request.IsRequired;
    extra.IsActive = request.IsActive;
    extra.MaxQuantity = request.MaxQuantity;
    extra.UpdatedAt = DateTime.UtcNow;

    await _repository.UpdateAsync(extra, ct);

    return extra.ToResponse(quantitySold);
}
```

#### Delete Extra

```csharp
public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
{
    var extra = await _repository.GetByIdAsync(id, ct);

    if (extra == null)
        return false;

    // Check if extra is used in any registrations
    var quantitySold = await _repository.GetQuantitySoldAsync(id, ct);

    if (quantitySold > 0)
    {
        throw new InvalidOperationException(
            $"Cannot delete extra '{extra.Name}' because it has been " +
            $"selected in {quantitySold} registration(s). " +
            "Consider deactivating it instead.");
    }

    await _repository.DeleteAsync(id, ct);

    return true;
}
```

#### List Extras for Edition

```csharp
public async Task<List<CampEditionExtraResponse>> GetByEditionAsync(
    Guid campEditionId,
    bool? activeOnly,
    CancellationToken ct)
{
    var extras = await _repository.GetByCampEditionAsync(
        campEditionId, activeOnly, ct);

    var result = new List<CampEditionExtraResponse>();

    foreach (var extra in extras)
    {
        var quantitySold = await _repository.GetQuantitySoldAsync(
            extra.Id, ct);

        result.Add(extra.ToResponse(quantitySold));
    }

    return result;
}
```

#### Validate Quantity Availability

```csharp
public async Task<bool> IsAvailableAsync(
    Guid extraId,
    int requestedQuantity,
    CancellationToken ct)
{
    var extra = await _repository.GetByIdAsync(extraId, ct);

    if (extra == null || !extra.IsActive)
        return false;

    if (!extra.MaxQuantity.HasValue)
        return true; // Unlimited

    var quantitySold = await _repository.GetQuantitySoldAsync(extraId, ct);
    var available = extra.MaxQuantity.Value - quantitySold;

    return available >= requestedQuantity;
}
```

---

## API Endpoints

All endpoints require `Board/Admin` authorization except where noted.

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/camps/editions/{editionId}/extras` | Create extra | Board+ |
| GET | `/api/camps/editions/{editionId}/extras` | List extras | Member+ |
| GET | `/api/camps/editions/extras/{id}` | Get extra by ID | Member+ |
| PUT | `/api/camps/editions/extras/{id}` | Update extra | Board+ |
| DELETE | `/api/camps/editions/extras/{id}` | Delete extra | Board+ |
| PATCH | `/api/camps/editions/extras/{id}/activate` | Activate extra | Board+ |
| PATCH | `/api/camps/editions/extras/{id}/deactivate` | Deactivate extra | Board+ |

### Example Requests/Responses

#### Create Extra

**Request:** `POST /api/camps/editions/{editionId}/extras`

```json
{
  "campEditionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Camp T-Shirt",
  "description": "Official camp t-shirt with logo",
  "price": 15.00,
  "pricingType": "PerPerson",
  "pricingPeriod": "OneTime",
  "isRequired": false,
  "maxQuantity": 100
}
```

**Response:** `201 Created`

```json
{
  "success": true,
  "data": {
    "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "campEditionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Camp T-Shirt",
    "description": "Official camp t-shirt with logo",
    "price": 15.00,
    "pricingType": "PerPerson",
    "pricingPeriod": "OneTime",
    "isRequired": false,
    "isActive": true,
    "maxQuantity": 100,
    "currentQuantitySold": 0,
    "createdAt": "2026-02-16T10:30:00Z",
    "updatedAt": "2026-02-16T10:30:00Z"
  }
}
```

#### List Extras

**Request:** `GET /api/camps/editions/{editionId}/extras?activeOnly=true`

**Response:** `200 OK`

```json
{
  "success": true,
  "data": [
    {
      "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
      "name": "Camp T-Shirt",
      "price": 15.00,
      "pricingType": "PerPerson",
      "pricingPeriod": "OneTime",
      "isRequired": false,
      "isActive": true,
      "maxQuantity": 100,
      "currentQuantitySold": 23
    },
    {
      "id": "8d0f7780-8536-51ef-c4fd-f18ad2g01bf8",
      "name": "Daily Lunch Package",
      "price": 8.00,
      "pricingType": "PerPerson",
      "pricingPeriod": "PerDay",
      "isRequired": true,
      "isActive": true,
      "maxQuantity": null,
      "currentQuantitySold": 45
    }
  ]
}
```

---

## Validation Rules

### CreateCampEditionExtraRequestValidator

```csharp
public class CreateCampEditionExtraRequestValidator
    : AbstractValidator<CreateCampEditionExtraRequest>
{
    public CreateCampEditionExtraRequestValidator()
    {
        RuleFor(x => x.CampEditionId)
            .NotEmpty()
            .WithMessage("Camp edition ID is required");

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Name is required and must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description must not exceed 1000 characters");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(10, 2, true)
            .WithMessage("Price must be 0 or greater");

        RuleFor(x => x.PricingType)
            .IsInEnum()
            .WithMessage("Invalid pricing type");

        RuleFor(x => x.PricingPeriod)
            .IsInEnum()
            .WithMessage("Invalid pricing period");

        RuleFor(x => x.MaxQuantity)
            .GreaterThan(0)
            .When(x => x.MaxQuantity.HasValue)
            .WithMessage("Max quantity must be greater than 0 if specified");
    }
}
```

---

## Test Cases

### Unit Tests

#### Service Tests

```csharp
public class CampEditionExtrasServiceTests
{
    [Fact]
    public async Task CreateAsync_WithValidData_CreatesExtra()
    {
        // Test successful creation
    }

    [Fact]
    public async Task CreateAsync_WhenEditionClosed_ThrowsException()
    {
        // Cannot add extras to closed editions
    }

    [Fact]
    public async Task UpdateAsync_WhenPriceChanged_AndAlreadySold_ThrowsException()
    {
        // Prevent price changes for sold items
    }

    [Fact]
    public async Task UpdateAsync_WhenMaxQuantityReduced_BelowSold_ThrowsException()
    {
        // Cannot reduce below sold quantity
    }

    [Fact]
    public async Task DeleteAsync_WhenExtraInUse_ThrowsException()
    {
        // Prevent deletion of used extras
    }

    [Fact]
    public async Task DeleteAsync_WhenNotInUse_DeletesSuccessfully()
    {
        // Allow deletion if not used
    }

    [Fact]
    public async Task IsAvailableAsync_WhenUnlimited_ReturnsTrue()
    {
        // Null max_quantity means unlimited
    }

    [Fact]
    public async Task IsAvailableAsync_WhenQuantityExceeded_ReturnsFalse()
    {
        // Enforce max_quantity limit
    }
}
```

#### Pricing Calculation Tests

```csharp
public class PricingCalculationTests
{
    [Theory]
    [InlineData(ExtraPricingType.PerPerson, ExtraPricingPeriod.OneTime, 15, 4, 1, 60)]      // T-shirt × 4 people
    [InlineData(ExtraPricingType.PerFamily, ExtraPricingPeriod.OneTime, 30, 4, 1, 30)]      // Insurance × 1 family
    [InlineData(ExtraPricingType.PerPerson, ExtraPricingPeriod.PerDay, 8, 4, 10, 320)]      // Lunch × 4 people × 10 days
    [InlineData(ExtraPricingType.PerFamily, ExtraPricingPeriod.PerDay, 5, 4, 10, 50)]       // Daily activity × 1 family × 10 days
    public void CalculatePrice_WithDifferentModels_ReturnsCorrectAmount(
        ExtraPricingType pricingType,
        ExtraPricingPeriod pricingPeriod,
        decimal unitPrice,
        int familySize,
        int campDays,
        decimal expectedTotal)
    {
        // Test pricing calculation logic
        var calculator = new ExtraPriceCalculator();
        var total = calculator.Calculate(
            unitPrice, pricingType, pricingPeriod, familySize, campDays);

        total.Should().Be(expectedTotal);
    }
}
```

### Integration Tests

```csharp
public class CampEditionExtrasEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task CreateExtra_AsBoard_ReturnsCreated()
    {
        // Test endpoint authorization and creation
    }

    [Fact]
    public async Task CreateExtra_AsMember_ReturnsForbidden()
    {
        // Test authorization
    }

    [Fact]
    public async Task ListExtras_IncludesQuantitySold()
    {
        // Verify quantity tracking
    }

    [Fact]
    public async Task DeleteExtra_WhenInUse_ReturnsBadRequest()
    {
        // Test constraint enforcement
    }
}
```

---

## Implementation Tasks

### Sprint 1: Core CRUD (3-4 days)

1. [ ] Create `CampEditionExtra` entity configuration
2. [ ] Verify migration includes all needed columns
3. [ ] Create `ICampEditionExtrasRepository` interface
4. [ ] Implement `CampEditionExtrasRepository`
5. [ ] Create validators (`CreateCampEditionExtraRequestValidator`, etc.)
6. [ ] Implement `CampEditionExtrasService`
7. [ ] Create endpoints in `CampEditionExtrasEndpoints.cs`
8. [ ] Register services in DI container
9. [ ] Unit tests for service (15+ tests)
10. [ ] Unit tests for validators (10+ tests)

### Sprint 2: Quantity Tracking & Constraints (2 days)

1. [ ] Implement `GetQuantitySoldAsync` in repository
2. [ ] Add quantity validation to registration flow
3. [ ] Implement concurrent registration handling
4. [ ] Add tests for quantity limits
5. [ ] Add tests for deletion constraints
6. [ ] Integration tests for endpoints

### Sprint 3: Pricing Calculator (1-2 days)

1. [ ] Create `ExtraPriceCalculator` utility
2. [ ] Implement calculation for all pricing models
3. [ ] Add comprehensive pricing tests (all combinations)
4. [ ] Integrate calculator into registration pricing
5. [ ] Document pricing examples

---

## Integration Points

### Registration System

When a family registers for a camp, extras are handled as follows:

1. **Required Extras**: Automatically added to registration — pre-selected and cannot be deselected
2. **Optional Extras**: Selected by the family during the wizard
3. **Pricing**: Calculated using `ExtraPriceCalculator`, shown as separate line items in the pricing breakdown
4. **Quantity**: Validated against `max_quantity` (capacity limits)
5. **Price Snapshot**: `unit_price` is captured at registration time to protect against later price changes
6. **Payment**: Extra costs are paid in a second payment period (shown as additional line items in the final budget)

**Registration Extras Table** (already exists):

```sql
CREATE TABLE registration_extras (
    id UUID PRIMARY KEY,
    registration_id UUID NOT NULL REFERENCES registrations(id),
    camp_edition_extra_id UUID NOT NULL REFERENCES camp_edition_extras(id),
    quantity INT NOT NULL DEFAULT 1,
    unit_price DECIMAL(10,2) NOT NULL,
    total_price DECIMAL(10,2) NOT NULL
);
```

### Registration API Changes

#### Request: `CreateRegistrationRequest` extension

```typescript
interface RegistrationExtraSelection {
  campEditionExtraId: string
  quantity: number       // 1 for boolean, N for quantity
}

interface CreateRegistrationRequest {
  // ...existing fields...
  extras?: RegistrationExtraSelection[]
}
```

#### Response: `RegistrationResponse` extension

```typescript
interface RegistrationExtraDetail {
  campEditionExtraId: string
  name: string
  quantity: number
  unitPrice: number
  totalPrice: number
}

interface RegistrationResponse {
  // ...existing fields...
  extras: RegistrationExtraDetail[]
}
```

### Registration Scope

The registration-side integration includes:

1. **Registration wizard**: Show available extras for the edition, allow family to select/configure them
2. **Registration service**: Save selected extras to `registration_extras` table
3. **Registration response**: Include selected extras with costs in the response
4. **Pricing integration**: Add extra costs to the `PricingBreakdown`

### Google Forms Fields Becoming Extras

These fields from the Google Forms registration are modeled as `CampEditionExtra` seed data for 2026:

| Google Forms Field | Extra Configuration | InputType |
| ------------------ | ------------------- | --------- |
| Vegetarian menu count | `Name: "Menú vegetariano"`, `Price: 0`, `PricingType: PerPerson`, `PricingPeriod: OneTime` | Quantity |
| Truck usage | `Name: "Transporte furgoneta"`, `Price: 0`, `PricingType: PerFamily`, `PricingPeriod: OneTime` | Boolean |

These are seeded with `Price = 0`. In future years, the admin can set a price via the extras CRUD.

### Frontend Considerations

- Show extras in the wizard Step 2 (Extras) — which currently exists but may be empty
- Boolean extras: toggle switch
- Quantity extras: number input with min/max
- Show unit price and calculated total per extra
- Show "Gratuito" or "0 €" for free extras
- Required extras are pre-selected and cannot be deselected

---

## Acceptance Criteria

- [ ] All pricing types work correctly (PerPerson, PerFamily, OneTime, PerDay)
- [ ] Quantity limits enforced
- [ ] Cannot delete extras in use
- [ ] Required extras automatically added to registrations
- [ ] Extras can be activated/deactivated
- [ ] Cannot change price of sold extras
- [ ] Cannot reduce max_quantity below sold amount
- [ ] 95%+ test coverage
- [ ] All endpoints documented in Swagger
- [ ] Manual E2E testing completed

---

## Success Metrics

- [ ] All 25+ test cases passing
- [ ] Zero failing integration tests
- [ ] API documentation complete with examples
- [ ] Performance: List extras query < 50ms
- [ ] Successful manual testing of all pricing combinations

---

## Related User Stories

- **US-CAMP-203**: Manage Camp Edition Extras

---

## Notes

- The `camp_edition_extras` table already exists in the database
- Integration with the registration system will require updates to the registration flow
- Consider adding a `sort_order` field for display ordering (future enhancement)
- Consider adding images/icons for extras (future enhancement)

---

## Document Control

- **Version**: 1.1
- **Created**: 2026-02-16
- **Updated**: 2026-02-26 — Merged registration integration from `feat-camp-edition-extras-registration`
- **Status**: ❌ Not Started
- **Dependencies**: Camp Editions Management must be completed first; `feat-registration-extra-fields2` (guardian + preference fields) should be merged first
- **Parent Spec**: [camp-crud-enriched.md](../feat-camps-definition/camp-crud-enriched.md)
