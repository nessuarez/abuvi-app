# Camp Meals ("Comensales") Report

## Overview

Board members currently have no way to tell the kitchen/catering provider how many people to
cook for on a given day of a camp edition, broken down by age (a baby eats differently from an
adult). Attendance is not static either: the registered headcount for a day needs manual
correction in both directions — a grandparent drops in for lunch on visiting day (add), or a
family member leaves the night before the last day and won't be there for breakfast (remove).

This feature adds a per-day, per-meal, per-age-range diner report for a `CampEdition`, computed
from existing registration data, with manual add/remove adjustments, exportable to Excel.

**Original request (verbatim, Spanish):**

> Necesitamos exportar en un excel un documento que detalle a partir de las personas inscritas el
> total por día y rango de edad de todo el campamento. Por comensales, claro. Por cada comida
> además necesitamos poder insertar personas extra: visitas, imprevistos, etc. o quitar personas
> porque a lo mejor no van a comer ese día.

**Status:** ❌ NOT STARTED — spec only, no code exists yet.

---

## Feature Description

For a selected `CampEdition`, the system computes, for every calendar day between
`StartDate` and `EndDate`, and for every meal of that day (breakfast, lunch, snack, dinner),
the number of diners broken down by age category (`Baby`, `Child`, `Adult` — the same
categories already used for camp pricing).

The baseline count for a given day+meal+age-category comes from registered attendees
(`RegistrationMember`) whose attendance period (`Complete`, `FirstWeek`, `SecondWeek`,
`WeekendVisit`) covers that day, excluding cancelled registrations. On top of that baseline,
a board member can:

- **Add extra diners**: a manual headcount entry for a specific day/meal/age-category, for
  people who are not registered attendees (visitors, unforeseen guests, staff, etc.).
- **Exclude a registered attendee from one meal**: mark a specific `RegistrationMember` as
  not eating a specific meal on a specific day (e.g. they leave the camp the evening before
  the last day, so they should not be counted for that day's breakfast).

The final report — baseline + extras − exclusions, per day/meal/age-category, with row and
column totals — can be viewed on screen and exported as an `.xlsx` workbook for the kitchen.

### Why every day has all 4 meals, with no special-casing for arrival/departure days

A real camp's first and last days often don't have every meal (e.g. campers arrive in time
for dinner only, or leave right after breakfast). Rather than modeling per-period meal
templates (which would require product decisions about every combination of `AttendancePeriod`
× first/last day), this spec deliberately keeps the baseline calculation simple — every
registered day gets all 4 meals — and relies entirely on the **meal exclusion** mechanism the
user already asked for to correct the edges. This keeps the feature scoped to what was
requested instead of guessing at camp-specific scheduling rules; see Open Questions below if
the edge case turns out to be common enough to warrant automation later.

---

## Domain Model Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Meal types | Fixed enum: `Breakfast` (Desayuno), `Lunch` (Comida), `Snack` (Merienda), `Dinner` (Cena) — same 4 for every day | Matches typical Spanish camp schedules; avoids building a configurable meal calendar for a first version. Confirm with product (see Open Questions). |
| Age ranges | Reuse the existing `AgeCategory` enum (`Baby`/`Child`/`Adult`) already computed and snapshotted on `RegistrationMember` at registration time, using the edition's own age-range configuration (`CampEdition.UseCustomAgeRanges` etc., resolved by `RegistrationPricingService.GetAgeCategoryAsync`) | No new age-bucket concept to design, test, or explain; consistent with how the same people are already priced. |
| Attendance source | `RegistrationMember.AttendancePeriod` + `RegistrationPricingService.GetPeriodDays` (already used for pricing) resolves which calendar days a member is present | This is the one existing primitive that already answers "is this person here on day X" — reused, not reinvented. |
| Manual additions | New entity `CampEditionExtraDiner` (day, meal, age category, count, notes) | Modeled the same way `CampEditionExtra` / `RegistrationExtra` already model "extra things attached to an edition." |
| Manual removals | New entity `CampEditionMealExclusion` (day, meal, `RegistrationMemberId`, reason) | Kept as its own entity rather than folding into `CampEditionExtraDiner` with nullable fields for both directions — the two record shapes and validation rules are different enough (age-category + count vs. member-id) that one shared nullable-heavy table would be harder to validate and test than two small ones (SRP). |
| Access | Board/Admin only, no member-facing surface | Catering headcounts are operational data for the board and kitchen, not something individual families need to see or edit. |

---

## Open Questions for Product (flag before/while implementing)

1. **Meal names/count**: is it always these 4 (Desayuno, Comida, Merienda, Cena), or does it vary
   by camp? If it varies, this needs a configurable meal list per edition instead of a fixed enum.
2. **Age ranges**: is reusing the existing pricing `AgeCategory` (Baby/Child/Adult) granular
   enough for the kitchen, or does catering need a different breakdown (e.g. distinguishing
   toddlers from babies)? Dietary/allergy data (already stored encrypted on `FamilyMember`) is
   explicitly **out of scope** for this report unless product asks for it separately.
3. **Excel layout**: one sheet with all days, or one sheet per day/week? This spec assumes a
   single "Resumen" sheet with days as row groups (see backend spec).
4. **Arrival/departure day meals**: confirmed to rely purely on manual exclusions per the
   reasoning above — flag if this turns out to be needed for every camper on every edition
   (in which case automating "no breakfast on arrival day / no dinner on departure day" would
   be worth a follow-up ticket).

---

## Related Specs

- Reuses attendance/pricing logic from `RegistrationPricingService` (`src/Abuvi.API/Features/Registrations/`).
- Follows the same feature-slice + DTO/entity pattern as
  [feat-camp-edition-extras](../feat-camp-edition-extras/camp-edition-extras.md).

## Detail Specs

- Backend: [feat-camp-meals-report_backend.md](./feat-camp-meals-report_backend.md)
- Frontend: [feat-camp-meals-report_frontend.md](./feat-camp-meals-report_frontend.md)

---

## Document Control

- **Version**: 1.0
- **Created**: 2026-08-26
- **Status**: ❌ Not Started — spec only
