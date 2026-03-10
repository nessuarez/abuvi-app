# Payment Concept/Line Items — Enriched User Story

## Summary

Add a structured concept description to each payment (installment) that provides a human-readable, line-by-line breakdown of what that payment covers. This enables tracking registration changes over time and understanding how each payment maps to specific attendees and extras.

## Problem

Currently, payments only have a `TransferConcept` field (e.g., `"CAMP-FAM-1"`) which is a bank reference, and an `AdminNotes` free-text field. There is no structured or automatic description of **what** each payment covers — which people, which periods, at what price. When registrations change (members added/removed, periods changed), it's impossible to trace how those changes affected existing payments without manually inspecting the full registration history.

## Solution

### New Field: `ConceptLines` on Payment

Add a new persisted field to the `Payment` entity that stores a JSON array of line items describing what the payment covers at the moment it was created or recalculated.

### Data Model Changes

**Payment entity** — add:

```csharp
public string? ConceptLinesSerialized { get; set; }  // JSON-serialized list
```

**ConceptLine DTO** (new value object, not a DB entity):

```csharp
public record PaymentConceptLine
{
    public string PersonFullName { get; init; }    // e.g., "Juan García López"
    public string AgeCategory { get; init; }       // "Adulto" | "Niño" | "Bebé"
    public string AttendancePeriod { get; init; }   // "Completo" | "1ª Semana" | "2ª Semana" | "Fin de semana"
    public decimal IndividualAmount { get; init; }  // Full price for this person
    public decimal AmountInPayment { get; init; }   // Amount attributed to this payment
    public decimal Percentage { get; init; }        // % of IndividualAmount in this payment (e.g., 50 for P1)
}
```

**ExtraConceptLine DTO** (for P3 - extras installment):

```csharp
public record PaymentExtraConceptLine
{
    public string ExtraName { get; init; }          // e.g., "Camiseta"
    public int Quantity { get; init; }              // For PerPerson: equals member count
    public decimal UnitPrice { get; init; }
    public decimal TotalAmount { get; init; }
    public string? UserInput { get; init; }         // e.g., "Talla M"
    public string PricingType { get; init; }        // "PerPerson" | "PerFamily"
}
```

### Database Changes

**Table `payments`** — add column:

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `concept_lines` | `text` | YES | JSON array of line items |

**EF Core Configuration** (`PaymentConfiguration.cs`):

```csharp
builder.Property(p => p.ConceptLinesSerialized)
    .HasColumnName("concept_lines")
    .HasColumnType("text")
    .IsRequired(false);
```

### Generation Logic

#### For Installments P1 and P2 (Base Amount)

When `PaymentsService.CreateInstallmentsAsync()` creates P1 and P2, generate concept lines from `registration.Members`:

```
P1 amount = Math.Ceiling(baseTotalAmount / 2)
P2 amount = baseTotalAmount - P1.amount
```

For each `RegistrationMember`:

- `PersonFullName` = `FamilyMember.FirstName + " " + FamilyMember.LastName`
- `AgeCategory` = mapped from enum (Baby→"Bebé", Child→"Niño", Adult→"Adulto")
- `AttendancePeriod` = mapped from enum (Complete→"Completo", FirstWeek→"1ª Semana", SecondWeek→"2ª Semana", WeekendVisit→"Fin de semana")
- `IndividualAmount` = `member.IndividualAmount`
- For P1: `Percentage` = `(P1.Amount / baseTotalAmount) * 100`, `AmountInPayment` = `Math.Ceiling(member.IndividualAmount * P1.Amount / baseTotalAmount)` (with rounding adjustment on last member)
- For P2: `Percentage` = `(P2.Amount / baseTotalAmount) * 100`, `AmountInPayment` = remaining after P1

#### For Installment P3 (Extras) — Enfoque Agregado

When `PaymentsService.SyncExtrasInstallmentAsync()` creates/updates P3, generate extra concept lines from `registration.Extras` using an **aggregated approach**: one line per extra type, not per person.

This is the only viable approach with the current data model, since `RegistrationExtra` stores `quantity` (number of units) but does **not** track which specific person each unit belongs to. For `PerPerson` extras, `quantity` equals the number of members — the line shows the total, not individual attribution.

For each `RegistrationExtra`:

- `ExtraName` = name from `CampEditionExtra`
- `Quantity` = `extra.Quantity` (for `PerPerson` extras, this is the member count)
- `UnitPrice` = `extra.UnitPrice`
- `TotalAmount` = `extra.TotalAmount`
- `UserInput` = `extra.UserInput` (if any)
- `PricingType` = `"PerPerson"` | `"PerFamily"` (informational, from `CampEditionExtra`)

#### On Recalculation (`SyncBaseInstallmentsAsync` / `SyncExtrasInstallmentAsync`)

When members or extras change and installments are recalculated, **regenerate the concept lines** with the new data. This captures the current state of the registration at each recalculation, providing a snapshot of what the payment covers after the change.

### API Response Changes

**PaymentResponse DTO** — add:

```csharp
public List<PaymentConceptLineDto>? ConceptLines { get; init; }
public List<PaymentExtraConceptLineDto>? ExtraConceptLines { get; init; }
```

These are deserialized from `ConceptLinesSerialized` when building the response. The response should present them as structured objects, not raw JSON.

**PaymentConceptLineDto:**

```csharp
public record PaymentConceptLineDto(
    string PersonFullName,
    string AgeCategory,
    string AttendancePeriod,
    decimal IndividualAmount,
    decimal AmountInPayment,
    decimal Percentage
);
```

**PaymentExtraConceptLineDto:**

```csharp
public record PaymentExtraConceptLineDto(
    string ExtraName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalAmount,
    string? UserInput,
    string PricingType          // "PerPerson" | "PerFamily"
);
```

### Display Example

**P1 (Primer plazo — 50% base):**

```
- Juan García López | Adulto | Completo | 400€ (50%) → 200€
- María García López | Adulto | Completo | 400€ (50%) → 200€
- Pablo García López | Niño | 1ª Semana | 150€ (50%) → 75€
Total: 475€
```

**P2 (Segundo plazo — 50% base):**

```
- Juan García López | Adulto | Completo | 400€ (50%) → 200€
- María García López | Adulto | Completo | 400€ (50%) → 200€
- Pablo García López | Niño | 1ª Semana | 150€ (50%) → 75€
Total: 475€
```

**P3 (Extras) — Enfoque agregado, una línea por tipo de extra:**

```
- Camiseta (por persona) x3 @ 15€ = 45€
- Seguro viaje (por familia) x1 @ 50€ = 50€
- Excursión (por persona) x3 @ 25€ = 75€
Total: 170€
```

> **Nota sobre extras `PerPerson`**: El modelo actual (`RegistrationExtra`) solo almacena la cantidad total, no la asignación individual por persona. Por tanto, el enfoque agregado es el único viable sin cambios estructurales. La `quantity` para extras `PerPerson` coincide con el número de miembros de la inscripción.

## Files to Modify

| File | Change |
|------|--------|
| `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs` | Add `ConceptLinesSerialized` property to `Payment` entity |
| `src/Abuvi.API/Features/Payments/PaymentsModels.cs` | Add `PaymentConceptLine`, `PaymentExtraConceptLine` records and response DTOs |
| `src/Abuvi.API/Data/Configurations/PaymentConfiguration.cs` | Add `concept_lines` column mapping |
| `src/Abuvi.API/Features/Payments/PaymentsService.cs` | Generate concept lines in `CreateInstallmentsAsync`, `SyncBaseInstallmentsAsync`, `SyncExtrasInstallmentAsync` |
| `src/Abuvi.API/Features/Payments/PaymentsEndpoints.cs` | Deserialize and include concept lines in payment responses |
| EF Migration | New migration for `concept_lines` column |

## Acceptance Criteria

1. When installments P1/P2 are created, `ConceptLines` is populated with one line per registration member showing: full name, age category, attendance period, individual full price, amount attributed to this installment, and percentage.
2. When installment P3 is created/updated, `ConceptLines` is populated with one line per registration extra showing: name, quantity, unit price, total amount, and user input if any.
3. When `SyncBaseInstallmentsAsync` recalculates P1/P2 (e.g., member added/removed), concept lines are regenerated to reflect the new state.
4. When `SyncExtrasInstallmentAsync` recalculates P3 (e.g., extra added/removed), concept lines are regenerated.
5. Payment API responses include the structured concept lines (not raw JSON).
6. Existing payments (created before this feature) have `null` concept lines — this is acceptable and should not cause errors.
7. A new EF migration adds the `concept_lines` column to the `payments` table.

## Non-Functional Requirements

- **Performance**: Concept lines are generated at write time (not computed at read time), so read performance is unaffected.
- **Data size**: JSON serialization uses `System.Text.Json` with default options. Expected payload per payment: ~500 bytes for a typical family of 4.
- **Backward compatibility**: The field is nullable; existing payments will return `null` for concept lines.

## Testing

- Unit test: Verify concept line generation for P1/P2 with mixed age categories and attendance periods.
- Unit test: Verify concept line generation for P3 with multiple extras.
- Unit test: Verify concept lines are regenerated after `SyncBaseInstallmentsAsync`.
- Integration test: Verify payment API response includes structured concept lines.
