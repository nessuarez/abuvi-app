# Feature Spec: feat-camp-edition-extras-registration — Connect Extras to Registration Flow

## Status: Pending — Spec needs enrichment

## Origin

Extracted from `feat-registration-extra-fields2` (Google Forms fields #11 "Vegetarian menu" and #13 "Truck usage").

**Why extracted**: These fields (vegetarian count, truck usage) are better modeled as **generic extras** using the existing `CampEditionExtra` entity. Some years they may have costs, other years they're free. The `camp_edition_extras` and `registration_extras` tables already exist in the database.

---

## Problem Statement

The existing `CampEditionExtra` entity (see `feat-camp-edition-extras/camp-edition-extras.md`) defines optional add-ons per camp edition with flexible pricing. However, the registration flow does not yet allow families to select extras during registration. This ticket connects the two.

### Fields from Google Forms becoming Extras

| Google Forms Field | Extra Configuration | InputType |
| ------------------ | ------------------- | --------- |
| Vegetarian menu count | `Name: "Menú vegetariano"`, `Price: 0`, `PricingType: PerPerson`, `PricingPeriod: OneTime` | Quantity |
| Truck usage | `Name: "Transporte furgoneta"`, `Price: 0`, `PricingType: PerFamily`, `PricingPeriod: OneTime` | Boolean |

These are seeded as `CampEditionExtra` records for the 2026 edition with `Price = 0`. In future years, the admin can set a price via the extras CRUD.

---

## Existing Infrastructure

### Database Tables (already exist)

```sql
-- Defined in feat-camp-edition-extras
CREATE TABLE camp_edition_extras (
    id UUID PRIMARY KEY,
    camp_edition_id UUID NOT NULL REFERENCES camp_editions(id),
    name VARCHAR(200) NOT NULL,
    description VARCHAR(1000),
    price DECIMAL(10,2) NOT NULL CHECK (price >= 0),
    pricing_type VARCHAR(20) NOT NULL,      -- PerPerson, PerFamily
    pricing_period VARCHAR(20) NOT NULL,     -- OneTime, PerDay
    is_required BOOLEAN NOT NULL DEFAULT false,
    is_active BOOLEAN NOT NULL DEFAULT true,
    max_quantity INT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL
);

CREATE TABLE registration_extras (
    id UUID PRIMARY KEY,
    registration_id UUID NOT NULL REFERENCES registrations(id),
    camp_edition_extra_id UUID NOT NULL REFERENCES camp_edition_extras(id),
    quantity INT NOT NULL DEFAULT 1,
    unit_price DECIMAL(10,2) NOT NULL,
    total_price DECIMAL(10,2) NOT NULL
);
```

### Existing Spec

See `ai-specs/changes/feat-camp-edition-extras/camp-edition-extras.md` for the full CRUD spec including:

- Entity models and DTOs
- Service with create/update/delete/list operations
- Pricing calculator (`ExtraPriceCalculator`)
- Validation rules
- API endpoints

---

## Scope of This Ticket

This ticket implements the **registration-side integration**:

1. **Registration wizard**: Show available extras for the edition, allow family to select/configure them
2. **Registration service**: Save selected extras to `registration_extras` table
3. **Registration response**: Include selected extras with costs in the response
4. **Pricing integration**: Add extra costs to the `PricingBreakdown`

### NOT in scope

- CRUD for `CampEditionExtra` (covered by `feat-camp-edition-extras`)
- Admin UI for managing extras (covered by `feat-camp-edition-extras`)

---

## Key Business Rules

1. Required extras (`is_required = true`) are automatically added during registration
2. Optional extras are selected by the family during the wizard
3. Extra costs are shown as separate line items in the pricing breakdown
4. Extras with `max_quantity` respect capacity limits
5. Extras are paid in a second payment period (pricing shows them as additional line items in the final budget)
6. `unit_price` is captured at registration time (snapshot) to protect against later price changes

---

## API Changes (Proposed)

### Request: `CreateRegistrationRequest` extension

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

### Response: `RegistrationResponse` extension

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

---

## Frontend Considerations

- Show extras in the wizard Step 2 (Extras) — which currently exists but may be empty
- Boolean extras: toggle switch
- Quantity extras: number input with min/max
- Show unit price and calculated total per extra
- Show "Gratuito" or "0 €" for free extras
- Required extras are pre-selected and cannot be deselected

---

## Dependencies

- `feat-camp-edition-extras` (CRUD must be implemented first — entities, service, endpoints)
- `feat-registration-extra-fields2` (guardian + preference fields — should be merged first)

---

## Document Control

- **Created**: 2026-02-26
- **Status**: Pending — needs enrichment with `/enrich-us` before implementation planning
- **Origin**: Extracted from `feat-registration-extra-fields2`, builds on `feat-camp-edition-extras`
