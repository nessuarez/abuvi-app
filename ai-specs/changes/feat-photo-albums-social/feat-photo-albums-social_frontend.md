# Frontend Implementation Plan: feat-photo-albums-social — Albums, Themes, Provenance and Collaborative Dating (Task 4)

## Overview

Frontend for Task 4 of [feat-photo-albums-social_enriched.md](./feat-photo-albums-social_enriched.md), consuming the API merged in #289: camp edition albums, cross-cutting themes, provenance, comments, collaborative dating and attendance.

Architecture principles applied:

- **Vue 3 Composition API**, `<script setup lang="ts">` everywhere. No Options API.
- **Composables are the single point of API communication.** Components never call the API directly.
- **PrimeVue + Tailwind**, no custom `<style>` blocks.
- All user-facing text in **Spanish**; code, comments and types in **English**.
- Every route behind `requiresAuth: true`. The archive holds photographs of identifiable people, including minors.

---

## Read this first: the ground moved

The spec for this task was written before #287 shipped. Three things are no longer true, and one is an outright bug against the API we just merged.

### 1. The upload form silently stamps the wrong year — must fix

[AnniversaryUploadForm.vue](frontend/src/components/anniversary/AnniversaryUploadForm.vue) currently sends:

```ts
year: form.year ?? 2026,
```

When a member leaves the year blank, the item is filed as **2026**. Against the new backend that resolves to whatever edition exists in 2026, so an undated 1985 photo is silently recorded as a 2026 camp.

This is worse than a no-op. It produces **wrong data that looks correct**, and it keeps the unplaced pile empty so the community is never asked. It defeats the entire collaborative dating loop that Tasks 1-2 exist to enable.

**Fix (Step 4.1): send `campEditionId: null` and `year: undefined` when the member does not know.** This is the single highest-value change in the ticket.

### 2. The upload form is disabled behind a hardcoded flag

The same file has `const comingSoon = true`, which disables submission. Task 4 must flip it — with the board's agreement, since it opens contributions to all members.

### 3. `AnniversaryJourney` already does the map, the venue list and the year strip

#287 shipped `AnniversaryJourney`, `AnniversaryVenueList`, `AnniversaryYearStrip`, `CampLocationMap` and `useCampHistory`, with a presentation mode that steps through fifty years. `AnniversaryPage` already shares `selectedYear` between the journey and the gallery.

**Do not build `AlbumGrid` or `CampTimelineMap` as the enriched spec proposed** — they would duplicate working components. Task 4 *extends* the journey instead: each year gains a link into its album, and the map gains the viewer's own attendance highlight.

### 4. Two endpoints now overlap, and neither is wrong

| | `GET /api/camps/history` (#287) | `GET /api/camp-editions/albums` (#289) |
|---|---|---|
| Keyed by | year + campId | **campEditionId** |
| Has | `editionNumber`, `totalEditionsAtVenue`, `previewPhotos` | per-type counts, `memoryCount`, cover, `viewerAttended` |
| Missing | **`campEditionId`** | edition numbering, previews |

`camps/history` has no edition id, so the journey cannot link to an album on its own. **The frontend joins the two by year** — both endpoints document the one-edition-per-year assumption, and each returns all fifty rows in a single call, so this is a client-side `Map` lookup and not an N+1.

> Consolidating these two endpoints is worth doing later. It is **out of scope here**: this is a frontend ticket and merging them is a backend change with its own regression surface.

### 5. Cypress cannot run on the development machine

Per [fix-cypress-binary-fails-to-start_enriched.md](./fix-cypress-binary-fails-to-start_enriched.md), `Cypress.exe` answers `bad option: --smoke-test` and **all nine specs are blocked**. `anniversary-journey.cy.ts` was merged in #287 without ever being executed.

**Consequence for this plan:** Step 7 writes the E2E specs but they **cannot be verified here**. Do not mark the ticket done on the strength of a spec nobody ran — that is exactly the false coverage the Cypress ticket was raised about. Either fix the binary first, or land the ticket with the E2E step explicitly deferred and say so in the PR.

---

## Architecture Context

### New files

**Types** (`frontend/src/types/`)

```
album.ts            AlbumSummary, AlbumDetail, AlbumMediaItem, UnplacedMedia
media-source.ts     MediaSource, CreateMediaSourceRequest, NewMediaSource
media-theme.ts      MediaTheme, MediaThemeRef, ThemeItems
media-comment.ts    MediaComment, MediaCommentReport, report reasons
media-dating.ts     YearProposalTally, YearProposalGroup, ThemeYearHint, SourceHint
camp-attendance.ts  CampTimeline, CampTimelineEntry, AttendanceEntry
```

**Composables** (`frontend/src/composables/`)

```
useAlbums.ts          album index, album detail, unplaced pile
useMediaSources.ts    contributor catalogue, detail, create, merge
useMediaThemes.ts     catalogue, theme items, attach/detach
useMediaComments.ts   thread, create, edit, delete, report, moderation queue
useMediaDating.ts     tally, upsert, withdraw, admin override
useCampAttendance.ts  declare, withdraw, timeline
```

**Components** (`frontend/src/components/anniversary/`)

Media-neutral names throughout. Nothing here is photo-only — an implementer who sees `PhotoCard` builds a photo-only lightbox and the audio interviews have nowhere to live.

```
AlbumMediaGrid.vue      paged grid for one edition, type filter chips
MediaCard.vue           renders by type: img / audio / video / document
MediaLightbox.vue       full item + metadata + comments + themes + dating
MediaCommentThread.vue  list + composer, optimistic insert
MediaCommentItem.vue    one comment, edit/delete/report affordances
MediaDatingPanel.vue    tally bars, theme and source hints, year picker
MediaThemeChips.vue     themes on an item, add via autocomplete, remove
MediaSourceBadge.vue    "Aportado por Manolo García"
MediaSourcePicker.vue   upload-form control: Mío / De otra persona
ThemeGrid.vue           theme catalogue cards with year span
ThemeTimeline.vue       one theme's items grouped by year, undated last
SourceGrid.vue          contributor cards
SourceDetail.vue        everything one person contributed
AttendanceButton.vue    "Yo estuve en este campamento" toggle
```

**Views** (`frontend/src/views/anniversary/`)

```
AlbumDetailPage.vue     /anniversary/albums/:editionId
ThemesIndexPage.vue     /anniversary/temas
ThemeDetailPage.vue     /anniversary/temas/:slug
UnplacedMediaPage.vue   /anniversary/sin-ubicar
SourcesIndexPage.vue    /anniversary/aportaciones
SourceDetailPage.vue    /anniversary/aportaciones/:id
MyCampTimelinePage.vue  /anniversary/mis-campamentos
```

**Admin** (`frontend/src/components/admin/`)

```
MediaCommentReportsPanel.vue   moderation queue
MediaThemesAdminPanel.vue      theme catalogue CRUD
MediaSourcesAdminPanel.vue     contributor CRUD + merge; the only screen showing contact details
```

### Modified files

| File | Change |
|---|---|
| [AnniversaryUploadForm.vue](frontend/src/components/anniversary/AnniversaryUploadForm.vue) | **The year bug**, the `comingSoon` flag, edition selector, theme picker, source picker |
| [AnniversaryJourney.vue](frontend/src/components/anniversary/AnniversaryJourney.vue) | "Ver álbum" link per year, attendance highlight on the map |
| [AnniversaryYearStrip.vue](frontend/src/components/anniversary/AnniversaryYearStrip.vue) | Mark years the viewer attended |
| [AnniversaryPage.vue](frontend/src/views/AnniversaryPage.vue) | Nav entries for the new sections |
| [useMediaItems.ts](frontend/src/composables/useMediaItems.ts) | `campEditionId`, `unplacedOnly`, `themeId` params |
| [media-item.ts](frontend/src/types/media-item.ts) | New response and request fields |
| [memory.ts](frontend/src/types/memory.ts) | `campEditionId` |
| [router/index.ts](frontend/src/router/index.ts) | Seven routes |

### State management

**Local state via composables — no Pinia store.** The project reserves Pinia for genuinely cross-cutting state (`auth`, `camp-editions`); album and theme data is page-scoped and refetched on navigation.

The one exception worth considering: `useCampAttendance` timeline data is read by the journey, the year strip and the timeline page. Fetch it once in `AnniversaryPage` and pass down via props rather than reaching for a store — the tree is shallow.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to the frontend feature branch.
- **Branch Naming**: `feature/feat-photo-albums-social-frontend` — **required**. Do not work on a generic `feat-photo-albums-social` branch.
- **Implementation Steps**:
  1. `git checkout dev`
  2. `git pull origin dev` — **base branch is `dev`, not `main`**
  3. `git checkout -b feature/feat-photo-albums-social-frontend`
  4. `git branch` to verify
- **Notes**: FIRST step, before any code change. Consider a dedicated worktree following the repo convention `abuvi-app.worktrees/<branch-without-feature-prefix>`.

---

### Step 1: Define TypeScript Interfaces

- **Files**: the six new files in `frontend/src/types/`, plus edits to `media-item.ts` and `memory.ts`.
- **Action**: Mirror the API response records exactly. No `any`.

**`album.ts`**

```ts
export interface AlbumSummary {
  campEditionId: string
  year: number
  campId: string
  campName: string
  campLocality: string | null
  latitude: number | null
  longitude: number | null
  photoCount: number
  videoCount: number
  /** Includes items of type Interview. */
  audioCount: number
  documentCount: number
  memoryCount: number
  coverThumbnailUrl: string | null
  /** Declared attendance unioned with attendance derived from registrations. */
  viewerAttended: boolean
}

export interface AlbumMediaItem {
  id: string
  uploadedByUserId: string
  uploadedByName: string
  fileUrl: string
  thumbnailUrl: string | null
  type: MediaItemType
  title: string
  description: string | null
  year: number | null
  decade: string | null
  campEditionId: string | null
  yearSource: MediaItemYearSource
  commentCount: number
  mediaSourceId: string | null
  mediaSourceName: string | null
  /** Already trimmed by the API for non-Admin viewers. Never re-derive client-side. */
  sourcePathDisplay: string | null
  themes: MediaThemeRef[]
  isApproved: boolean
  isPublished: boolean
  displayOrder: number
  isPrimary: boolean
  createdAt: string
}

export type MediaItemYearSource =
  | 'Unknown' | 'Exif' | 'FolderName' | 'Uploader' | 'Community' | 'Admin'
```

**Implementation Notes**:

- `sourcePathDisplay` and `contributorContact` arrive **already filtered by the API according to the caller's role**. The frontend must never attempt to reconstruct them, and must never render `contributorContact` outside the admin panel.
- Extend `CreateMediaItemRequest` with `campEditionId?: string | null`, `themeIds?: string[]`, `mediaSourceId?: string`, `newSource?: NewMediaSource`, `sourcePath?: string`.

---

### Step 2: Create Composables

- **Files**: the six new files in `frontend/src/composables/`.
- **Action**: Follow [useCampHistory.ts](frontend/src/composables/useCampHistory.ts) exactly — `ref` state, `loading`/`error`, `ApiResponse<T>` unwrapping, Spanish error strings, `console.error` on catch, a module-level error constant.

**`useAlbums.ts`**

```ts
export function useAlbums() {
  const albums = ref<AlbumSummary[]>([])
  const album = ref<AlbumDetail | null>(null)
  const unplaced = ref<UnplacedMedia | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchIndex = async (): Promise<void> => { /* GET /camp-editions/albums */ }

  const fetchAlbum = async (
    editionId: string,
    params?: { page?: number; pageSize?: number; type?: MediaItemType; themeId?: string }
  ): Promise<void> => { /* GET /camp-editions/{id}/album */ }

  const fetchUnplaced = async (params?: {
    page?: number; pageSize?: number; type?: MediaItemType
    mediaSourceId?: string; suggestedForMe?: boolean
  }): Promise<void> => { /* GET /media-items/unplaced */ }

  /** Year -> edition id, for joining the #287 journey to its albums. */
  const editionIdByYear = computed(() =>
    new Map(albums.value.map((a) => [a.year, a.campEditionId]))
  )

  return { albums, album, unplaced, loading, error, fetchIndex, fetchAlbum, fetchUnplaced, editionIdByYear }
}
```

**`useMediaDating.ts`** exposes `tally`, `fetchTally`, `propose` (PUT), `withdraw` (DELETE) and `setYearAsAdmin` (PATCH). Every mutation returns the fresh tally, so the panel re-renders from the response rather than refetching.

**`useMediaComments.ts`** must support **optimistic insert with rollback**: push a provisional comment, replace it with the server's on success, remove it and toast on failure. Also surface `429` distinctly — `"Has enviado muchos comentarios seguidos. Espera un minuto."` — because a generic error there reads as a bug.

**Error messages** (Spanish, per [frontend-standards.mdc](ai-specs/specs/frontend-standards.mdc) § Language Standards):

| Composable | Message |
|---|---|
| `useAlbums` | `'No se pudieron cargar los álbumes'` |
| `useMediaThemes` | `'No se pudieron cargar los temas'` |
| `useMediaComments` | `'No se pudieron cargar los comentarios'` |
| `useMediaDating` | `'No se pudo cargar la datación'` |
| `useMediaSources` | `'No se pudieron cargar las aportaciones'` |
| `useCampAttendance` | `'No se pudo cargar tu histórico de campamentos'` |

---

### Step 3: Routing

- **File**: [router/index.ts](frontend/src/router/index.ts)
- **Action**: Add seven routes, all `requiresAuth: true`, all lazy-loaded, matching the existing style.

| Path | Name | Title |
|---|---|---|
| `/anniversary/albums/:editionId` | `anniversary-album` | `ABUVI \| Álbum del campamento` |
| `/anniversary/temas` | `anniversary-themes` | `ABUVI \| Temas` |
| `/anniversary/temas/:slug` | `anniversary-theme` | `ABUVI \| Tema` |
| `/anniversary/sin-ubicar` | `anniversary-unplaced` | `ABUVI \| Recuerdos sin ubicar` |
| `/anniversary/aportaciones` | `anniversary-sources` | `ABUVI \| Aportaciones` |
| `/anniversary/aportaciones/:id` | `anniversary-source` | `ABUVI \| Aportación` |
| `/anniversary/mis-campamentos` | `anniversary-my-camps` | `ABUVI \| Mis campamentos` |

> There is deliberately **no `/anniversary/albums` index route**: the journey from #287 already is the album index. Adding one would give members two competing front doors to the same fifty editions.

---

### Step 4: Fix and Extend the Upload Form

**File**: [AnniversaryUploadForm.vue](frontend/src/components/anniversary/AnniversaryUploadForm.vue)

#### 4.1 — The year bug (do this first, it is independently valuable)

```ts
// Before — silently files undated material as 2026:
year: form.year ?? 2026,

// After — an unknown year is a valid submission that feeds collaborative dating:
year: form.year ?? undefined,
campEditionId: form.campEditionId,   // null when "No lo sé"
```

Apply to **both** the `createMemory` and `createMediaItem` calls.

#### 4.2 — Edition selector

A `Select` of the fifty editions from `useAlbums().albums`, whose **first and pre-selected option is "No lo sé — que la comunidad lo ubique"** (value `null`).

Helper text under it: *"Tu recuerdo irá a la sección Sin ubicar, donde otros abuvinos podrán ayudar a datarlo."*

**The form must not block submission on a missing edition or year.** No validation rule may require either. This is the behavioural change that makes the whole feature work.

#### 4.3 — `MediaSourcePicker`

*"¿De quién es este material?"* — **Mío** (default, sends no source) or **De otra persona**, which reveals a name field, an optional autocomplete against existing contributors, and optional notes. The contact field renders **only for Admin/Board**.

#### 4.4 — Theme multi-select

Optional `MultiSelect` backed by `GET /api/media-themes`, sending `themeIds`.

#### 4.5 — `comingSoon`

Remove the flag and enable submission. **Confirm with the board before merging** — this opens contributions to every member.

#### 4.6 — `sourcePath`

When the browser supplies `webkitRelativePath` (directory uploads), send it as `sourcePath` without showing it in the form.

---

### Step 5: Album, Theme, Source and Timeline Views

#### 5.1 — `MediaCard.vue` renders by type

The component that makes this feature not-photo-only:

| Type | Rendering |
|---|---|
| `Photo` | PrimeVue `Image` with `preview`, `loading="lazy"` |
| `Audio` / `Interview` | inline `<audio controls preload="none">` — the anniversary gallery already does this; reuse, do not reinvent |
| `Video` | `<video preload="none">` with a poster |
| `Document` | icon + filename link |

#### 5.2 — `AlbumDetailPage.vue`

Header from `AlbumSummary` (year, venue, per-type counts, `AttendanceButton`), type filter chips, paged `AlbumMediaGrid`, and a separate relatos section from `GET /api/memories?campEditionId=`.

Empty state matters — most albums start at zero: *"Este campamento aún no tiene recuerdos. ¿Tienes alguno?"* with a link to the upload form.

#### 5.3 — `MediaLightbox.vue`

Full item, metadata, `MediaSourceBadge`, `MediaThemeChips`, `MediaCommentThread`, `MediaDatingPanel`.

**Accessibility is a requirement, not a nicety**: ←/→ between items, Esc to close, focus trapped inside, `role="dialog"` + `aria-modal="true"`, focus returned to the invoking card on close.

#### 5.4 — `MediaDatingPanel.vue`

Tally bars per proposed year, the viewer's own vote, and **both hint blocks**:

- Theme hints — *"Otras fotos de San Abuvino son de 1998, 2003 y 2011"*
- Source hint — *"Lo aportó Manolo García; sus otras fotos son de 1997 y 1998"* plus *"Venía en la carpeta: …/Verano 98/Selva de Oza"*

When `contributorUserId` is non-null, offer *"preguntar a esta persona"*.

Hide the vote control and show *"Fecha confirmada"* when `isResolved && yearSource === 'Admin'`.

#### 5.5 — Journey integration

- `AnniversaryJourney`: a **"Ver álbum"** action on the selected year, resolving the edition id through `editionIdByYear`. Handle a missing mapping by hiding the link, not by erroring.
- `AnniversaryYearStrip`: mark years the viewer attended.
- `CampLocationMap`: highlight venues the viewer attended — this is *"estos son tus campamentos"* over the fifty, and it reuses the existing map rather than adding a second one.

#### 5.6 — `MyCampTimelinePage.vue`

*"Has estado en 14 campamentos"* plus the full fifty-edition list from `GET /api/users/me/camp-timeline`, each row showing `attendanceSource` and its media count.

---

### Step 6: Vitest Unit Tests

Beside each component in `__tests__/`, following [AnniversaryGallery.test.ts](frontend/src/components/anniversary/__tests__/AnniversaryGallery.test.ts).

**The tests that matter most:**

- `AnniversaryUploadForm.test.ts` — **submitting with "No lo sé" selected posts `campEditionId: null` and no year, and succeeds.** Add a regression test asserting the payload **never contains a hardcoded 2026**
- `MediaCard.test.ts` — right control per type: `img` for Photo, `audio` for Audio *and* Interview, `video` for Video, link for Document
- `MediaCommentThread.test.ts` — optimistic insert, rollback on API error, edit/delete visibility driven by `canEdit`/`canDelete`, distinct `429` message
- `MediaDatingPanel.test.ts` — tally rendering, both hint blocks, resolved state hides the control
- `SourceDetail.test.ts` — renders a contributor's items across years and **never renders a contact detail**
- `ThemeGrid.test.ts` — year span and undated count
- `AttendanceButton.test.ts` — toggle, family-member selection, derived attendance is not withdrawable
- Composable tests mirroring `useMediaItems.test.ts`

---

### Step 7: Cypress E2E Tests

`frontend/cypress/e2e/`:

- `anniversary-albums.cy.ts` — journey → year → album → lightbox → comment
- `anniversary-dating.cy.ts` — open an unplaced item, propose a year, see the tally update
- `anniversary-themes.cy.ts` — theme page shows items from several years

> **These cannot be verified on the development machine** (see hallazgo 5). Write them, then either fix the Cypress binary per [fix-cypress-binary-fails-to-start_enriched.md](./fix-cypress-binary-fails-to-start_enriched.md) and run them, or state plainly in the PR that they are unexecuted. Do not let them pass as coverage.

---

### Step 8: Update Technical Documentation

- **Action**: **MANDATORY** before considering the implementation complete.
- **Implementation Steps**:
  1. **Review Changes** — seven routes, six composables, ~17 components, the upload-form fix.
  2. **Identify Documentation Files**:
     - [frontend-standards.mdc](ai-specs/specs/frontend-standards.mdc) — the media-neutral naming rule; the type-dispatch pattern in `MediaCard`; the lightbox accessibility pattern; extend § Maps Integration with the attendance highlight
     - [INDEX.md](ai-specs/changes/INDEX.md) — set Frontend `[x]` for `feat-photo-albums-social`
     - [api-endpoints.md](ai-specs/specs/api-endpoints.md) — only if a response shape turns out wrong in integration
  3. **Update Documentation** — English, matching existing structure.
  4. **Verify** — accurate and consistently formatted.
  5. **Report Updates** — list files changed and how.
- **References**: [documentation-standards.mdc](ai-specs/specs/documentation-standards.mdc).

---

## Implementation Order

1. **Step 0** — Branch off `dev`
2. **Step 4.1** — **The year bug.** Ship it first: it is a handful of lines, independently valuable, and every day it stays in place produces more mis-filed uploads
3. **Step 1** — Types
4. **Step 2** — Composables
5. **Step 3** — Routes
6. **Step 5.1-5.2** — `MediaCard`, `AlbumMediaGrid`, `AlbumDetailPage`
7. **Step 4.2-4.6** — The rest of the upload form
8. **Step 5.3** — Lightbox
9. **Step 5.4** — Comments and dating panel
10. **Steps 5.5-5.6** — Themes, sources, journey integration, timeline
11. **Step 6** — Vitest
12. **Step 7** — Cypress (see the caveat)
13. **Step 8** — Documentation

**If scope needs trimming**, cut in this order: the timeline page, then the contributor *pages* (the source badge stays), then collaborative dating. **Never cut Step 4.1 or the "No lo sé" option** — the first is a live data-corruption bug and the second is what fills the pile that everything else works on.

---

## Testing Checklist

- [ ] `npm run build` (runs `vue-tsc --noEmit`), `npm run lint`, `npm run test:run` all green
- [ ] **Upload with "No lo sé" posts `campEditionId: null` and no year, and the item appears in Sin ubicar**
- [ ] **No payload anywhere contains a hardcoded year**
- [ ] An album containing a photo, an audio, a video and a relato renders all four
- [ ] A theme page shows items from several different years in one view
- [ ] `contributorContact` renders nowhere outside `MediaSourcesAdminPanel`
- [ ] `sourcePathDisplay` is rendered as received, never reconstructed
- [ ] Lightbox: ←/→, Esc, focus trap, focus restored on close
- [ ] Responsive at 360 px, 768 px, 1280 px
- [ ] Existing `AnniversaryGallery` and `AnniversaryJourney` still work unchanged
- [ ] All user-facing text in Spanish; no "foto" in type-agnostic chrome

---

## Error Handling Patterns

Composables own `loading`, `error` and data refs; components render from them and never catch API errors themselves.

| Status | Handling |
|---|---|
| `400` | Field errors under the input, via the message in `error.message` |
| `403` | Hide the affordance rather than showing a failure. `canEdit`/`canDelete` already tell the UI what is permitted — a button that always errors is a bug |
| `404` | Empty state on pages, toast on actions |
| `409` | Toast: *"Ya has denunciado este comentario"* |
| `429` | Toast: *"Has enviado muchos comentarios seguidos. Espera un minuto."* |
| `500` | Generic toast; `console.error` in the composable |

Toasts via PrimeVue `useToast()`, per [frontend-standards.mdc](ai-specs/specs/frontend-standards.mdc) § Toast Notifications.

---

## UI/UX Considerations

- **PrimeVue**: `Image` (preview), `Skeleton`, `Select`, `MultiSelect`, `AutoComplete`, `Chip`, `Dialog`, `Button`, `Paginator`, `Textarea`, `Toast`, `ConfirmDialog`.
- **Tailwind**: 1 column mobile, 2 tablet, 4 desktop. `loading="lazy"` on thumbnails.
- **Never say "foto" in shared chrome.** Use *"recuerdo"* or *"contenido"* anywhere that can hold an audio or a document — the unplaced pile, the dating panel, the comment composer.
- **Framing matters.** The unplaced pile leads with its purpose, not an apology: *"Estos recuerdos aún no tienen campamento. Si reconoces alguno, ayúdanos a ubicarlo."* Provenance is recognition, not metadata: *"Aportado por Manolo García"*, linking to everything else they gave.
- **Accessibility**: labelled regions, keyboard-navigable lightbox, `aria-live` on the dating tally so a screen reader hears the vote land.
- Media loads `preload="none"`; only thumbnails in grids.

---

## Dependencies

**No new npm packages.** Everything uses PrimeVue components and Leaflet, both already present. If a lightbox library looks tempting, the accessibility requirements above are cheaper to meet by hand than to retrofit onto one.

---

## Notes

**Business rules**

- `campEditionId === null` means *"edition unknown"* — temporary, never a permanent category. The UI must never present it as "does not belong to a camp".
- Consensus needs ≥3 votes and ≥66%; the panel shows the tally so a member sees the disagreement before voting.
- `yearSource === 'Admin'` is final: hide the vote control.
- Attendance with `attendanceSource === 'Registration'` is real but not withdrawable — render it without a remove affordance.

**Language**

- User-facing text in **Spanish**. Code, comments, types and test names in **English**.

**Privacy**

- `contributorContact` and `sourcePathDisplay` are filtered **server-side by role**. The frontend renders what it receives and never reconstructs either. `contributorContact` appears in exactly one screen.
- Do not add a client-side "show full path" affordance. There is nothing to show that the server did not send, and adding one invites a future endpoint that leaks.

**TypeScript**

- Strict, no `any`, `<script setup lang="ts">` throughout.

---

## Next Steps After Implementation

1. Open the PR against **`dev`**, not `main`.
2. Confirm the `comingSoon` removal with the board before merging.
3. Seed the theme catalogue (Task 3 ships `media-themes.csv`) or the theme UI demos empty.
4. Task 3's bulk importer fills the unplaced pile — until it runs, Sin ubicar will look sparse and the dating flow is hard to demo convincingly.
5. Consider consolidating `camps/history` and `camp-editions/albums`; the year-join here works but is a seam that will confuse the next reader.

---

## Implementation Verification

**Code Quality**
- [ ] TypeScript strict, no `any`, `<script setup lang="ts">` everywhere
- [ ] Components never call the API directly — always through a composable
- [ ] No custom `<style>` blocks; Tailwind utilities only
- [ ] Media-neutral naming throughout; no `Photo*` component names

**Functionality**
- [ ] All seven routes work behind auth
- [ ] Every `MediaItemType` renders correctly in grid and lightbox
- [ ] Upload without an edition works end to end

**Testing**
- [ ] Vitest coverage on new composables and components
- [ ] Cypress specs written; **executed, or explicitly reported as unexecuted**

**Integration**
- [ ] Response shapes match the API merged in #289
- [ ] `AnniversaryJourney` links into albums via the year join
- [ ] #287 components still behave as before

**Documentation**
- [ ] `frontend-standards.mdc` and `INDEX.md` updated
