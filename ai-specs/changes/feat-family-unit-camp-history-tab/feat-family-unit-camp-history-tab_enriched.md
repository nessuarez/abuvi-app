# "Camps" tab in the family unit page

**Feature ID:** `feat-family-unit-camp-history-tab`
**Date:** 2026-11-04
**Type:** Product feature (backend + frontend)
**Original request (ES):** "Sería interesante que dentro de cada unidad familiar se mostraran en una pestaña los campamentos a los que se ha asistido y/o inscripciones realizadas."
**Original request (EN):** It would be useful for each family unit to show, in a tab, the camps it has attended and/or the registrations it has made.

---

## 1. Summary

Today the family unit page ([FamilyUnitPage.vue](../../../frontend/src/views/FamilyUnitPage.vue)) only shows the family header and its members. Registrations live on a separate screen (`/registrations`, own family only), and declared historical attendance ("I was at this camp") lives in the personal timeline (`/api/users/me/camp-timeline`).

This story **combines both sources in a "Campamentos" tab** inside the family unit, so that:

- The family sees its full ABUVI history at a glance.
- Board/Admin users opening `/family-unit/:id` from administration see that family's history **without searching edition by edition** in the registration lists.

### What already exists (do not rebuild)

| Piece | Where | Limitation for this use case |
| --- | --- | --- |
| Family registrations | `GET /api/registrations` → `RegistrationsService.GetByFamilyUnitAsync` | Only resolves the **logged-in user's** family; Board cannot request another family's. Does not include registered members. |
| Declared attendance | `CampEditionAttendance` (table `camp_edition_attendances`) | Queried per user (`/users/me/camp-timeline`), not per family. |
| Registration-derived attendance | `CampEditionAttendanceRepository.GetRegisteredEditionIdsForUserAsync` | **Does not filter by status**: a cancelled registration counts as attendance. Do not reuse that rule as-is (see §5). |
| Tabs pattern | PrimeVue `Tabs/TabList/Tab/TabPanels/TabPanel` in [RegistrationDetailPage.vue](../../../frontend/src/views/registrations/RegistrationDetailPage.vue) | — |
| Registration card and status badge | `RegistrationCard.vue`, `RegistrationStatusBadge.vue` | Reusable. |

---

## 2. User stories

**US-1 — Family.** As the representative or a linked member of a family unit, I want to see in one tab every camp my family has attended or is registered for, so that my history is in one place and I can open the detail of each registration.

**US-2 — Board/Admin.** As a Board member or administrator viewing a family unit, I want to see its registrations (status, amounts, registered members) and its historical attendance, so that I have the family's context without switching screens.

---

## 3. Functional behavior

### 3.1 Page structure

`FamilyUnitPage.vue` organizes the family unit content (when it exists) into tabs, keeping the header card outside them:

```
[Family unit header: photo, name, Edit/Delete]   ← unchanged

[ Miembros | Campamentos ]
  Miembros     → current content (FamilyMemberList + buttons)  ← unchanged
  Campamentos  → new FamilyCampHistory component
```

- Default tab: **Miembros**.
- The active tab is reflected in the query string (`?tab=campamentos`) so it can be linked from administration and survives a refresh.
- "Campamentos" data is **lazy-loaded** the first time the tab is opened (not in `onMounted`).
- "No family unit" and loading states: unchanged.

### 3.2 "Campamentos" tab content

Chronological list, **descending by year**, one entry per camp edition. Each entry combines the registration and the declared attendance, when they exist.

**Top summary:** "Habéis participado en **N** campamentos" (N = entries with participation `Attended`, see §3.3). If there is an active registration (status other than `Cancelled` on an edition that has not ended), it is highlighted at the top as "Inscripción en curso".

**Each entry shows:**

| Data | Source | Notes |
| --- | --- | --- |
| Camp name and year | `CampEdition.Camp.Name`, `CampEdition.Year` | |
| Dates and location | `StartDate`, `EndDate`, `Camp.Location` | Only when there is a registration (historical editions may lack reliable dates). |
| Participation status | Computed (§3.3) | Labels: "Asistió", "Inscrita", "Cancelada". |
| Registration status | `Registration.Status` | `RegistrationStatusBadge`. Only when there is a registration. |
| Registered members | `RegistrationMember` → `FamilyMember` | Name + attendance period (`AttendancePeriod`). |
| Members with declared attendance | `CampEditionAttendance` | Name; "Declarado" tag. |
| Amounts | `TotalAmount`, paid, remaining | See §6 (visibility). |
| "Ver inscripción" action | — | Navigates to `/registrations/:id`. Only when there is a registration. |

**Filter:** a `SelectButton` with "Todos" (default) / "Inscripciones" / "Asistencias declaradas". A "Ocultar canceladas" checkbox (checked by default) hides cancelled entries; when unchecked, they are shown dimmed.

**Empty state:** "Esta familia aún no tiene campamentos registrados." For the user's own family, add a CTA "Inscribirse en un campamento" → `/camp` (only if an edition is open; reuse the existing available-editions logic if trivial, otherwise omit the CTA).

> UI copy is in Spanish (the app's user-facing language); everything else follows the English-only standard.

### 3.3 "Participation status" rule

Computed in the backend (single source of truth), per edition:

| Condition | `participation` |
| --- | --- |
| Registration with `Status = Cancelled` and no declared attendance | `Cancelled` |
| Non-cancelled registration and `CampEdition.EndDate < today` | `Attended` |
| Non-cancelled registration and edition not ended | `Registered` |
| No registration but declared attendance by some member | `Attended` |

> If an edition has several registrations from the same family (rare; `ExistsAsync` prevents it on creation, but it may exist in imported data), take the most recent non-cancelled one.

---

## 4. Backend

### 4.1 New endpoint

```
GET /api/family-units/{familyUnitId:guid}/camp-history
```

- **Feature folder:** `src/Abuvi.API/Features/FamilyUnits/` (the resource is the family; it consumes Registrations and Camps repositories).
- **Name:** `GetFamilyUnitCampHistory`.
- **Response:** `ApiResponse<FamilyCampHistoryResponse>`.
- **Status codes:** `200`, `401`, `403` (no permission on the family), `404` (family does not exist).

### 4.2 Authorization

Identical to `GetFamilyUnitById` in [FamilyUnitsEndpoints.cs](../../../src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs): **representative, linked family member, or Admin/Board role**. Extract that check into a shared helper if it is currently inline, to avoid duplication.

### 4.3 DTOs (in `FamilyUnitsModels.cs`)

```csharp
public enum FamilyCampParticipation { Attended, Registered, Cancelled }

public record FamilyCampHistoryResponse(
    int TotalAttended,
    IReadOnlyList<FamilyCampHistoryEntry> Entries);

public record FamilyCampHistoryEntry(
    Guid CampEditionId,
    string CampName,
    int Year,
    DateTime? StartDate,
    DateTime? EndDate,
    string? Location,
    FamilyCampParticipation Participation,
    FamilyCampRegistrationSummary? Registration,      // null when only declared attendance exists
    IReadOnlyList<FamilyCampMemberEntry> Members);

public record FamilyCampRegistrationSummary(
    Guid RegistrationId,
    RegistrationStatus Status,
    decimal? TotalAmount,       // null when the requester cannot see amounts (§6)
    decimal? AmountPaid,
    decimal? AmountRemaining,
    DateTime CreatedAt);

public record FamilyCampMemberEntry(
    Guid FamilyMemberId,
    string FullName,
    string Source,              // "Registration" | "Declared"
    AttendancePeriod? AttendancePeriod);   // only for Source = Registration
```

Enums serialized as strings (project convention).

### 4.4 Queries

- **Registrations repository:** new method `GetCampHistoryByFamilyUnitAsync(Guid familyUnitId, ct)` in `RegistrationsRepository`, `AsNoTracking`, including `CampEdition.Camp`, `Members.FamilyMember` and `Payments`. Do **not** extend `GetByFamilyUnitAsync`, to keep the `/api/registrations` list lean.
- **Attendance repository:** new method `GetDeclaredByFamilyUnitAsync(Guid familyUnitId, ct)` in `CampEditionAttendanceRepository`: attendances where `FamilyMember.FamilyUnitId == familyUnitId`, **or** `FamilyMemberId == null` and `User.FamilyUnitId == familyUnitId` (the declarer declared themselves; resolve their linked `FamilyMember` by `UserId` to show the name). Include `CampEdition.Camp`.
- **Amount paid:** sum of `Payments` with `Status == Completed`, same as `GetByFamilyUnitAsync`.
- Two queries total, no N+1. The merge and the `Participation` computation happen in memory in the service (tens of rows per family).

### 4.5 Service

New method in `FamilyUnitsService` (or a small `FamilyCampHistoryService` if `FamilyUnitsService` is already large): `GetCampHistoryAsync(Guid familyUnitId, Guid requesterId, string requesterRole, ct)`. Responsibilities: validate existence, apply §3.3, deduplicate members (if a member is registered and also declared for the same edition, keep only `Source = Registration`), sort by `Year` descending, and apply §6.

Use an injected `TimeProvider` (or the clock already used in the project) for "today", so tests are deterministic.

---

## 5. Separate finding (out of scope)

`CampEditionAttendanceRepository.GetRegisteredEditionIdsForUserAsync` counts **any** registration as attended, including cancelled registrations and future editions. This inflates the personal timeline's "has estado en N campamentos". Not fixed in this story, but a separate `fix-` should apply the §3.3 rule so both counters agree.

---

## 6. Non-functional requirements

- **Amount privacy:** amounts are returned to the representative and Admin/Board. For a linked non-representative member, return `null` (the UI hides the column). Confirm with product whether linked members should see them; default: hidden.
- **No sensitive data:** do not expose `SpecialNeeds`, internal accommodation notes, or members' medical/allergy data.
- **Performance:** 2 queries per request, `AsNoTracking`, response < 300 ms with 30 editions.
- **Read-only:** the endpoint changes nothing; actions (cancel, edit) stay in the registration detail.
- **Accessibility / mobile:** stacked cards on mobile (no table), consistent with `feat-family-member-mobile-ux`.
- **UI copy in Spanish**; code, identifiers and docs in English.

---

## 7. Frontend

### 7.1 Files

| File | Change |
| --- | --- |
| `frontend/src/types/family-unit.ts` | Types `FamilyCampHistoryResponse`, `FamilyCampHistoryEntry`, `FamilyCampRegistrationSummary`, `FamilyCampMemberEntry`, `FamilyCampParticipation`. |
| `frontend/src/composables/useFamilyUnits.ts` | `getFamilyCampHistory(familyUnitId)` → `GET /family-units/{id}/camp-history`, with state `campHistory`, `campHistoryLoading`, `campHistoryError`. |
| `frontend/src/components/family-units/FamilyCampHistory.vue` | **New.** Presentational: receives `history`, `loading`, `error`, `canSeeAmounts`; emits `view-registration`. Summary, filter, list, empty state. |
| `frontend/src/components/family-units/FamilyCampHistoryEntry.vue` | **New** (optional if the entry grows). Card for one edition; reuses `RegistrationStatusBadge`. |
| `frontend/src/views/FamilyUnitPage.vue` | Wrap "Miembros" and "Campamentos" in `Tabs`; sync `?tab=`; lazy load; navigate to `/registrations/:id`. |

Container-presentational pattern: `FamilyUnitPage` (container) calls the composable; `FamilyCampHistory` makes no HTTP calls.

### 7.2 Navigation to detail

`/registrations/:id` must allow Admin/Board access to other families' registrations. **Verify** that `GET /api/registrations/{id}` already allows it; if not, the "Ver inscripción" button for Board must link to the existing admin registration view instead of the user view.

---

## 8. Tests (strict TDD: RED → GREEN → REFACTOR)

### Backend (xUnit, `src/Abuvi.Tests/Unit/Features/FamilyUnits/`)

- Family with no registrations or attendances → empty `Entries`, `TotalAttended = 0`.
- Confirmed registration on a past edition → `Attended`, with members and `AttendancePeriod`.
- Registration on a future edition → `Registered`; not counted in `TotalAttended`.
- Cancelled registration without declaration → `Cancelled`; not counted.
- Declared attendance only (historical edition) → `Attended`, `Registration = null`, members with `Source = Declared`.
- Declared attendance with `FamilyMemberId = null` → resolved to the declarer's linked member.
- Member registered and declared for the same edition → appears once with `Source = Registration`.
- Sorted by year descending.
- Linked non-representative member → amounts `null`; representative and Admin/Board → amounts populated.

### Backend (integration, if an integration project exists for FamilyUnits)

- `403` for a user from another family without Board/Admin role.
- `404` for a non-existent family.
- `200` for Board on another family.

### Frontend (Vitest)

- `FamilyCampHistory.spec.ts`: renders summary, entries, empty state, filters; hides cancelled by default; hides amounts when `canSeeAmounts = false`; emits `view-registration`.
- `FamilyUnitPage.spec.ts` (extend): tabs visible; "Miembros" by default; `?tab=campamentos` opens the tab; history is fetched only when the tab opens, and only once.
- `useFamilyUnits` (extend): `getFamilyCampHistory` calls the correct URL and handles errors.

---

## 9. Documentation

- Add the endpoint to the API documentation in `ai-specs/specs/` (FamilyUnits section).
- User-facing changelog entry (`ChangelogPage` / the project's changelog file): "Nueva pestaña Campamentos en tu unidad familiar".

---

## 10. Acceptance criteria

- [ ] The family unit page shows "Miembros" and "Campamentos" tabs; "Miembros" keeps its current behavior.
- [ ] The "Campamentos" tab lists, by year descending, each edition with a registration and/or declared attendance by the family.
- [ ] Participation status follows the §3.3 rule and the counter only counts `Attended`.
- [ ] Representative, linked members and Admin/Board can see the tab; any other user gets `403`.
- [ ] Amounts are only shown to the representative and Admin/Board.
- [ ] "Ver inscripción" opens the matching registration detail and works for Board on other families.
- [ ] `?tab=campamentos` opens the tab directly.
- [ ] Data loaded in a single request, only when the tab is opened.
- [ ] §8 backend and frontend tests green; §9 documentation updated.

---

## 11. Open product questions

1. Should linked (non-representative) members see registration amounts? (Default: no.)
2. Should users be able to **declare attendance** from this tab (e.g. "We were there in 1998"), or does that stay in the map/timeline? (Default: read-only; left for a later story.)
