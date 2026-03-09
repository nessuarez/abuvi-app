# Fix: Payment Instructions Duplicated After Registration Confirmation

## User Story

**As a** user completing a camp registration,
**I want** to see the payment instructions only once after confirming my registration,
**so that** the UI is clear, consistent, and not confusing.

---

## Problem Description

After confirming a registration on the `RegisterForCampPage`, the "Instrucciones de pago" (Payment Instructions) step panel is rendered **twice** on screen. Both instances are identical in content and behaviour, resulting in a duplicated display of:

- Bank transfer instructions (`BankTransferInstructions` component)
- Payment installment cards (`PaymentInstallmentCard` loop)
- Second installment due date message
- "Ir a mi inscripción" navigation button

This was introduced in commit `90382e9a` ("Feat/registrations payments (#152)") likely as a merge conflict artefact where both versions of the step were preserved instead of one being removed.

---

## Root Cause

**File:** [frontend/src/views/registrations/RegisterForCampPage.vue](frontend/src/views/registrations/RegisterForCampPage.vue)

There are **two consecutive, identical `<StepPanel>` blocks** for the payment step (lines 479–510 and 512–555), both with:
- `v-if="createdRegistrationId"`
- `:value="paymentStepValue"`
- Identical inner content

---

## Acceptance Criteria

- [ ] After confirming a registration, the "Instrucciones de pago" step panel is displayed **exactly once**.
- [ ] The `BankTransferInstructions`, `PaymentInstallmentCard` items, the second-installment info message, and the "Ir a mi inscripción" button all appear once and function correctly.
- [ ] No visual regression on any other step of the registration stepper.
- [ ] The fix is a pure template change — no logic, computed properties, or composables need modification.

---

## Files to Modify

| File | Change |
|------|--------|
| [frontend/src/views/registrations/RegisterForCampPage.vue](frontend/src/views/registrations/RegisterForCampPage.vue) | Remove the **second** duplicate `<StepPanel v-if="createdRegistrationId" :value="paymentStepValue">` block (lines 512–555) |

---

## Implementation Steps

1. Open `frontend/src/views/registrations/RegisterForCampPage.vue`.
2. Locate the two consecutive `<!-- Step: Payment Instructions -->` comment blocks starting at lines ~479 and ~512.
3. Delete the **second** `<StepPanel>` block (lines 512–555 inclusive), keeping only the first one (lines 479–510).
4. Verify the template still has a single `</StepPanels>` closing tag after the remaining `</StepPanel>`.
5. Run the frontend dev server and complete a registration end-to-end to confirm payment instructions appear only once.

---

## Non-Functional Requirements

- **No regression**: All existing stepper steps (member selection, accommodation, confirm, payment) must continue to function normally.
- **No backend changes required**: This is a pure frontend template fix.
- **No new tests required** for this minimal fix, but if a Playwright/Vitest E2E test exists for the registration flow, verify it still passes.

---

## Related Files (no changes needed)

- [frontend/src/components/payments/BankTransferInstructions.vue](frontend/src/components/payments/BankTransferInstructions.vue)
- [frontend/src/components/payments/PaymentInstallmentCard.vue](frontend/src/components/payments/PaymentInstallmentCard.vue)
- [frontend/src/views/registrations/RegistrationDetailPage.vue](frontend/src/views/registrations/RegistrationDetailPage.vue) — uses payment components correctly (no duplication)
