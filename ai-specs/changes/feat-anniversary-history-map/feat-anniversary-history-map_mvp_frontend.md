# Frontend Implementation Plan: feat-anniversary-history-map — MVP Phase (contributing at the camp)

## Overview

Turn the anniversary upload form back on so people can contribute **during the 2026 camp**. This is
the phase the enriched spec marks `⭐ PRIORIDAD`, and it is deliberately small: five changes, all in
one component plus one composable.

The governing principle is **collect, don't publish yet**. Everything contributed lands in the
approval queue that already exists, and the board decides later what gets published. That keeps the
consent problem at its minimum while still capturing the material — which is the urgent part,
because the camp does not happen twice.

Verified: `MediaItemsService` already sets `IsApproved = isInternalMedia` (false for anniversary
uploads) and `IsPublished = false`
([`MediaItemsService.cs:40-41`](../../../src/Abuvi.API/Features/MediaItems/MediaItemsService.cs#L40-L41)).
**No backend change is needed for the approval behaviour** — it is already correct.

Architecture principles: Vue 3 Composition API, `<script setup lang="ts">`, strict TypeScript,
composables for all API access, PrimeVue + Tailwind, amber palette.

### Scope: frontend only, with one decision that is not

Every change below is frontend. **But one prerequisite is not a code change at all** and blocks the
edition selector from doing its job — see the next section. Resolve that first or the QR deep link
points at a year the form cannot offer.

---

## Blocking prerequisite: the 2026 edition is not reachable by any endpoint

This was checked against the real data, not assumed.

- `GET /api/camps/history` returns **only `Completed`** editions — 1976–2025. By design.
- `GET /api/camps/editions/active` returns only editions with status **`Open`**.
- `GET /api/camps/current` walks a priority list: current year `Open`, current year `Closed`,
  previous year `Completed`, previous year `Closed`. **`Draft` appears nowhere in it.**

The 2026 camp has just been recorded as **El Clar del Bosc, Girona** in
`docs/CAMPAMENTOS_HISTORICOS.csv` and `docs/CAMPAMENTOS_EDICIONES_HISTORICOS.csv`, but:

1. Its status is **`Draft`** — the venue is decided, the registration state was not something that
   change could assert. **No endpoint returns a `Draft` edition**, so the selector cannot offer 2026.
2. It has **no coordinates** (`geocodeStatus = pending`): there is no Google Places key configured,
   so it was not geocoded, and the import gate deliberately refuses rows with editions that are
   still pending. **The row is not in the database yet.**

### What has to happen before the selector works

| Step | Owner | Note |
| --- | --- | --- |
| Geocode El Clar del Bosc, or set its coordinates by hand and mark `ok_manual` | whoever has the Places key, or the coordinate workbench built in Phase 1.6 | Without this the import gate blocks the row |
| Import the venue and the 2026 edition | `dotnet run --project src/Abuvi.Setup -- import camps` then `import camp-editions` | |
| Promote the 2026 edition from `Draft` to `Open` | the board, via the admin UI | This is a decision about the association, not a technical step |

**Until then, build against the fallback described in Step 3** — the plan does not depend on this
being resolved first, but the deep link and the "current camp" option stay inert until it is.

---

## Architecture Context

### Files to modify

| File | Change |
| --- | --- |
| `frontend/src/components/anniversary/AnniversaryUploadForm.vue` | Enable it, swap the year input for an edition selector, consent checkbox, review notice, contributor wording, query prefill |
| `frontend/src/composables/useCampHistory.ts` | Add the current (non-completed) edition to the options it can offer |
| `frontend/src/components/anniversary/__tests__/AnniversaryUploadForm.test.ts` | Cover the new behaviour; existing cases assume `comingSoon` |
| `frontend/src/composables/__tests__/useCampHistory.test.ts` | Cover the new option list |

### Files to create

| File | Purpose |
| --- | --- |
| `frontend/src/components/anniversary/AnniversaryConsentNotice.vue` *(optional)* | The review + takedown notice, if it is wanted in more than one place |

### Routing

**No route changes.** `/anniversary` stays `requiresAuth: true`
([`router/index.ts:42`](../../../frontend/src/router/index.ts#L42)). The spec is explicit that access
stays as it is, and that people without an account contribute through an **assisted route**: someone
from the board with a session open uploads the material on the spot. That needs no code.

### State management

Local component state. No Pinia. The form already uses a `reactive` object; keep it.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a frontend-specific branch.
- **Branch Naming**: `feature/feat-anniversary-history-map-mvp-frontend`.
- **Implementation Steps**:
  1. `git checkout dev && git pull origin dev`
  2. `git checkout -b feature/feat-anniversary-history-map-mvp-frontend`
  3. `git branch`
- **Notes**: PRs target `dev`, never `main`. Phases 2 and 3 are already merged into `dev`, so
  `useCampHistory` and `GET /api/camps/history` are available.

---

### Step 1: Offer the current edition alongside the history

- **File**: `frontend/src/composables/useCampHistory.ts`
- **Action**: Add an option list that covers both the 50 completed editions **and** the camp that is
  happening now, which the history endpoint deliberately excludes.
- **Function/Component Signature**:

```typescript
export interface CampEditionOption {
  year: number
  /** "2003 — Espinosa de los Monteros", or "2026 — El Clar del Bosc" */
  label: string
  campName: string | null
  /** True for the camp currently running: it is not part of the history yet. */
  isCurrent: boolean
}

// added to the existing return value
editionOptions: ComputedRef<CampEditionOption[]>
fetchEditionOptions: () => Promise<void>
```

- **Implementation Steps**:
  1. Keep `fetchHistory` untouched. Add `fetchEditionOptions`, which calls `fetchHistory` **and**
     `GET /camps/current` (already wrapped by `useCampEditions.fetchCurrentCampEdition`, so reuse
     that rather than adding a second axios call).
  2. `editionOptions` maps the history to `{ year, label: `${year} — ${campName}`, isCurrent: false }`
     in **descending** year order — the most recent first, because that is what somebody at the camp
     wants, and 1976 is the rare case.
  3. If the current edition exists and its year is not already in the list, prepend it with
     `isCurrent: true`.
  4. **Tolerate the current endpoint failing.** If `/camps/current` 404s — which it does today,
     because the 2026 edition is `Draft` — the options are just the history. Never let that failure
     empty the list or surface an error: the form must still work.
- **Dependencies**: `@/composables/useCampEditions`, existing types.
- **Implementation Notes**:
  - Do **not** widen `GET /api/camps/history` to include non-completed editions. It would change what
    `photoCount` means (a future year has nothing to preserve, which is not the same as a lost year)
    and would put the 2026 camp on the anniversary map as if it were history.
  - Descending order is a deliberate departure from the map, which reads chronologically. Different
    job: the map tells a story, the selector is a control.

---

### Step 2: Enable the form

- **File**: `frontend/src/components/anniversary/AnniversaryUploadForm.vue`
- **Action**: Remove the "coming soon" gate.
- **Implementation Steps**:
  1. Delete `const comingSoon = true`
     ([line 43](../../../frontend/src/components/anniversary/AnniversaryUploadForm.vue#L43)) and
     every branch that reads it, including the "Próximamente" notice in the template.
  2. Check the ticket that disabled it,
     `ai-specs/tickets/disable_anniversary_upload_enriched.md`, for anything else it turned off.
- **Implementation Notes**: Genuinely one line plus the template block. Do it early: everything
  after this is testable in the browser only once the form renders.

---

### Step 3: Replace the year input with an edition selector

- **File**: `frontend/src/components/anniversary/AnniversaryUploadForm.vue`
- **Action**: Swap the bare `InputNumber` for a PrimeVue `Select` of real editions.
- **Implementation Steps**:
  1. Remove the `InputNumber` import and its field; add `Select` (already imported for content type).
  2. Bind to `editionOptions` with `optionLabel="label"` and `optionValue="year"`. The submit payload
     still sends `year`, so `createMediaItem` / `createMemory` are unchanged.
  3. Group or mark the current camp visibly — *"2026 — El Clar del Bosc (este campamento)"* — so
     somebody standing in it picks the right one without thinking.
  4. **Delete the `form.year ?? 2026` fallbacks** in `handleSubmit`
     ([lines 76 and 113](../../../frontend/src/components/anniversary/AnniversaryUploadForm.vue#L76)).
     A hardcoded year silently mis-files a memory; once the field is a real choice, make it
     **required** in `validate()` instead.
  5. **Fallback for an unknown year.** If the query string names a year that is in neither list (the
     2026 case today), still show it as a bare `2026` option and preselect it, rather than dropping
     the QR's intent on the floor. A one-line union in the computed, and it is what stops the whole
     feature from being blocked on the `Draft` promotion.
- **Implementation Notes**: `Select` with 51 options is fine; add `filter` so typing a year narrows it.

---

### Step 4: Deep link from the QR code

- **File**: `frontend/src/components/anniversary/AnniversaryUploadForm.vue`
- **Action**: Prefill from the query string so the printed QR lands somebody on a form that is ready.
- **Target URL**: `/anniversary?tipo=audio&anio=2026#subir-recuerdo`
- **Implementation Steps**:
  1. `const route = useRoute()`; on mount, read `tipo` and `anio`.
  2. Map `tipo` through the existing `contentTypes` values (`foto`, `video`, `audio`, `historia`).
     **Ignore an unrecognised value** rather than erroring — a mistyped poster must not produce a
     broken page.
  3. `anio` → `Number`; ignore anything that is not a plausible year.
  4. The `#subir-recuerdo` anchor already exists on the section
     ([`AnniversaryUploadForm.vue:147`](../../../frontend/src/components/anniversary/AnniversaryUploadForm.vue#L147)),
     but browsers do not always honour a fragment on an SPA's first paint. Scroll to it explicitly
     on mount when the fragment is present.
- **Implementation Notes**: Spanish parameter names (`tipo`, `anio`) come from the spec and end up
  printed on a poster. Keep them exactly as specified — this is the one place where a rename is
  expensive after the fact.

---

### Step 5: Mandatory image-rights declaration

- **File**: `frontend/src/components/anniversary/AnniversaryUploadForm.vue`
- **Action**: A checkbox that must be ticked before the form can be sent.
- **Exact wording** (from the spec, do not paraphrase):
  > *Tengo derecho a compartir esta imagen y respeto la privacidad de quienes aparecen.*
- **Implementation Steps**:
  1. PrimeVue `Checkbox` with `binary`, bound to `form.rightsAccepted`.
  2. Add to `validate()`: unticked → `errors.rights = 'Debes aceptar esta declaración para enviar'`.
  3. Reset it in `resetForm()`.
  4. Associate the label with the input (`inputId` + `<label for>`) so it is reachable by keyboard
     and read correctly by a screen reader.
- **Implementation Notes**:
  - **This is not stored anywhere.** It gates the form in the browser and nothing more. That is a
    real limitation and the plan states it rather than implying a record exists: if the association
    later needs to prove consent was declared, that needs a backend field and is a separate ticket.
    Say so in the PR.
  - Show it for every content type, not just photos. A written story can name people too.

---

### Step 6: Visible notice about review and takedown

- **File**: `frontend/src/components/anniversary/AnniversaryUploadForm.vue`
- **Action**: Tell people plainly what happens to what they send.
- **Content**: that everything is reviewed before publication, and who to contact to have something
  removed.
- **Implementation Steps**:
  1. A short block above the submit button, in the amber palette, not a dismissible toast — it has
     to be visible while deciding to send, not after.
  2. Point the takedown route at the existing contact section (`#contacto`) unless the board gives a
     specific address.
- **Implementation Notes**: **The takedown contact is a real address that must reach a person.** If
  nobody confirms one, use the existing contact form rather than inventing an email. Flag it in the
  PR if it goes in unconfirmed.

---

### Step 7: Make it clear you can contribute on someone else's behalf

- **File**: `frontend/src/components/anniversary/AnniversaryUploadForm.vue`
- **Action**: Wording only — no new field.
- **Implementation Steps**: The form already has a name field. Change its help text to say the name
  can be **the person who remembers, not the person uploading**. This is what makes the assisted
  route work: a board member uploads for someone with no account, and the memory keeps the right
  name on it.
- **Implementation Notes**: The uploading account is still recorded in `UploadedByUserId`, so
  authorship is not lost — the name field is about attribution of the memory, not of the upload.

---

### Step 8: Tests

- **Files**: `AnniversaryUploadForm.test.ts`, `useCampHistory.test.ts`
- **Implementation Steps**:
  1. **`useCampHistory`**: options built from history, descending; the current edition prepended and
     marked; no duplicate when the current year is already in the history; `/camps/current` failing
     leaves the history options intact.
  2. **Form — gating**: cannot submit with the rights box unticked, and the error names it; ticking
     it allows submission.
  3. **Form — edition**: the year is required; the submitted payload carries the selected year; the
     `?? 2026` fallback is gone (submit with no year selected must fail validation, not default).
  4. **Form — deep link**: `?tipo=audio&anio=2026` preselects both; an unrecognised `tipo` is ignored
     without throwing; a year in neither list still appears and is selected.
  5. **Form — enabled**: the "Próximamente" notice is gone and the fields are interactive. **Existing
     tests assume `comingSoon`** — expect to rewrite several.
  6. Mock `useCampHistory`, `useBlobStorage`, `useMediaItems` and `useMemories` following the pattern
     in `AnniversaryGallery.test.ts`.
- **Implementation Notes**: The rights checkbox is the one thing here with a legal edge. Test it
  properly, including that `resetForm` clears it — a sticky ticked box after a successful submit
  would mean the next contributor never actually declared anything.

---

### Step 9: Update Technical Documentation

- **Action**: Review and update technical documentation according to the changes made.
- **Implementation Steps**:
  1. **Review Changes**: list every file touched.
  2. **Identify Documentation Files**:
     - `ai-specs/changes/feat-anniversary-history-map/feat-anniversary-history-map_enriched.md` —
       mark the MVP phase done, tick its acceptance criteria, and record what the rights checkbox
       does **not** do (it is not persisted).
     - `ai-specs/specs/frontend-standards.mdc` — only if a new reusable pattern appears (a consent
       notice component, or a query-prefill convention worth naming).
     - `ai-specs/tickets/disable_anniversary_upload_enriched.md` — mark it resolved; it is the ticket
       being undone here.
     - `ai-specs/specs/api-endpoints.md` — **verify only**. No API change is expected.
  3. **Update Documentation**: English, matching existing structure.
  4. **Verify Documentation**: confirm every change is reflected.
  5. **Report Updates**: state which files changed and how.
- **References**: `ai-specs/specs/documentation-standards.mdc`.
- **Notes**: MANDATORY before considering the implementation complete.

---

## Implementation Order

1. **Step 0** — Branch.
2. **Step 2** — Enable the form (do it first; nothing else is visible until it renders).
3. **Step 1** — Edition options in `useCampHistory`.
4. **Step 3** — Edition selector, including the unknown-year fallback.
5. **Step 5** — Rights checkbox.
6. **Step 6** — Review and takedown notice.
7. **Step 4** — QR deep link.
8. **Step 7** — Contributor wording.
9. **Step 8** — Tests.
10. **Step 9** — Documentation.

**If time runs short**, steps 2, 5 and 6 are the ones that cannot be dropped: an enabled form that
collects nothing is useless, and a form that collects photos of people without a declaration or a
notice should not ship at all. The selector can fall back to a plain year field for one camp; the
consent pieces cannot fall back to anything.

---

## Testing Checklist

**Automated**

- [ ] `npm run test:run` green, including the rewritten form tests.
- [ ] `npm run build` (`vue-tsc --noEmit && vite build`) clean.
- [ ] Note: `npm run lint` **cannot run in this repo** — ESLint 10 is installed but there is no
      `eslint.config.js`. Pre-existing; do not treat a failure here as caused by this work.

**Manual on `/anniversary`, on a real phone**

- [ ] Upload a photo end to end; it appears in the admin approval queue **unpublished**.
- [ ] Upload an audio file end to end.
- [ ] Submitting without ticking the rights box is refused, and the message says why.
- [ ] The rights box is unticked again after a successful submit.
- [ ] The edition selector lists real editions labelled *"2003 — Espinosa de los Monteros"*.
- [ ] No year selected → validation error, **not** a silent 2026.
- [ ] `/anniversary?tipo=audio&anio=2026#subir-recuerdo` opens scrolled to the form with both fields
      preselected.
- [ ] A nonsense `?tipo=xyz` does not break the page.
- [ ] The review-and-takedown notice is readable without scrolling past the submit button.
- [ ] The whole flow works on a phone, on camp-grade connectivity — test with the network throttled,
      not just on office wifi. **This is the condition the feature actually ships into.**

---

## Error Handling Patterns

- The form already routes failures through PrimeVue `Toast` with the composable's error message.
  Keep that; it is correct here, because these are results of an action the user took.
- **A failed upload must not clear the form.** Somebody who just recorded a 3-minute story on a bad
  connection cannot be asked to type it again. Verify `resetForm()` runs only after success.
- `/camps/current` failing is expected, not exceptional (see Step 1). It must never surface to the
  user.

---

## UI/UX Considerations

- **PrimeVue**: `Select` (content type, edition), `Checkbox` (rights), `FileUpload`, `Textarea`,
  `InputText`, `Button`, `ProgressBar`, `Toast` — all already in the file except `Checkbox`.
- **Mobile first.** This form's primary device is a phone held by somebody standing in a field. Big
  tap targets, no horizontal scroll, the submit button reachable with a thumb.
- **Accessibility**: the rights checkbox needs a real associated label; validation errors need
  `aria-describedby` on the field they belong to.
- **Upload feedback**: a large audio file on a weak connection takes a while. The existing
  `ProgressBar` must stay visible and honest.

---

## Dependencies

- **npm packages**: none new.
- **PrimeVue components**: `Checkbox` is the only addition.
- **API**: none new. `GET /camps/history` and `GET /camps/current` both exist.

---

## Notes

### Business rules

- **Everything is collected unpublished.** Already true in `MediaItemsService`; do not add any path
  that publishes directly.
- **The rights checkbox is a gate, not a record.** Stated again because it is easy to assume
  otherwise: nothing is stored. If proof is needed, that is a backend ticket.
- **Showing photos on the map is publishing them** to every member. The takedown mechanism (deferred
  Phase 3.7) becomes more urgent with every photo collected here, not less.
- `photoCount` counts `MediaItemType.Photo` only. Once audio arrives from this camp, a year holding
  only audio still reads as `0` on the map. Known, documented, and the clean fix is a separate audio
  counter — **not** widening `photoCount`.

### Language

- Code, comments, tests, commits: **English**. User-facing text: **Spanish**.
- The QR parameter names stay Spanish (`tipo`, `anio`) because they get printed.

### Out of scope

Per the spec: email campaign, forwardable links, unidentified accounts, the `Contributor` role, the
identification queue, an open gallery, and tabbed navigation. Phases 3.5–3.7 stay deferred.

---

## Next Steps After Implementation

1. Open a PR to **`dev`**.
2. Resolve the blocking prerequisite above so the 2026 edition actually exists and is selectable.
3. Print the QR pointing at the deep link and **test it by scanning the printed poster**, not by
   typing the URL.
4. Confirm the takedown contact reaches a real person before the camp starts.
5. Re-raise Phase 3.7: this phase starts accumulating photographs of identifiable people, many of
   them minors.

---

## Implementation Verification

- **Code Quality**
  - [ ] `<script setup lang="ts">`, no `any`, strict types.
  - [ ] No `?? 2026` or any other hardcoded year left in the component.
  - [ ] No direct `api` calls from the component.
- **Functionality**
  - [ ] A photo and an audio file both reach the approval queue unpublished.
  - [ ] The form cannot be submitted without the rights declaration.
  - [ ] The deep link preselects type and year.
- **Testing**
  - [ ] `npm run test:run` green; the form's `comingSoon` tests replaced, not deleted.
- **Integration**
  - [ ] Verified against a running API with a real member session.
- **Documentation**
  - [ ] Enriched spec updated, including what the rights checkbox does not do.
  - [ ] `disable_anniversary_upload_enriched.md` marked resolved.
