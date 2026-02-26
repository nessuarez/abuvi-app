# Renew Current Camp Landing Page — Enriched Spec

## Summary

Redesign `CampPage.vue` (`/camp`) into a rich, modern landing page that showcases the current camp edition with full detail: photos, description, map, accommodation, extras, pricing, and status-aware registration CTAs. Requires backend enrichment of the `GET /api/camps/current` endpoint to return camp photos, extras, accommodation capacity, and Google Places data that is currently excluded from that response.

---

## Context & Current State

| Item | Current |
|---|---|
| Route | `/camp` → `CampPage.vue` |
| Data source | `GET /api/camps/editions/active` (Open status only) |
| Composable | `useCampEditions().getActiveEdition()` |
| Display | `ActiveEditionCard` — name, dates, pricing, capacity counter, notes |
| Missing | Photos, description, map, accommodation, extras, contact info, modern layout |

The richer endpoint `GET /api/camps/current` (smart status-priority fallback) already exists and is accessible to all members, but `CampPage.vue` does not use it. Its response DTO (`CurrentCampEditionResponse`) is also missing several Camp-level fields.

---

## Objectives

1. Switch `CampPage.vue` to use `GET /api/camps/current` instead of the active-edition endpoint.
2. Extend the `CurrentCampEditionResponse` DTO to include: camp photos (Google Places), camp description, contact info, accommodation capacity, and active extras.
3. Redesign `CampPage.vue` into a rich, multi-section landing page reusing existing components where possible.
4. Adapt status messaging appropriately (Open / Closed / Completed / no edition).

---

## Backend Changes

### 1. Extend `CurrentCampEditionResponse` DTO

**File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

Add the following fields to `CurrentCampEditionResponse`:

```csharp
public record CurrentCampEditionResponse(
    // --- existing fields (unchanged) ---
    Guid Id,
    Guid CampId,
    string CampName,
    string? CampLocation,
    string? CampFormattedAddress,
    decimal? CampLatitude,
    decimal? CampLongitude,
    int Year,
    DateTime StartDate,
    DateTime EndDate,
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool UseCustomAgeRanges,
    int? CustomBabyMaxAge,
    int? CustomChildMinAge,
    int? CustomChildMaxAge,
    int? CustomAdultMinAge,
    CampEditionStatus Status,
    int? MaxCapacity,
    int RegistrationCount,
    int? AvailableSpots,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,

    // --- NEW fields ---
    string? CampDescription,           // Camp.Description
    string? CampPhoneNumber,           // Camp.PhoneNumber (E.164)
    string? CampNationalPhoneNumber,   // Camp.NationalPhoneNumber (formatted for display)
    string? CampWebsiteUrl,            // Camp.WebsiteUrl
    string? CampGoogleMapsUrl,         // Camp.GoogleMapsUrl
    decimal? CampGoogleRating,         // Camp.GoogleRating
    int? CampGoogleRatingCount,        // Camp.GoogleRatingCount
    IReadOnlyList<CampPhotoResponse> CampPhotos, // Camp.Photos ordered by DisplayOrder (IsPrimary first)
    AccommodationCapacity? AccommodationCapacity,        // Edition override ?? Camp fallback
    int? CalculatedTotalBedCapacity,   // Computed from AccommodationCapacity
    IReadOnlyList<CampEditionExtraResponse> Extras       // Active extras, sorted by SortOrder
);
```

Add `CampEditionExtraResponse` record (if it doesn't already exist in a suitable form — check if `CampEditionExtra` entity already has a response DTO):

```csharp
public record CampEditionExtraResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string PricingType,     // "PerPerson" | "PerFamily"
    string PricingPeriod,   // "OneTime" | "PerDay"
    bool IsRequired,
    int? MaxQuantity,
    int CurrentQuantity,
    int SortOrder
);
```

### 2. Update Repository — `GetCurrentAsync`

**File:** `src/Abuvi.API/Features/Camps/CampEditionsRepository.cs`

In `GetCurrentAsync`, add `.ThenInclude(c => c.Photos)` and `.Include(e => e.Extras)` to both queries:

```csharp
public async Task<CampEdition?> GetCurrentAsync(int currentYear, CancellationToken cancellationToken = default)
{
    var currentYearEdition = await _context.CampEditions
        .AsNoTracking()
        .Include(e => e.Camp)
            .ThenInclude(c => c.Photos.OrderBy(p => p.IsPrimary ? 0 : 1).ThenBy(p => p.DisplayOrder))
        .Include(e => e.Extras.Where(x => x.IsActive).OrderBy(x => x.SortOrder))
        .Where(e => e.Year == currentYear && !e.IsArchived
            && (e.Status == CampEditionStatus.Open || e.Status == CampEditionStatus.Closed))
        .OrderByDescending(e => e.Status == CampEditionStatus.Open ? 1 : 0)
        .FirstOrDefaultAsync(cancellationToken);

    if (currentYearEdition != null)
        return currentYearEdition;

    var previousYear = currentYear - 1;
    return await _context.CampEditions
        .AsNoTracking()
        .Include(e => e.Camp)
            .ThenInclude(c => c.Photos.OrderBy(p => p.IsPrimary ? 0 : 1).ThenBy(p => p.DisplayOrder))
        .Include(e => e.Extras.Where(x => x.IsActive).OrderBy(x => x.SortOrder))
        .Where(e => e.Year == previousYear && !e.IsArchived
            && (e.Status == CampEditionStatus.Completed || e.Status == CampEditionStatus.Closed))
        .OrderByDescending(e => e.Status == CampEditionStatus.Completed ? 1 : 0)
        .FirstOrDefaultAsync(cancellationToken);
}
```

> **Note:** EF Core 5+ supports filtered includes. Confirm the EF Core version supports `.Include(e => e.Extras.Where(...))`. If not, filter in the service layer after loading all extras.

### 3. Update Service — `GetCurrentAsync`

**File:** `src/Abuvi.API/Features/Camps/CampEditionsService.cs`

Map the new fields in `GetCurrentAsync`. Accommodation priority: edition override first, then camp fallback.

```csharp
var accommodationCapacity = edition.GetAccommodationCapacity()
    ?? edition.Camp.GetAccommodationCapacity();

var calculatedBedCapacity = accommodationCapacity != null
    ? CalculateTotalBedCapacity(accommodationCapacity)
    : (int?)null;

return new CurrentCampEditionResponse(
    // ... existing fields ...
    CampDescription: edition.Camp.Description,
    CampPhoneNumber: edition.Camp.PhoneNumber,
    CampNationalPhoneNumber: edition.Camp.NationalPhoneNumber,
    CampWebsiteUrl: edition.Camp.WebsiteUrl,
    CampGoogleMapsUrl: edition.Camp.GoogleMapsUrl,
    CampGoogleRating: edition.Camp.GoogleRating,
    CampGoogleRatingCount: edition.Camp.GoogleRatingCount,
    CampPhotos: edition.Camp.Photos
        .Select(p => new CampPhotoResponse(p.Id, p.PhotoReference, p.PhotoUrl,
            p.Width, p.Height, p.AttributionName, p.AttributionUrl, p.Description, p.IsPrimary, p.DisplayOrder))
        .ToList(),
    AccommodationCapacity: accommodationCapacity,
    CalculatedTotalBedCapacity: calculatedBedCapacity,
    Extras: edition.Extras
        .Select(x => new CampEditionExtraResponse(x.Id, x.Name, x.Description, x.Price,
            x.PricingType.ToString(), x.PricingPeriod.ToString(), x.IsRequired,
            x.MaxQuantity, x.CurrentQuantity, x.SortOrder))
        .ToList()
);
```

> Check if `CalculateTotalBedCapacity` already exists in the service (it does in `CampsService` for the camp detail endpoint). Extract to a shared static helper or reuse if accessible.

### 4. Update API Documentation

**File:** `ai-specs/specs/api-endpoints.md`

Update the `GET /api/camps/current` response schema to reflect the new fields.

### 5. Tests

**File:** `src/Abuvi.Tests/Unit/Features/Camps/CampEditionsServiceTests.cs`

Add unit tests covering:
- `GetCurrentAsync` returns `CampPhotos` from `Camp.Photos` ordered (primary first, then by DisplayOrder)
- `GetCurrentAsync` returns `Extras` only for `IsActive == true`, ordered by `SortOrder`
- `AccommodationCapacity` uses edition override when present; falls back to camp capacity when edition has none
- `CalculatedTotalBedCapacity` is populated correctly when accommodation capacity is present

---

## Frontend Changes

### 1. Update TypeScript Types

**File:** `frontend/src/types/camp-edition.ts`

Add or extend `CurrentCampEditionResponse` to include the new backend fields:

```typescript
export interface CurrentCampEditionResponse {
  id: string
  campId: string
  campName: string
  campLocation: string | null
  campFormattedAddress: string | null
  campLatitude: number | null
  campLongitude: number | null
  year: number
  startDate: string
  endDate: string
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  useCustomAgeRanges: boolean
  customBabyMaxAge?: number
  customChildMinAge?: number
  customChildMaxAge?: number
  customAdultMinAge?: number
  status: CampEditionStatus
  maxCapacity?: number
  registrationCount: number
  availableSpots?: number
  notes?: string
  createdAt: string
  updatedAt: string
  // New fields
  campDescription: string | null
  campPhoneNumber: string | null
  campNationalPhoneNumber: string | null
  campWebsiteUrl: string | null
  campGoogleMapsUrl: string | null
  campGoogleRating: number | null
  campGoogleRatingCount: number | null
  campPhotos: CampPlacesPhoto[]     // reuse type from '@/types/camp'
  accommodationCapacity: AccommodationCapacity | null  // reuse from '@/types/camp'
  calculatedTotalBedCapacity: number | null
  extras: CampEditionExtra[]
}
```

> The `CampPlacesPhoto` and `AccommodationCapacity` types already exist in `frontend/src/types/camp.ts`.

### 2. Update Composable

**File:** `frontend/src/composables/useCampEditions.ts`

`fetchCurrentCampEdition` already calls `/camps/current` but types the result as `CampEdition`. Update it to use `CurrentCampEditionResponse`:

```typescript
const currentCampEdition = ref<CurrentCampEditionResponse | null>(null)

const fetchCurrentCampEdition = async (): Promise<void> => {
  // ... existing error handling ...
  const response = await api.get<ApiResponse<CurrentCampEditionResponse>>('/camps/current')
  // ...
}
```

### 3. Redesign `CampPage.vue`

**File:** `frontend/src/views/CampPage.vue`

Replace the current `getActiveEdition()` call with `fetchCurrentCampEdition()` and redesign the template.

**Data fetching:**
- On mount: call `fetchCurrentCampEdition()` and `getCurrentUserFamilyUnit()`
- Loading, error, and empty states remain (using PrimeVue `ProgressSpinner` and `Message`)

**Page structure (top to bottom):**

```
┌─────────────────────────────────────────────────┐
│  HERO SECTION                                   │
│  Camp name · Location · Year · Status badge     │
│  Date range + duration · Countdown (if Open)    │
│  Primary photo background (if available)        │
│  → CTA button (register / view registrations)   │
├─────────────────────────────────────────────────┤
│  CAPACITY BAR (if maxCapacity exists)           │
│  Progress: registrationCount / maxCapacity      │
├─────────────────────────────────────────────────┤
│  PHOTO GALLERY (if campPhotos.length > 0)       │
│  → Reuse <CampPlacesGallery :photos="...">      │
├─────────────────────────────────────────────────┤
│  ABOUT THE CAMP (if campDescription exists)     │
│  Description text                               │
├─────────────────────────────────────────────────┤
│  MAP (if campLatitude && campLongitude)         │
│  → Reuse <CampLocationMap :locations="...">     │
├─────────────────────────────────────────────────┤
│  ACCOMMODATION (if accommodationCapacity)       │
│  → Reuse <AccommodationCapacityDisplay>         │
├─────────────────────────────────────────────────┤
│  PRICING                                        │
│  → Reuse <PricingBreakdown>                     │
├─────────────────────────────────────────────────┤
│  OPTIONAL SERVICES / EXTRAS                     │
│  (if extras.length > 0)                         │
│  New component: <CampExtrasSection>             │
├─────────────────────────────────────────────────┤
│  CONTACT INFO                                   │
│  Address · Phone · Website · Rating             │
│  (if any contact field is present)              │
├─────────────────────────────────────────────────┤
│  BOTTOM CTA (repeated for UX)                   │
└─────────────────────────────────────────────────┘
```

**Status-aware CTA logic (same rules as current, extended for new statuses):**

| `edition.status` | User is representative | CTA shown |
|---|---|---|
| `Open` | Yes | "Inscribirse al campamento" button (primary) |
| `Open` | No | Disabled button "Solo el representante puede inscribirse" |
| `Closed` | — | "Inscripciones cerradas" message + "Ver mis inscripciones" link |
| `Completed` | — | "Este campamento ha finalizado" + "Ver mis inscripciones" link |
| `Closed`/`Completed` previous year | — | "Próximamente se abrirá la inscripción para el próximo campamento" |

**Notes field:** Displayed in the hero or as a highlighted notice below the hero if present.

### 4. New Component: `CampExtrasSection.vue`

**File:** `frontend/src/components/camps/CampExtrasSection.vue`

Displays active extras as a card list with name, description, price and pricing labels.

**Props:**
```typescript
defineProps<{
  extras: CampEditionExtra[]
}>()
```

**Pricing label logic:**
- `pricingType === 'PerPerson'` → "por persona"
- `pricingType === 'PerFamily'` → "por familia"
- `pricingPeriod === 'PerDay'` → "/ día"
- `pricingPeriod === 'OneTime'` → "(pago único)"
- Required extras: display a "Incluido" or "Obligatorio" badge

**Template structure:**
```html
<section>
  <h2>Servicios adicionales</h2>
  <ul>
    <li v-for="extra in extras" :key="extra.id">
      <div class="name">{{ extra.name }} <Badge v-if="extra.isRequired" value="Obligatorio" /></div>
      <p v-if="extra.description">{{ extra.description }}</p>
      <span class="price">{{ formatCurrency(extra.price) }} {{ pricingLabel(extra) }}</span>
    </li>
  </ul>
</section>
```

### 5. Update Route Title (optional)

**File:** `frontend/src/router/index.ts`

No route changes needed. Keep `/camp` with `name: 'camp'`. Title can stay `"ABUVI | Campamento"`.

---

## API Documentation Update

**File:** `ai-specs/specs/api-endpoints.md`

Update `GET /api/camps/current` section to document the new response fields:
- `campDescription`, `campPhoneNumber`, `campNationalPhoneNumber`, `campWebsiteUrl`, `campGoogleMapsUrl`, `campGoogleRating`, `campGoogleRatingCount`
- `campPhotos`: array of `CampPhotoResponse` (same shape as in `GET /api/camps/{id}`)
- `accommodationCapacity`: same shape as in `GET /api/camps/{id}` (edition override takes priority over camp default)
- `calculatedTotalBedCapacity`: integer, computed from accommodation capacity
- `extras`: array of active `CampEditionExtraResponse`, sorted by `sortOrder`

---

## Files to Modify

### Backend
| File | Change |
|---|---|
| `src/Abuvi.API/Features/Camps/CampsModels.cs` | Add new fields to `CurrentCampEditionResponse`; add `CampEditionExtraResponse` if absent |
| `src/Abuvi.API/Features/Camps/CampEditionsRepository.cs` | Add `ThenInclude(c => c.Photos)` and `Include(e => e.Extras.Where(x => x.IsActive))` in `GetCurrentAsync` |
| `src/Abuvi.API/Features/Camps/CampEditionsService.cs` | Map new fields in `GetCurrentAsync` |
| `src/Abuvi.Tests/Unit/Features/Camps/CampEditionsServiceTests.cs` | Add unit tests for new field mappings |

### Frontend
| File | Change |
|---|---|
| `frontend/src/types/camp-edition.ts` | Add/update `CurrentCampEditionResponse` interface |
| `frontend/src/composables/useCampEditions.ts` | Update `currentCampEdition` ref type to `CurrentCampEditionResponse` |
| `frontend/src/views/CampPage.vue` | Full redesign: switch to `fetchCurrentCampEdition`, multi-section layout |
| `frontend/src/components/camps/CampExtrasSection.vue` | New component for extras display |
| `ai-specs/specs/api-endpoints.md` | Document new fields on `GET /api/camps/current` |

### Components to reuse (no changes needed)
- `CampPlacesGallery.vue` — photo gallery
- `CampLocationMap.vue` — embedded map
- `AccommodationCapacityDisplay.vue` — accommodation breakdown
- `PricingBreakdown.vue` — pricing table with age ranges
- `CampEditionStatusBadge.vue` — status pill

---

## Acceptance Criteria

- [ ] `GET /api/camps/current` returns `campPhotos`, `extras` (active only, sorted), `accommodationCapacity`, `campDescription`, `campPhoneNumber`, `campWebsiteUrl`, `campGoogleMapsUrl`, `campGoogleRating`
- [ ] Accommodation capacity uses the edition-level override if set, otherwise falls back to the camp-level value
- [ ] `CampPage.vue` calls `/camps/current` (not `/camps/editions/active`)
- [ ] Photo gallery section is visible when the camp has photos
- [ ] Map section is visible when camp has coordinates
- [ ] Accommodation section is visible when accommodation capacity is set
- [ ] Extras section is visible when there are active extras
- [ ] Contact section is visible when any contact field is present
- [ ] CTA button is shown only to the family representative when status is Open
- [ ] Status messages are appropriate for Open / Closed / Completed editions
- [ ] Previous-year "completed" edition renders without register CTA but with informational message
- [ ] Empty state shown when no qualifying edition exists
- [ ] Page is responsive (mobile-first, single column; 2-column layout on lg+)
- [ ] Unit tests pass for new service mapping logic

---

## Non-Functional Requirements

- **Performance**: The repository query must remain `AsNoTracking()`. Photos and extras are bounded collections; no N+1 risk with eager loading.
- **Security**: `GET /api/camps/current` is already Member+. Photos served via `/places/photo?reference=...` proxy should also require authentication (verify the existing authorization on that endpoint).
- **Accessibility**: Photo `<img>` elements must have descriptive `alt` attributes. Interactive elements (buttons, links) must have `aria-label` where needed.
- **i18n-ready**: All user-facing strings use Spanish (consistent with the rest of the app). No hardcoded labels in logic.
