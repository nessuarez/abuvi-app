---
name: Payments admin search filter by family/representative
description: Add text search by family name or representative name to the admin payments list, mirroring the registrations panel behavior
type: project
---

## User Story

As an admin/board user, I want to filter the payments list by family name or representative name so that I can quickly find payments for a specific family without scrolling or applying other filters.

## Context

The **registrations admin panel** (`RegistrationsAdminPanel.vue`) already has a debounced text search that sends a `search` query param to `GET /camp-editions/{id}/registrations`. The **payments admin panel** (`PaymentsAllList.vue`) has no equivalent — it only supports status, edition, installment period, and date filters.

The data model already supports this: `Payment → Registration → FamilyUnit (Name)` and `Payment → Registration → RegisteredByUser (FirstName, LastName)`.

---

## Acceptance Criteria

1. A text input labeled **"Buscar familia o representante..."** appears in the payments filters row (next to the existing filters).
2. Typing in the input filters the list server-side by `FamilyUnit.Name` OR `(RegisteredByUser.FirstName + " " + RegisteredByUser.LastName)`, case-insensitive, with 300 ms debounce.
3. Clicking "Limpiar filtros" clears the search input along with the other filters.
4. The filter combines with existing filters (status, edition, installment/dates) — all are applied simultaneously.
5. Pagination resets to page 1 when the search query changes.
6. Search sends the term in the query string as `Search=<term>` (consistent with the `AsParameters` binding in the endpoint).

---

## Files to Modify

### Backend

#### `src/Abuvi.API/Features/Payments/PaymentsModels.cs`
Add `Search` to `PaymentFilterRequest`:
```csharp
public record PaymentFilterRequest(
    PaymentStatus? Status = null,
    Guid? CampEditionId = null,
    int? InstallmentNumber = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? Search = null,   // NEW
    int Page = 1,
    int PageSize = 20
);
```

#### `src/Abuvi.API/Features/Payments/PaymentsRepository.cs`
In `GetFilteredAsync`, extend the LINQ query to join `RegisteredByUser` and apply the search filter. The existing query already includes `Registration → FamilyUnit` via `.Include`. Rewrite to use a LINQ join (same pattern as `RegistrationsRepository.GetAdminPagedAsync`) so the search fields are accessible before pagination:

```csharp
var query = from p in db.Payments.AsNoTracking()
            join r in db.Registrations on p.RegistrationId equals r.Id
            join fu in db.FamilyUnits on r.FamilyUnitId equals fu.Id
            join u in db.Users on r.RegisteredByUserId equals u.Id
            join ce in db.CampEditions on r.CampEditionId equals ce.Id
            join c in db.Camps on ce.CampId equals c.Id
            select new { Payment = p, FamilyName = fu.Name, RepFirstName = u.FirstName, RepLastName = u.LastName, CampEditionId = ce.Id };

// Apply existing filters on p.*
// Apply search filter:
if (!string.IsNullOrWhiteSpace(filter.Search))
{
    var term = filter.Search.Trim().ToLower();
    query = query.Where(x =>
        x.FamilyName.ToLower().Contains(term) ||
        (x.RepFirstName + " " + x.RepLastName).ToLower().Contains(term));
}
```

> **Note:** The return type still needs to be `(List<Payment> Items, int TotalCount)` to keep `IPaymentsRepository` stable. Project back to `p.Payment` for the final `.Select(x => x.Payment)`.

`IPaymentsRepository` does **not** need changes — the signature `GetFilteredAsync(PaymentFilterRequest filter, CancellationToken ct)` is unchanged; the filter record gains a new optional property.

### Frontend

#### `frontend/src/types/payment.ts`
Add `search` to `PaymentFilterParams`:
```ts
export interface PaymentFilterParams {
  status?: PaymentStatus
  campEditionId?: string
  installmentNumber?: number
  fromDate?: string
  toDate?: string
  search?: string   // NEW
  page?: number
  pageSize?: number
}
```

#### `frontend/src/composables/usePayments.ts`
In `getAllPayments`, append the search param:
```ts
if (filter.search) params.append('Search', filter.search)
```

#### `frontend/src/components/admin/PaymentsAllList.vue`
1. Import `useDebounceFn` from `@vueuse/core` and `InputText` from `primevue/inputtext` (already imported).
2. Add a `searchQuery` ref:
   ```ts
   const searchQuery = ref('')
   ```
3. Include `search` in the filter built inside `fetchPayments()`:
   ```ts
   if (searchQuery.value) filter.search = searchQuery.value
   ```
4. Add a debounced watcher (300 ms):
   ```ts
   const debouncedSearch = useDebounceFn(() => {
     currentPage.value = 1
     fetchPayments()
   }, 300)
   watch(searchQuery, debouncedSearch)
   ```
5. Reset `searchQuery` inside `resetFilters()`.
6. Add the input in the filters template row, before the clear-filters button:
   ```html
   <div>
     <label class="mb-1 block text-xs font-medium text-gray-600">Familia / Representante</label>
     <span class="p-input-icon-left">
       <i class="pi pi-search" />
       <InputText
         v-model="searchQuery"
         placeholder="Buscar familia o representante..."
         class="w-64"
       />
     </span>
   </div>
   ```

---

## Tests

### Backend — `src/Abuvi.Tests/Unit/Features/Payments/PaymentsRepository_FilterTests.cs`
Add test cases:
- `GetFilteredAsync_WhenSearchMatchesFamilyName_ReturnsMatchingPayments`
- `GetFilteredAsync_WhenSearchMatchesRepresentativeName_ReturnsMatchingPayments`
- `GetFilteredAsync_WhenSearchMatchesNoOne_ReturnsEmpty`
- `GetFilteredAsync_WhenSearchCombinedWithStatus_AppliesBothFilters`

These tests require seeding a `User` entity (for `RegisteredByUserId`) and updating the existing seed in `SeedBaseEntities()` to link `Registration.RegisteredByUserId` to a seeded user. Check the existing tests carefully — the current seeds may not include a `User`.

### Frontend — `frontend/src/components/admin/__tests__/PaymentsAllList.test.ts`
Add tests verifying:
- Search input is rendered in the filters row.
- Typing in the search input triggers a debounced API call with the `Search` param.
- `resetFilters` clears the search input and refetches without the param.

---

## Non-Functional Requirements

- **Performance**: The search filter runs server-side with pagination; no client-side filtering. The `FamilyUnit.Name` and `User.FirstName/LastName` columns should already be indexed or are part of small-cardinality joins — no additional DB index required for this scale.
- **Security**: Search input is bound via EF Core parameterized queries (`.Contains(term)` translates to `LIKE @p`); no SQL injection risk.
- **Consistency**: UI behavior and debounce timing (300 ms) match the registrations panel exactly.

---

## Out of Scope
- Searching by `TransferConcept` or email — can be a follow-up.
- Exporting filtered results — separate ticket.
