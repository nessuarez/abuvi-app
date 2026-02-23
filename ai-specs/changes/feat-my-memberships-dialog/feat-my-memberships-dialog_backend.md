# Backend Implementation Plan: feat-my-memberships-dialog — Integrate MembershipDialog into management views

---

## Overview

This feature wires the already-built `MembershipDialog.vue` component into `FamilyUnitPage.vue` and `ProfilePage.vue`. It is a **pure frontend task**. The backend is fully implemented as part of the `feat-membership-and-guests` feature (already merged).

**There is no backend work to do for this ticket.**

---

## Why No Backend Work Is Needed

| Area | Status | Notes |
|---|---|---|
| `Membership` entity and DB schema | ✅ Done | Migration `20260216234803_AddMembershipEntities.cs` |
| `MembershipFee` entity and DB schema | ✅ Done | Same migration |
| `GET /api/family-units/{fuid}/members/{mid}/membership` | ✅ Done | Returns active membership or 404 |
| `POST /api/family-units/{fuid}/members/{mid}/membership` | ✅ Done | Creates membership with start date |
| `DELETE /api/family-units/{fuid}/members/{mid}/membership` | ✅ Done | Deactivates membership (soft delete) |
| `GET /api/memberships/{id}/fees` | ✅ Done | Lists all fees |
| `GET /api/memberships/{id}/fees/current` | ✅ Done | Current year fee |
| `POST /api/memberships/{id}/fees/{fid}/pay` | ✅ Done | Marks fee as paid |
| `MembershipsService.cs` | ✅ Done | Full business logic |
| `MembershipsRepository.cs` | ✅ Done | EF Core queries |
| `CreateMembershipValidator.cs` | ✅ Done | FluentValidation |
| `PayFeeValidator.cs` | ✅ Done | FluentValidation |
| Unit tests | ✅ Done | (verify coverage before marking done) |

---

## Frontend Work Required

The frontend plan is in the enriched spec. See:

**→ [feat-my-memberships-dialog_enriched.md](./feat-my-memberships-dialog_enriched.md)**

Summary of frontend changes (3 files only):

| File | Change |
|---|---|
| `frontend/src/components/family-units/FamilyMemberList.vue` | Add `manageMembership` emit, `canManageMemberships` prop, action button |
| `frontend/src/views/FamilyUnitPage.vue` | Import and wire `MembershipDialog` |
| `frontend/src/views/ProfilePage.vue` | Import and wire `MembershipDialog`, reload on close |

---

## Implementation Order

**Step 0**: Use branch `feature/feat-my-memberships-dialog-frontend` (no backend branch needed).

For the full implementation plan, follow `/develop-frontend` with the enriched spec.

---

## Notes

- No migrations required.
- No NuGet package changes.
- No endpoint changes.
- No service or repository changes.
- The backend already returns 404 for members with no membership — the frontend handles this silently (existing behaviour documented in the spec).

---

**Feature ID**: `feat-my-memberships-dialog`
**Date**: 2026-02-22
**Backend Status**: ✅ Already complete — no action required
**Frontend Status**: Ready for implementation
