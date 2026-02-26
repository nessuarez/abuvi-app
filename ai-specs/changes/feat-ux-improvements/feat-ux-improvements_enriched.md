# Enriched User Story: UX Improvements — Web App

**Source:** `ai-specs/changes/feat-ux-improvements/feat-ux-improvements.md`
**Date:** 2026-02-26
**Scope:** Frontend-heavy with one minor backend change (Improvement 1)

---

## Summary

Five targeted UX improvements to the web application covering camp edition creation, list navigation, map display, image carousels, and the profile page layout.

---

## Improvement 1: Camp Edition Proposal Form — Simplification & Smart Defaults

### Background

The current `CampEditionProposeDialog.vue` form:

- Requires `proposalReason` (`Motivo de la propuesta`) — must become optional
- Includes `proposalNotes` (`Notas adicionales`) — must be removed from the UI entirely
- Does not pre-populate dates from the previous year's edition

### Backend Change

**File:** Feature handler for `POST /api/camps/editions/propose`

- Change `ProposalReason` from required to optional (`string?`) in the request DTO
- Remove the `[Required]` validation attribute from `ProposalReason`
- `ProposalNotes` field can remain in the backend DTO and database (no migration needed); it simply will no longer be sent from the frontend
- Update `api-endpoints.md` to reflect that `proposalReason` is now optional

**Endpoint:** `POST /api/camps/editions/propose`

```
Body changes:
  proposalReason: string | null   ← was required, now optional
  proposalNotes: string | null    ← backend unchanged (still accepted but frontend won't send it)
```

### Frontend Changes

**File:** `frontend/src/components/camps/CampEditionProposeDialog.vue`

1. **Remove `proposalReason` required validation** — keep the field in the form as an optional textarea, remove any validation that blocks submission when empty
2. **Remove `proposalNotes` field** — delete the form field entirely (label, input, error display)
3. **Pre-populate dates from the previous year:**
   - When opening the dialog for year Y, find the most recent edition for year Y-1 from the already-loaded editions list (from the `useCampEditions()` composable)
   - Map previous edition's dates to year Y by replacing only the year component, keeping month and day:
     - `previousStartDate` → same month/day, year Y
     - `previousEndDate` → same month/day, year Y
   - If no previous edition exists, default to: `startDate = Aug 15, Y`, `endDate = Aug 22, Y`
   - Keep all pre-filled dates editable

**File:** `frontend/src/types/camp-edition.ts`

- Update `ProposeCampEditionRequest` interface: `proposalReason?: string` (optional)
- Remove `proposalNotes` from `ProposeCampEditionRequest`

### Acceptance Criteria

- [ ] User can submit the propose form without filling in `Motivo de la propuesta`
- [ ] `Notas adicionales` field does not appear in the propose form
- [ ] When proposing a new edition for year Y, `startDate` and `endDate` are pre-populated from year Y-1's edition dates, adjusted to year Y
- [ ] If no previous edition exists, dates default to mid-August of year Y
- [ ] Pre-filled dates are fully editable by the user
- [ ] Backend accepts requests with null/empty `proposalReason` without returning 400

### Non-Functional Requirements

- Pre-population logic must be pure/computed — no additional API calls needed (use already-loaded editions)
- Backend must not break existing editions that already have a `proposalReason` stored

---

## Improvement 2: Clickable Camp Edition Names in Lists

### Background

The `CampEditionsPage.vue` DataTable lists camp editions. Users cannot directly navigate to an edition's detail page by clicking its name — they must use a separate action button. This should be a clickable link.

### Frontend Changes

**File:** `frontend/src/views/camps/CampEditionsPage.vue`

- In the DataTable, find the column that displays the edition year or camp name
- Wrap the cell content in a `<router-link>` to `/camps/editions/{edition.id}` (or use `useRouter().push()` with `@click`)
- Style the link: `class="text-primary underline cursor-pointer hover:opacity-75"` (Tailwind + PrimeVue primary color)
- The existing `CampEditionDetailPage.vue` serves as the target page

**File:** `frontend/src/router/index.ts` (or equivalent router config)

- Verify that a route exists for `/camps/editions/:id` pointing to `CampEditionDetailPage.vue`
- If missing, add:

  ```typescript
  {
    path: '/camps/editions/:id',
    name: 'CampEditionDetail',
    component: () => import('@/views/camps/CampEditionDetailPage.vue'),
    meta: { requiresAuth: true, roles: ['Admin', 'Board'] }
  }
  ```

### Acceptance Criteria

- [ ] Clicking the edition name/year in the DataTable navigates to `/camps/editions/{id}`
- [ ] The link is visually distinct (underlined, primary color)
- [ ] Navigation works via router (no full page reload)
- [ ] Route is protected with `Admin` and `Board` roles

### Non-Functional Requirements

- No additional API calls for navigation (use existing data in the table row)

---

## Improvement 3: Camp Location Map — Vertical Extension & Richer Popups

### Background

`CampLocationMap.vue` uses Leaflet.js and currently renders a map with a fixed height that is too short. Marker popups only show `<strong>name</strong><br>year`. More vertical space and richer popup content are needed.

### Frontend Changes

**File:** `frontend/src/components/camps/CampLocationMap.vue`

1. **Increase map height:** Change the map container's Tailwind height class from its current value to at least `h-[500px]` or `h-[600px]`. Ensure the map container has `style="height: 500px"` (Leaflet requires explicit pixel height on the container)
2. **Enrich popup content:** Update the popup HTML string bound to each marker:

   ```html
   <div class="text-sm">
     <strong>{name}</strong><br>
     <span>{location}</span><br>
     <span>Última edición: {lastEditionYear}</span>
   </div>
   ```

3. **Update `CampLocation` interface** (in `frontend/src/types/` or inline) to include:

   ```typescript
   interface CampLocation {
     id: string
     name: string
     latitude: number
     longitude: number
     location: string           // formatted address or city
     lastEditionYear: number    // year of most recent edition
   }
   ```

**File:** Parent component(s) that instantiate `<CampLocationMap>` (likely `CampEditionDetails.vue` or `CampPage.vue`/`AdminPage.vue`)

- Ensure `locations` prop passed to the map includes `location` (from `Camp.location`) and `lastEditionYear` (derived from `CampEdition.year` of the most recent edition for that camp)
- This data is already available in the editions list; compute `lastEditionYear` client-side from the loaded editions

### Acceptance Criteria

- [ ] Map is at least 500px tall
- [ ] Each marker popup displays: camp name, location/address, last edition year
- [ ] Map still auto-fits bounds to show all markers
- [ ] Map remains responsive (full width of its container)

### Non-Functional Requirements

- No additional backend API calls — derive `lastEditionYear` from already-loaded data
- Leaflet cleanup on `onUnmounted` must remain intact

---

## Improvement 4: Replace Static Images with Carousels

### Background

Camp location cards/details currently show a single static primary photo. The `CampPhoto` entity supports multiple photos per camp with `displayOrder`. These should be displayed as a carousel with captions.

### Frontend Changes

**File:** `frontend/src/components/camps/CampLocationCard.vue`

- Replace the single `<img>` or `<Image>` element with a PrimeVue `<Galleria>` component
- Pass the camp's `photos` array (from `CampPhoto[]`) sorted by `displayOrder`
- Show at minimum: photo image, description as caption, primary indicator
- Galleria configuration:

  ```vue
  <Galleria
    :value="photos"
    :show-thumbnails="false"
    :show-item-navigators="photos.length > 1"
    :show-indicators="photos.length > 1"
    :circular="true"
    :auto-play="false"
  >
    <template #item="{ item }">
      <img :src="item.photoUrl" :alt="item.description ?? camp.name" class="w-full object-cover h-48" />
    </template>
    <template #caption="{ item }">
      <span v-if="item.description" class="text-sm">{{ item.description }}</span>
    </template>
  </Galleria>
  ```

- If no photos exist, show a placeholder image or the camp name initials

**File:** `frontend/src/components/camps/CampEditionDetails.vue` (if it also displays camp photos)

- Apply the same Galleria pattern if photos are shown here
- Photos are available from `GET /api/camps/{id}` response (already includes `photos[]`)

**Note on `AnniversaryCarousel.vue`:** The hardcoded slides in the anniversary carousel are out of scope for this ticket — they are not tied to backend photo data. Only camp-related photo displays are in scope.

### Type Requirement

Confirm `CampPhoto` type in `frontend/src/types/` includes:

```typescript
interface CampPhoto {
  id: string
  campId: string
  photoUrl: string
  description: string | null
  isPrimary: boolean
  isOriginal: boolean
  displayOrder: number
}
```

### Acceptance Criteria

- [ ] Camp location cards show a Galleria/carousel when multiple photos exist
- [ ] Photos are ordered by `displayOrder` ascending
- [ ] Captions display the photo `description` when present
- [ ] Navigation arrows only appear when there are 2+ photos
- [ ] Single photo renders without navigation controls (no arrows, no indicators)
- [ ] Missing photos show a placeholder

### Non-Functional Requirements

- Photos must already be loaded when the card renders (no lazy loading required for now)
- Galleria must not auto-play to avoid distracting UX

---

## Improvement 5: Profile Page — Extended Desktop Width or Tabbed Layout

### Background

`ProfilePage.vue` currently renders all four sections (personal info, family unit, account security) in a narrow single-column layout. On desktop screens, this wastes horizontal space and requires excessive scrolling.

### Frontend Changes

**File:** `frontend/src/views/ProfilePage.vue`

**Option A — Extended Width (simpler, recommended):**

- Change the outer container from `max-w-2xl` (or equivalent) to `max-w-5xl` on desktop
- Use a two-column grid on `lg` breakpoint to display sections side by side:

  ```html
  <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
    <!-- Personal Information -->
    <!-- Family Unit & Members -->
    <!-- Account Security -->
  </div>
  ```

- Family & Members section (likely the tallest) spans full width: `class="lg:col-span-2"`

**Option B — Tabbed Interface (if sections are too dense for grid):**

- Use PrimeVue `<Tabs>` component (PrimeVue 4 API)
- Three tabs:
  1. `Información personal` — personal info + account security
  2. `Familia y miembros` — family unit, members list, memberships
  3. `Cuota y pagos` — fee status (if separated from members)
- Tabs only activate on `md+` breakpoint; on mobile, show as stacked sections

The decision between Option A and B should be validated against the actual rendered density of the page, but Option A is preferred for its simplicity.

### Acceptance Criteria

- [ ] On desktop (`lg` breakpoint and above), the profile page uses available horizontal space (at least `max-w-5xl`)
- [ ] On mobile, layout remains single-column and functional
- [ ] All existing sections are accessible (no content removed)
- [ ] No layout shifts or overflow issues at any breakpoint

### Non-Functional Requirements

- No backend changes required
- Accessibility: Tab order must remain logical if tabs are implemented (left-to-right, top-to-bottom)

---

## Files to Modify — Summary

| File | Improvement |
|------|------------|
| `frontend/src/components/camps/CampEditionProposeDialog.vue` | 1 |
| `frontend/src/types/camp-edition.ts` | 1 |
| Backend: `ProposeCampEditionRequest.cs` (or equivalent) | 1 |
| `ai-specs/specs/api-endpoints.md` | 1 |
| `frontend/src/views/camps/CampEditionsPage.vue` | 2 |
| `frontend/src/router/index.ts` | 2 (verify route) |
| `frontend/src/components/camps/CampLocationMap.vue` | 3 |
| Parent component(s) passing data to `CampLocationMap` | 3 |
| `frontend/src/components/camps/CampLocationCard.vue` | 4 |
| `frontend/src/components/camps/CampEditionDetails.vue` | 4 (if applicable) |
| `frontend/src/views/ProfilePage.vue` | 5 |

---

## Implementation Order (Recommended)

1. **Improvement 1** (backend + frontend) — highest business value, unblocks form usability
2. **Improvement 2** (frontend only, small) — quick win
3. **Improvement 5** (frontend only, CSS) — quick win
4. **Improvement 3** (frontend, map) — moderate effort
5. **Improvement 4** (frontend, carousel) — most UI effort, depends on photos being loaded

---

## Testing Requirements

- Each form change (Improvement 1) must have:
  - Unit test: `CampEditionProposeDialog` renders without `proposalNotes` field
  - Unit test: form submits successfully with empty `proposalReason`
  - Unit test: dates pre-populate correctly from previous year edition
- Navigation (Improvement 2): unit test that clicking edition name triggers router navigation
- Map (Improvement 3): unit test that popup content includes `location` and `lastEditionYear`
- Coverage threshold: maintain ≥90% branches, functions, lines, statements per frontend-standards.mdc
