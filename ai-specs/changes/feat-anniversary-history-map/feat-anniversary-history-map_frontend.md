# Frontend Implementation Plan: feat-anniversary-history-map — Phase 3 Historical Visualisation (map + chronology)

## Overview

Phase 3 builds the **output** side of the 50th anniversary section: a navigable journey over 50 camp
editions (1976–2025) across 31 venues, driven entirely by `GET /api/camps/history` (Phase 2, merged
into `dev` via PR #285).

The layout is the one validated in the geocode review page: **map on the left (~60 %), scrollable
venue list on the right (~40 %)**, both synchronised through a single shared `selectedYear`. A year
strip underneath turns the 50 editions into a navigation control, and a presentation mode walks the
whole history unattended — the demo the association will show on stage.

Architecture principles applied:

- **Vue 3 Composition API**, `<script setup lang="ts">` everywhere, TypeScript strict, no `any`.
- **Composable-based data access**: components never call `api` directly (`frontend-standards.mdc`,
  "Composables").
- **Local state, no Pinia**: the selection lives on a single page and is not shared across routes.
  `frontend-standards.mdc` reserves Pinia for cross-route shared state.
- **PrimeVue + Tailwind**, keeping the existing amber palette of the anniversary section.
- **Extend, don't fork**: `CampLocationMap.vue` gains optional props whose defaults preserve today's
  behaviour, so its four existing consumers stay untouched.

### Scope note — this is a frontend plan for a frontend phase

The `/plan-backend-ticket` command was requested, but **Phase 3 has no backend work**. Its file list
in the enriched spec is entirely `composables/` and `components/anniversary/`, and the only endpoint
it needs (`GET /api/camps/history`) shipped in Phase 2 and is already merged into `dev`. The spec is
also explicit that no additional endpoint is required — the 50 rows are grouped by `campId` on the
client. This document therefore follows `ai-specs/.commands/plan-frontend-ticket.md`.

If a backend change turns out to be needed mid-implementation, the two candidates are listed under
[Notes → Possible backend follow-ups](#possible-backend-follow-ups); neither blocks Phase 3.

---

## Architecture Context

### Files to create

| File | Purpose |
| --- | --- |
| `frontend/src/types/camp-history.ts` | `CampHistoryEntry`, `CampHistoryPhoto`, `CampHistoryVenue` |
| `frontend/src/composables/useCampHistory.ts` | Fetch `/camps/history`; expose entries, venues, loading, error |
| `frontend/src/components/anniversary/AnniversaryJourney.vue` | Container; owns `selectedYear`, coordinates map / list / strip / gallery |
| `frontend/src/components/anniversary/AnniversaryVenueList.vue` | Scrollable venue list with clickable year chips |
| `frontend/src/components/anniversary/AnniversaryYearStrip.vue` | Horizontal 50-year navigation band |
| `frontend/src/composables/__tests__/useCampHistory.test.ts` | Vitest |
| `frontend/src/components/anniversary/__tests__/AnniversaryJourney.test.ts` | Vitest |
| `frontend/src/components/anniversary/__tests__/AnniversaryVenueList.test.ts` | Vitest |
| `frontend/src/components/anniversary/__tests__/AnniversaryYearStrip.test.ts` | Vitest |

### Files to modify

| File | Change |
| --- | --- |
| `frontend/src/components/camps/CampLocationMap.vue` | Additive optional props: honour `selectedId`, marker scaling, height, clickable years |
| `frontend/src/types/camp.ts` | Extend `CampLocation` with optional `id`, `editionYears`, `editionCount` |
| `frontend/src/components/anniversary/AnniversaryGallery.vue` | Accept optional `year` prop; refetch on change; year-aware empty state |
| `frontend/src/components/anniversary/AnniversaryTimeline.vue` | Receive milestones via props instead of the hardcoded array |
| `frontend/src/views/AnniversaryPage.vue` | Mount `AnniversaryJourney`, wire the gallery year, add `#historia` nav link, uncomment the timeline |
| `frontend/src/components/camps/__tests__/CampLocationMap.test.ts` | Cover the new optional behaviour |
| `frontend/src/components/anniversary/__tests__/AnniversaryGallery.test.ts` | Cover the `year` prop |

### Routing

**No routing changes.** `/anniversary` already exists with `requiresAuth: true`
(`frontend/src/router/index.ts:42`), which is the correct gate: the endpoint requires a member role
and returns 401 otherwise. Only in-page anchors are added (`#historia`).

### State management approach

**Local state in `AnniversaryJourney.vue`**, no Pinia store:

- `selectedYear: Ref<number | null>` — the single source of truth for the whole journey.
- `selectedVenue` — derived (`computed`) from `selectedYear`, never stored separately. Storing both
  is what makes map↔list synchronisation drift.
- `useCampHistory()` owns the server data and the `venues` grouping; the container owns selection.

---

## Prerequisite: what `CampLocationMap.vue` actually supports today

The enriched spec assumes the component can be reused "as is". **It cannot** — an audit of
`frontend/src/components/camps/CampLocationMap.vue` found four gaps that Phase 3 depends on. This is
the single biggest risk to the phase estimate, so it is stated up front:

| Spec assumption | Reality in the code |
| --- | --- |
| `selectedId` prop drives selection | The prop is **declared but never read**. No pan, no popup, no highlight |
| `selectLocation` emits an id | It emits `location.name` (line 63), and `markers` is keyed by name too |
| Pin size can scale with edition count | Markers are the default Leaflet icon, fixed size |
| Popups can hold clickable years | Popups are built from an **HTML string** (`bindPopup(\`<div…>\`)`), so nothing inside them is clickable |
| The map fits a 60/40 split and a mobile 55 vh | Height is hardcoded `h-[500px]` on the container |

The good news: **no existing consumer binds `:selected-id` or `@select-location`.** All four usages
(`CampPage.vue:325`, `CampLocationsPage.vue:280`, `CampLocationDetailPage.vue:372`,
`CampEditionDetails.vue:85`) pass only `:locations`. The extension is therefore safe as long as every
new prop is optional and every default reproduces today's rendering.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a frontend-specific branch.
- **Branch Naming**: `feature/feat-anniversary-history-map-frontend` (required — do not implement on
  the general `feature/feat-anniversary-history-map` branch; that one carried Phase 2 and is merged).
- **Implementation Steps**:
  1. Ensure you are on the latest `dev`: `git checkout dev && git pull origin dev`.
  2. Create the branch: `git checkout -b feature/feat-anniversary-history-map-frontend`.
  3. Verify: `git branch`.
- **Notes**: Branch from `dev`. PR #285 (Phase 2, the `/api/camps/history` endpoint) merged into `dev`
  on 2026-08-25, so `dev` already carries the API this phase consumes — no need to branch from the
  backend branch. **PRs target `dev`, never `main`.** If the working tree is dirty with unrelated
  work, use a git worktree instead of switching branches in place.

---

### Step 1: Define TypeScript Interfaces

- **File**: `frontend/src/types/camp-history.ts` (new)
- **Action**: Mirror the Phase 2 response DTOs exactly, plus the client-side venue grouping.
- **Function/Component Signature**:

```typescript
/** One published anniversary photo, as returned by GET /api/camps/history. */
export interface CampHistoryPhoto {
  id: string
  /** Never empty: the API falls back to the full image when no thumbnail exists. */
  thumbnailUrl: string
  title: string
}

/** One completed camp edition. Mirrors CampHistoryResponse in the API. */
export interface CampHistoryEntry {
  year: number
  campId: string
  campName: string
  location: string | null
  latitude: number | null
  longitude: number | null
  /** How many times the association had camped at this venue up to and including this year. */
  editionNumber: number
  /** The venue's full tally across the whole history. */
  totalEditionsAtVenue: number
  /** Approved and published photos for this year. 0 means "nothing survives", not "not loaded". */
  photoCount: number
  previewPhotos: CampHistoryPhoto[]
}

/** A venue with all its editions, grouped client-side from the 50 flat rows. */
export interface CampHistoryVenue {
  campId: string
  campName: string
  location: string | null
  latitude: number | null
  longitude: number | null
  /** Ascending. Length always equals totalEditionsAtVenue. */
  years: number[]
  totalEditionsAtVenue: number
  /** Sum of photoCount across this venue's editions. */
  photoCount: number
}
```

- **Dependencies**: none.
- **Implementation Notes**:
  - Field names are the camelCase serialisation of `CampHistoryResponse`
    (`src/Abuvi.API/Features/Camps/CampsModels.cs:830`). Do not rename or reshape them in the type —
    a mismatch here fails silently at runtime.
  - `latitude` / `longitude` are `decimal?` server-side and arrive as `number | null`. Every one of
    the 50 current rows has coordinates, but **the type must keep the null** — a future venue
    imported without geocoding would otherwise crash `L.latLngBounds`.
  - No `galleryUrl` field: the API deliberately returns no client routes. The gallery link is built
    from `year` in the frontend.

---

### Step 2: Create the `useCampHistory` Composable

- **File**: `frontend/src/composables/useCampHistory.ts` (new)
- **Action**: Fetch the history once and expose it both flat (by year) and grouped (by venue).
- **Function/Component Signature**:

```typescript
export function useCampHistory(): {
  entries: Ref<CampHistoryEntry[]>          // ascending by year, as the API returns them
  venues: ComputedRef<CampHistoryVenue[]>   // grouped by campId, ordered by first year
  years: ComputedRef<number[]>              // ascending, for the year strip
  loading: Ref<boolean>
  error: Ref<string | null>
  fetchHistory: () => Promise<void>
  entryByYear: (year: number) => CampHistoryEntry | undefined
  venueByYear: (year: number) => CampHistoryVenue | undefined
}
```

- **Implementation Steps**:
  1. Follow `useMediaItems.ts` verbatim as the reference: `ref` state, `try/catch/finally`, the
     `ApiErrorShape` local type, `response.data.success && response.data.data` guard,
     `console.error` on failure.
  2. `fetchHistory` calls `api.get<ApiResponse<CampHistoryEntry[]>>('/camps/history')`.
  3. On failure set `error.value` to the API message or the fallback
     `'No se pudo cargar el histórico de campamentos'` (user-facing text in Spanish; see Language
     Standards).
  4. `venues` is a `computed` that reduces `entries` into a `Map<string, CampHistoryVenue>` keyed by
     `campId`, pushing `year` into `years` and accumulating `photoCount`. Order the output by each
     venue's **first** year so the list reads chronologically, and sort `years` ascending.
  5. `entryByYear` / `venueByYear` are lookup helpers backed by a `computed` `Map`, not `Array.find`
     — they are called on every selection change and from the presentation-mode interval.
- **Dependencies**: `vue` (`ref`, `computed`), `@/utils/api`, `@/types/api`, `@/types/camp-history`.
- **Implementation Notes**:
  - **Do not paginate and do not add query params.** The endpoint returns all 50 rows in one call by
    design; that is what makes the client-side grouping legitimate.
  - Call `fetchHistory` once, from the container's `onMounted`. Never from a child — two mounted
    children would produce two identical requests.
  - `totalEditionsAtVenue` comes from the server; use it, but assert `years.length` matches it in the
    unit test. A mismatch means the grouping key is wrong.

---

### Step 3: Extend `CampLocationMap.vue` (additive, non-breaking)

- **File**: `frontend/src/components/camps/CampLocationMap.vue` (modify),
  `frontend/src/types/camp.ts` (modify)
- **Action**: Close the four gaps identified in the prerequisite section, without changing any
  existing consumer's rendering.
- **Function/Component Signature**:

```typescript
// types/camp.ts — additive fields only, all optional
export interface CampLocation {
  latitude: number
  longitude: number
  name: string
  year?: number
  location?: string
  lastEditionYear?: number
  /** Stable identifier. When absent, `name` is used, preserving today's behaviour. */
  id?: string
  /** Edition years shown as clickable chips inside the popup. */
  editionYears?: number[]
  /** Drives marker size. When absent, the default Leaflet pin is used. */
  editionCount?: number
}

// CampLocationMap.vue
interface Props {
  locations: CampLocation[]
  selectedId?: string
  /** Tailwind height class for the map container. Default preserves the current look. */
  heightClass?: string     // default: 'h-[500px]'
}
const emit = defineEmits<{
  selectLocation: [id: string]
  selectYear: [year: number]
}>()
```

- **Implementation Steps**:
  1. **Stable key.** Introduce `const keyOf = (loc: CampLocation) => loc.id ?? loc.name` and use it
     for the `markers` Map and for the `selectLocation` payload. Consumers that pass no `id` keep
     receiving the name exactly as today.
  2. **Honour `selectedId`.** Add a `watch` on `() => props.selectedId` that, when the id resolves to
     a known marker, calls `map.panTo(marker.getLatLng())` and `marker.openPopup()`. Guard against a
     missing marker and against `selectedId === undefined` (deselection must not move the map).
  3. **Marker scaling.** When `location.editionCount` is present, render an `L.divIcon` (not
     `circleMarker` — a div icon keeps the amber styling and stays clickable) whose diameter is
     `28 + Math.min(editionCount, 4) * 6` px, capped so a 4-edition venue is noticeably larger
     without dwarfing the rest. When `editionCount` is absent, keep `L.marker` with the default icon
     so the four existing pages render identically.
  4. **Selected marker highlight.** Add/remove a CSS class on the marker element
     (`marker.getElement()?.classList.toggle('camp-marker--selected', …)`) rather than re-creating
     markers; re-creating them closes the popup and fights the pan.
  5. **Clickable years in the popup.** Replace the popup HTML string with a **DOM element**:
     build a `document.createElement('div')`, append the name/location nodes, and append one
     `<button>` per `editionYears` entry with a real `addEventListener('click', …)` that emits
     `selectYear`. `bindPopup` accepts an `HTMLElement`. Building the string and hoping for delegated
     clicks is the fragile path — do not take it.
     **Set text via `textContent`, never `innerHTML`**, so a venue name can never inject markup (the
     current string-concatenation popup is an XSS vector waiting for a bad venue name).
  6. **Height prop.** Replace the hardcoded class with `:class="heightClass ?? 'h-[500px]'"` and call
     `map.invalidateSize()` in a `nextTick` after the container is laid out — a Leaflet map inside a
     grid column that is sized after mount renders grey tiles otherwise. This is the classic
     side-by-side Leaflet bug; budget for it.
  7. Keep the existing `watch` on `locations` and the `fitBounds` call unchanged.
- **Dependencies**: `leaflet` (already a dependency), no new packages.
- **Implementation Notes**:
  - **Every new prop and field is optional and every default reproduces current output.** Verify by
    running the existing `CampLocationMap.test.ts` unmodified before adding new cases — if it needs
    editing to pass, the change is not additive.
  - Filter out entries with `latitude === null || longitude === null` **before** passing them to the
    map, in the container, not inside this component.
  - If step 5 proves awkward against the existing test's `mockBindPopup` string assertions, keep the
    string path when `editionYears` is absent and use the element path only when it is present. That
    keeps the old tests green and confines the new behaviour.

---

### Step 4: Create `AnniversaryVenueList.vue`

- **File**: `frontend/src/components/anniversary/AnniversaryVenueList.vue` (new)
- **Action**: The right-hand 40 %: one scrollable row per venue, each showing its years as clickable
  chips.
- **Function/Component Signature**:

```typescript
interface Props {
  venues: CampHistoryVenue[]
  selectedYear: number | null
  selectedCampId: string | null
}
const emit = defineEmits<{
  selectYear: [year: number]
  selectVenue: [campId: string]
}>()
```

- **Implementation Steps**:
  1. Render each venue as an `<article>` with `campName`, `location`, and a wrapped row of year
     chips — the layout the spec draws:
     `Espinosa de los Monteros · Burgos` / `1983 · 1993 · 2003 · 2015`.
  2. Each year chip is a real `<button>` emitting `selectYear`. Mark the selected one with the amber
     fill and `aria-current="true"`.
  3. Clicking the row body (not a chip) emits `selectVenue`.
  4. Show `totalEditionsAtVenue` as a small badge when `> 1` — that is the "here we came back four
     times" story the list is meant to tell.
  5. **Scroll into view**: `watch` `selectedCampId` and call `scrollIntoView({ block: 'nearest',
     behaviour: prefersReducedMotion ? 'auto' : 'smooth' })` on the matching row via a template ref
     map. Use `block: 'nearest'` — `'center'` yanks the list on every step of presentation mode.
  6. The list container is `overflow-y-auto` with a height matching the map
     (`h-[500px] lg:h-[600px]`), and full-height/auto on mobile.
- **Dependencies**: `vue`, `@/types/camp-history`. PrimeVue `Badge` optional; a Tailwind span is
  enough and avoids a component import for a pill.
- **Implementation Notes**:
  - Do not fetch anything here. Props in, events out — this component must be unit-testable with a
    plain array.
  - Keyboard access matters: the chips are buttons, so tab order works for free. Add
    `aria-label="Edición de 1983 en Espinosa de los Monteros"` on each chip so the year alone is not
    the whole accessible name.

---

### Step 5: Create `AnniversaryYearStrip.vue`

- **File**: `frontend/src/components/anniversary/AnniversaryYearStrip.vue` (new)
- **Action**: A horizontal band of all 50 years, used both for manual navigation and as the visible
  progress indicator during presentation mode.
- **Function/Component Signature**:

```typescript
interface Props {
  entries: CampHistoryEntry[]     // ascending by year
  selectedYear: number | null
}
const emit = defineEmits<{ selectYear: [year: number] }>()
```

- **Implementation Steps**:
  1. Horizontal `overflow-x-auto` flex row of 50 `<button>` chips, each showing the year.
  2. Under each year, a small dot whose opacity/colour reflects `photoCount`: filled amber when
     `photoCount > 0`, hollow when `0`. At a glance this shows where the archive has holes — which is
     the argument the presentation is making.
  3. Selected year: amber fill, `aria-current="true"`, and `scrollIntoView({ inline: 'center',
     block: 'nearest' })` on change so presentation mode keeps the current year visible.
  4. Decade separators (a thin divider before each year ending in 0) make 50 chips scannable.
- **Dependencies**: `vue`, `@/types/camp-history`.
- **Implementation Notes**:
  - **Why a new component instead of reusing `AnniversaryTimeline.vue`.** The existing timeline is a
    PrimeVue `Timeline` whose horizontal variant renders `w-32` cards inside a `min-w-[900px]`
    wrapper. Fifty nodes at that width is roughly a **6 400 px** scroll container with 50 title +
    description blocks — unusable as a navigation control and heavy to render. The timeline's job is
    narrative (a handful of milestones); the strip's job is navigation (all 50 years). They are
    different components. The timeline is still refactored to props in Step 8, per the spec.

---

### Step 6: Create `AnniversaryJourney.vue` (container)

- **File**: `frontend/src/components/anniversary/AnniversaryJourney.vue` (new)
- **Action**: Own `selectedYear`, fetch the history, lay out map + list + strip, and run presentation
  mode.
- **Function/Component Signature**:

```typescript
const emit = defineEmits<{ 'update:year': [year: number | null] }>()
// or expose selectedYear via defineExpose if AnniversaryPage prefers to read it
```

- **Implementation Steps**:
  1. `const { entries, venues, years, loading, error, fetchHistory, entryByYear, venueByYear } =
     useCampHistory()`; call `fetchHistory()` in `onMounted`.
  2. `const selectedYear = ref<number | null>(null)`. Derive everything else:
     - `selectedEntry = computed(() => selectedYear.value ? entryByYear(selectedYear.value) : undefined)`
     - `selectedCampId = computed(() => selectedEntry.value?.campId ?? null)`
  3. `mapLocations = computed(...)` maps `venues` to `CampLocation[]`, **filtering out null
     coordinates**, and sets `id: campId`, `name: campName`, `location`, `editionYears: years`,
     `editionCount: totalEditionsAtVenue`.
  4. Wire the three children:
     - map `@select-location` → select that venue's **most recent** year (the one most likely to have
       photos), map `@select-year` → set the year directly;
     - list `@select-year` / `@select-venue` → same;
     - strip `@select-year` → same.
     All paths funnel through one `selectYear(year: number)` function. Resist adding a second setter.
  5. `watch(selectedYear, …)` emits `update:year` so the page can drive the gallery.
  6. **Layout**: `grid grid-cols-1 lg:grid-cols-5`; map spans `lg:col-span-3` (60 %), list
     `lg:col-span-2` (40 %). On mobile the map gets `h-[55vh]` and the list stacks below, per the
     spec. Pass `height-class` to the map accordingly.
  7. **Empty state**: when `selectedEntry.photoCount === 0`, render the call to action —
     *"De 1987 en Los Palancares no conservamos nada todavía. ¿Tienes algo?"* built from the real
     year and venue name — with a button that scrolls to `#subir-recuerdo`. Reuse the
     `scrollToUpload` pattern already in `AnniversaryGallery.vue:9`.
  8. **Preview photos**: when `photoCount > 0`, show `selectedEntry.previewPhotos` as up to three
     thumbnails above the gallery link. They come free with the endpoint — not using them wastes the
     work Phase 2 did to avoid a second round trip.
  9. **Presentation mode**:
     - `isPlaying: Ref<boolean>`, a `setInterval` at 2 000 ms stepping through `years`, wrapping to
       the start at the end.
     - **`onUnmounted` must clear the interval.** A leaked interval that calls `panTo` on a destroyed
       Leaflet map throws on every tick.
     - Any manual selection (`selectYear` from a user event) stops playback. Auto-advance calls the
       same function with a flag, or sets `selectedYear` directly — do not let the auto-advance stop
       itself.
     - Respect `window.matchMedia('(prefers-reduced-motion: reduce)')`: still advance, but skip the
       smooth scrolling and panning animation.
     - The toggle is a PrimeVue `Button` with `pi-play` / `pi-pause`.
  10. **Loading / error**: PrimeVue `Skeleton` blocks matching the final layout while `loading`; on
      `error`, the same treatment `AnniversaryGallery.vue` already uses (warning icon + message),
      never a blank map frame.
- **Dependencies**: `vue`, `@/composables/useCampHistory`, `@/components/camps/CampLocationMap.vue`,
  the two new anniversary components, PrimeVue `Button` and `Skeleton`.
- **Implementation Notes**:
  - **Initial selection**: leave `selectedYear` as `null` on load and let the map show all 31 pins
    fitted to Spain. Auto-selecting a year on mount hides the "50 years at once" first impression
    that is the whole point of the opening frame.
  - `invalidateSize()` after the grid settles (see Step 3.6) — this container is exactly the case
    that triggers the grey-tile bug.

---

### Step 7: Wire the Gallery to the Selected Year

- **File**: `frontend/src/components/anniversary/AnniversaryGallery.vue` (modify)
- **Action**: Accept an optional `year` and refetch when it changes.
- **Function/Component Signature**:

```typescript
interface Props { year?: number | null }
const props = defineProps<Props>()
```

- **Implementation Steps**:
  1. Replace the bare `onMounted(() => fetchMediaItems({ approved: true, context: 'anniversary-50' }))`
     with a `load()` function that includes `year: props.year ?? undefined`.
  2. `watch(() => props.year, load)` plus `onMounted(load)`.
  3. Adjust the heading and the empty state to name the year when one is selected —
     *"Recuerdos de 2003"* and *"De 2003 no conservamos nada todavía"* rather than the generic copy.
  4. Add a "Ver todos los años" button when `year` is set, emitting `clearYear`.
- **Dependencies**: none new; `useMediaItems` already accepts `year`.
- **Implementation Notes**: `fetchMediaItems` already builds the query string from `year`, `approved`
  and `context` (`useMediaItems.ts:24-33`) — no composable change is required.

---

### Step 8: Refactor `AnniversaryTimeline.vue` to Props

- **File**: `frontend/src/components/anniversary/AnniversaryTimeline.vue` (modify)
- **Action**: Remove the hardcoded milestone array and receive milestones from the parent.
- **Function/Component Signature**:

```typescript
export interface AnniversaryMilestone { year: number; title: string; description: string }
interface Props { milestones: AnniversaryMilestone[]; selectedYear?: number | null }
const emit = defineEmits<{ selectYear: [year: number] }>()
```

- **Implementation Steps**:
  1. Delete the module-level `milestones` const and take it from props. Keep both the vertical
     (mobile) and horizontal (desktop) PrimeVue `Timeline` variants exactly as they are.
  2. Highlight the node whose `year === selectedYear` (amber ring on the marker) and emit
     `selectYear` on click.
  3. In `AnniversaryPage.vue`, uncomment the timeline section (currently lines 66-68) and feed it a
     **curated** list — not all 50 editions (see Notes).
- **Dependencies**: `primevue/timeline` (already used).
- **Implementation Notes**:
  - **The current hardcoded milestones are invented and several are wrong.** "2020 — campamento
    virtual" contradicts the imported data, which records a real 2020 edition at Los Palancares.
    Anything shipped here must come from real data or from a person who knows the history — do not
    carry the existing array forward as seed content.
  - The honest short-term option, given the presentation deadline: feed the timeline the handful of
    editions that genuinely mark something (1976 first camp; the venues with 3–4 returns; 2025), or
    leave the section commented out for this release and ship the year strip alone. **Prefer the
    latter if no verified milestone text is available** — an invented history is worse than no
    timeline in a 50th-anniversary presentation.

---

### Step 9: Mount the Journey in `AnniversaryPage.vue`

- **File**: `frontend/src/views/AnniversaryPage.vue` (modify)
- **Action**: Insert the journey section and connect it to the gallery.
- **Implementation Steps**:
  1. `const selectedYear = ref<number | null>(null)` in the page.
  2. New section between the hero and the upload form — the journey is the reason to visit, so it
     comes before the ask:

     ```html
     <section id="historia" class="bg-amber-50 py-16">
       <AnniversaryJourney @update:year="selectedYear = $event" />
     </section>
     ```

  3. Pass it down: `<AnniversaryGallery :year="selectedYear" @clear-year="selectedYear = null" />`.
  4. Add `<a href="#historia">Historia</a>` to the sticky nav, between "Inicio" and "Comparte".
  5. Uncomment the timeline section only if Step 8's curated milestones exist.
- **Dependencies**: the new components.
- **Implementation Notes**: keep the existing amber dividers (`<div class="h-px bg-amber-200" />`)
  between sections so the new block matches the page rhythm.

---

### Step 10: Write Vitest Unit Tests

- **Files**: the four new `__tests__` files plus additions to the two existing ones.
- **Action**: Cover the composable, the two presentational components, the container's coordination,
  and the map extension.
- **Implementation Steps**:
  1. **`useCampHistory.test.ts`** — mock `@/utils/api` with `vi.mock` exactly as
     `useCamps.test.ts:31` does. A `makeEntry(overrides)` factory keeps the cases readable. Cover:
     - successful fetch populates `entries` in the order returned;
     - `venues` groups by `campId`, `years` ascending, `years.length === totalEditionsAtVenue`;
     - venues ordered by first year;
     - `photoCount` summed per venue;
     - `entryByYear` / `venueByYear` hit and miss;
     - API `success: false` sets `error` and leaves `entries` empty;
     - a thrown error sets `error` and clears `loading`.
  2. **`AnniversaryVenueList.test.ts`** — a year chip click emits `selectYear` with that year; the
     selected chip carries `aria-current`; a venue with 4 years renders 4 chips and the badge.
  3. **`AnniversaryYearStrip.test.ts`** — 50 entries render 50 buttons; a `photoCount: 0` entry
     renders the hollow dot; clicking emits the year.
  4. **`AnniversaryJourney.test.ts`** — stub the map (`CampLocationMap: true`, the pattern used in
     `CampPage.spec.ts:61`) and mock `useCampHistory`. Cover:
     - selecting a year from the list updates what the strip receives;
     - selecting a venue on the map selects that venue's most recent year;
     - a `photoCount: 0` selection renders the call to action with the real year and venue name;
     - presentation mode advances with `vi.useFakeTimers()` and **is cleared on unmount**
       (assert `clearInterval` was called, or that no further advance happens after `unmount()`);
     - a manual selection stops playback;
     - venues with null coordinates are excluded from `mapLocations`.
  5. **`CampLocationMap.test.ts`** — add cases without touching the existing ones: `selectedId` opens
     the matching marker's popup; a location with `editionCount` uses a div icon; `editionYears`
     renders year buttons that emit `selectYear`; `heightClass` is applied; a location **without**
     the new fields renders exactly as before.
  6. **`AnniversaryGallery.test.ts`** — `year` is forwarded to `fetchMediaItems`; changing it
     refetches; the heading names the year.
- **Dependencies**: `vitest`, `@vue/test-utils` (both already configured).
- **Implementation Notes**:
  - The Leaflet mock at the top of `CampLocationMap.test.ts` (lines 1-31) already stubs `L.map`,
    `L.marker`, `bindPopup` and the CSS import. Extend that mock rather than writing a second one.
  - `frontend-standards.mdc` sets the coverage bar; aim for full branch coverage on the composable,
    which is where the grouping logic lives.

---

### Step 11: Cypress E2E (optional for this release)

- **File**: `frontend/cypress/e2e/anniversary-journey.cy.ts` (new)
- **Action**: One happy path — log in as a member, open `/anniversary`, click a year chip, assert the
  gallery filters.
- **Implementation Notes**: Leaflet in Cypress is slow and flaky; assert on the **list and gallery**,
  not on map internals. Given the presentation deadline this step is genuinely optional — the Vitest
  coverage plus the manual checklist in §6 covers the risk. Say so explicitly if it is skipped rather
  than leaving it silently undone.

---

### Step 12: Update Technical Documentation

- **Action**: Review and update technical documentation according to the changes made.
- **Implementation Steps**:
  1. **Review Changes**: list every file touched in Steps 1-11.
  2. **Identify Documentation Files**:
     - `ai-specs/specs/frontend-standards.mdc` → the "Maps Integration" section still shows the
       original minimal Leaflet snippet. Update it to document the extended `CampLocationMap`
       contract: `id`, `selectedId`, `editionYears`, `editionCount`, `heightClass`, the `selectYear`
       emit, the DOM-element popup, and the `invalidateSize()` requirement in flex/grid layouts.
     - `ai-specs/specs/frontend-standards.mdc` → add the anniversary journey to the component
       inventory if one is maintained there.
     - `ai-specs/specs/api-endpoints.md` → **add `GET /api/camps/history`**. Phase 2 shipped the
       endpoint without documenting it: a search of the specs finds no `camps/history` entry, and
       the `api-spec.yml` the command template names **does not exist in this repo** —
       `api-endpoints.md` is the API reference actually maintained here. Document it beside the
       other `/api/camps` reads (`GET /api/camps/current`, line 1121): path, Member+ authorisation,
       the `CampHistoryResponse` shape including `photoCount` and `previewPhotos`, and the 401.
     - `ai-specs/changes/feat-anniversary-history-map/feat-anniversary-history-map_enriched.md` →
       mark Phase 3 as `✅ HECHO`, tick its checklist, and record the two deviations from the spec
       (the separate `AnniversaryYearStrip` component, and whatever was decided about the timeline).
  3. **Update Documentation**: all documentation in English, matching existing structure.
  4. **Verify Documentation**: confirm every change is reflected and formatting is consistent.
  5. **Report Updates**: state which files were updated and what changed.
- **References**: `ai-specs/specs/documentation-standards.mdc`.
- **Notes**: MANDATORY before considering the implementation complete.

---

## Implementation Order

1. **Step 0** — Create branch `feature/feat-anniversary-history-map-frontend` off `dev`.
2. **Step 1** — Types in `types/camp-history.ts`.
3. **Step 2** — `useCampHistory` composable + its tests (TDD: the grouping logic is the one piece
   with real logic in it, write the test first).
4. **Step 3** — Extend `CampLocationMap.vue` and `CampLocation`. **Run the existing map tests
   unmodified before moving on** — they are the regression gate for the four other pages.
5. **Step 4** — `AnniversaryVenueList.vue`.
6. **Step 5** — `AnniversaryYearStrip.vue`.
7. **Step 6** — `AnniversaryJourney.vue` container, including presentation mode.
8. **Step 7** — Gallery `year` prop.
9. **Step 9** — Mount in `AnniversaryPage.vue`. *(Step 8 is deliberately after this — see below.)*
10. **Step 8** — `AnniversaryTimeline.vue` props refactor, **only if verified milestone text
    exists**. Otherwise leave the section commented out and record the decision.
11. **Step 10** — Complete the Vitest suite.
12. **Step 11** — Cypress E2E (optional).
13. **Step 12** — Documentation update (mandatory).

**If time runs short before the presentation**, the deliverable that stands alone is
Steps 0-7 + 9 + 10: map, list, year strip, gallery linking, empty-state call to action and
presentation mode. Steps 8 and 11 are the ones to drop, and dropping them should be stated openly
rather than left implicit.

---

## Testing Checklist

**Automated**

- [ ] `npm run test:run` — all suites green, including the four new files.
- [ ] `npm run build` (`vue-tsc --noEmit && vite build`) — no type errors, no `any`.
- [ ] `npm run lint` clean.
- [ ] The **pre-existing** `CampLocationMap.test.ts` cases pass **without modification**.
- [ ] Coverage: `useCampHistory` grouping fully covered (all branches).
- [ ] Presentation mode's interval is proven cleared on unmount.

**Manual on `/anniversary` (logged in as a member)**

- [ ] The map opens with all 31 pins fitted to Spain; no grey tiles in the 60/40 split.
- [ ] Clicking a pin highlights and scrolls the matching list row into view.
- [ ] Clicking a list row centres the map and opens that venue's popup.
- [ ] A venue's years appear both in its list row and in the map popup, and both are clickable.
- [ ] Espinosa de los Monteros shows **4** editions (1983, 1993, 2003, 2015) and a visibly larger pin.
- [ ] Selecting a year filters the gallery to that year; "Ver todos los años" clears it.
- [ ] A year with `photoCount: 0` shows the call to action naming the real year and venue, and its
      button scrolls to the upload form.
- [ ] A year with photos shows up to three preview thumbnails without a second network request
      (check the Network tab: **one** call to `/camps/history`).
- [ ] Presentation mode walks all 50 years without jumps and stops on manual interaction.
- [ ] Mobile (≤ 640 px): map ~55 vh on top, list below, chips tappable, no horizontal page scroll.
- [ ] Keyboard: every year chip is reachable by tab and activates with Enter/Space.
- [ ] Logged out, `/anniversary` redirects to login (the endpoint returns 401 by design).

---

## Error Handling Patterns

- **Composable owns `loading` / `error` / data refs** — the pattern in `useMediaItems.ts`. Components
  render states; they never build error strings.
- **API errors**: read `response.data.error?.message` first, fall back to a Spanish user-facing
  message, and `console.error` the raw error for diagnosis. Axios interceptors in `@/utils/api`
  already handle 401 redirects — do not re-handle 401 in the composable.
- **Failure is visible, never blank**: on `error`, render the warning-icon block
  `AnniversaryGallery.vue:39-43` already uses. A blank map container reads as a broken page.
- **Partial data is normal**: a venue without coordinates is skipped on the map but must still appear
  in the list. Never let one bad row take down the whole journey.
- **No Toast for load failures** — this is a read-only page render, not a user action. Toasts are for
  the results of things the user did (`frontend-standards.mdc`, "Toast Notifications").

---

## UI/UX Considerations

- **PrimeVue**: `Button` (presentation toggle, call to action), `Skeleton` (loading), `Timeline`
  (Step 8 only). Year chips and venue rows are plain buttons with Tailwind — a PrimeVue `Chip` is not
  interactive and `Tag` is not a button.
- **Tailwind / palette**: keep the amber scale already used across the anniversary components
  (`amber-900` headings, `amber-500` markers, `amber-50` section background).
- **Responsive**: mobile-first. `grid-cols-1` → `lg:grid-cols-5` (3 + 2). Map `h-[55vh]` on mobile,
  `h-[600px]` from `lg`. The year strip scrolls horizontally on every breakpoint.
- **Accessibility**:
  - year chips are `<button>` with `aria-label` naming year **and** venue;
  - `aria-current="true"` on the selected year;
  - the map section carries `aria-label="Mapa de campamentos históricos"`;
  - the list is the keyboard-accessible equivalent of the map — a Leaflet map is not operable by
    keyboard, so the list is not decorative, it is the accessible path;
  - honour `prefers-reduced-motion` in presentation mode and in every `scrollIntoView`.
- **Loading states**: skeletons shaped like the final layout (a map-sized block and a list of rows),
  not a spinner — the layout shift from a spinner to a 60/40 grid is jarring on a projector.

---

## Dependencies

- **npm packages**: none new. `leaflet` and `primevue` are already dependencies.
- **PrimeVue components**: `Button`, `Skeleton`, `Timeline` (Step 8), `Image` (already in the
  gallery).
- **API**: `GET /api/camps/history` — merged to `dev` in PR #285. No backend work in this phase.

---

## Notes

### Business rules

- One year = one edition = one venue. The grouping and the year-based gallery filter both rest on
  this; it holds for 1976–2025 (50 editions, no gaps, no repeats). If a future year ever has two
  editions, the year-keyed lookups break — worth a comment in `useCampHistory` so the assumption is
  visible.
- `photoCount: 0` is meaningful data, not an error. It is the difference between "nothing survives
  from 1987" and "not loaded yet", and it is what turns a gap into a call to action. Never render it
  as a spinner or hide the row.
- `photoCount` counts `Type = Photo` only. A year with only audio reads as `0` — correct today,
  misleading once camp audio capture is live (Phase 4). Do not paper over it in the frontend by
  relabelling the count; the clean fix is a separate audio counter in the API.
- Showing photos on the map **is publishing them** to every member. This makes the deferred Phase 3.7
  takedown mechanism more urgent, not less. Flag it again when this ships.

### Language requirements

- **Code, comments, types, test names, commit messages: English** (`base-standards.mdc`).
- **User-facing text: Spanish**, matching the existing anniversary components.

### TypeScript

- Strict mode, no `any`, `<script setup lang="ts">` in every component, explicit `defineProps` /
  `defineEmits` generics. The one existing `any` in `CampLocationMap.vue` (the Leaflet icon
  workaround, line 22) is pre-existing — leave it, do not add new ones.

### Possible backend follow-ups

Neither blocks Phase 3; note them if they come up:

1. A gallery endpoint returning photos for several years at once, if the year-by-year gallery
   refetch proves slow in practice. Measure before building.
2. An audio counter alongside `photoCount`, needed once Phase 4 lands (see business rules).

---

## Next Steps After Implementation

1. Open a PR from `feature/feat-anniversary-history-map-frontend` **to `dev`** (never `main`).
2. Update the enriched spec: Phase 3 `✅ HECHO`, checklist ticked, deviations recorded.
3. Demo on the actual presentation hardware — a projector at 1920×1080 is not a laptop screen, and
   the 60/40 split and year strip should be checked there before the day.
4. Revisit the **MVP phase** (enabling the upload form), which the spec marks `⭐ PRIORIDAD` and which
   now has what it was waiting for: the edition selector can be fed straight from `useCampHistory`.
5. Re-raise Phase 3.7 (takedown mechanism) now that photos are visible on the map.

---

## Implementation Verification

- **Code Quality**
  - [ ] `<script setup lang="ts">` in all new components; no `any`; strict types.
  - [ ] No direct `api` calls from components — all data access via `useCampHistory` / `useMediaItems`.
  - [ ] `CampLocationMap` changes are strictly additive; all new props optional with
        behaviour-preserving defaults.
  - [ ] Popup content built with `textContent`, not `innerHTML`.
  - [ ] `npm run lint` and `vue-tsc --noEmit` clean.
- **Functionality**
  - [ ] Map ↔ list ↔ strip ↔ gallery stay synchronised through the single `selectedYear`.
  - [ ] Exactly one request to `/camps/history` per page load.
  - [ ] Presentation mode runs and cleans up.
- **Testing**
  - [ ] `npm run test:run` green; new Vitest files cover composable, components and container.
  - [ ] Existing `CampLocationMap.test.ts` unmodified and passing.
- **Integration**
  - [ ] Verified against real data: 50 rows, 1976–2025, 31 venues, Espinosa de los Monteros 2015 as
        edition 4 of 4.
  - [ ] 401 handling verified logged out.
- **Documentation**
  - [ ] `frontend-standards.mdc` "Maps Integration" updated with the extended contract.
  - [ ] Enriched spec updated with Phase 3 status and deviations.
