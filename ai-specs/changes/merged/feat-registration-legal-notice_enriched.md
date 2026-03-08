# User Story: Add Legal Notice with Terms Acceptance to Camp Registration

## ID

`feat-registration-legal-notice`

## Summary

Add a mandatory legal notice with a checkbox to the camp registration wizard so that users must explicitly accept the camp's terms and conditions before confirming their enrollment.

## User Story

**As a** family representative registering for a camp,
**I want to** see and accept the camp's terms and conditions during enrollment,
**So that** I am informed of the legal conditions and the organization has a record of my consent.

## Context

- The camp registration wizard (`RegisterForCampPage.vue`) currently has a multi-step flow: Participantes > Extras > Alojamiento (optional) > Confirmar.
- The user registration form (`RegisterForm.vue`) already has a terms acceptance checkbox linked to `/legal/privacy`, but this covers account creation only — not camp enrollment.
- Camp enrollment is a separate legal action (financial commitment, liability, health/safety rules) that requires its own terms acceptance.
- Legal pages already exist at `/legal/*` (privacy, notice, bylaws, transparency) using `LegalPageLayout.vue`.

## Detailed Requirements

### Frontend Changes

#### 1. Add legal notice and checkbox to the confirmation step

**File:** `frontend/src/views/registrations/RegisterForCampPage.vue`

- Add a legal notice block in the **Confirm step** (`StepPanel :value="confirmStepValue"`), between the review information and the action buttons.
- The block should include:
  - A bordered container with a warning/info style (similar to the price reference block).
  - A brief legal text explaining what the user is accepting. Example:

    ```
    Al confirmar esta inscripción, declaro que:
    - He leído y acepto las normas del campamento.
    - Autorizo el tratamiento de los datos personales según la política de privacidad.
    - Acepto las condiciones de pago y cancelación.
    ```

  - Links to the relevant legal pages: `/legal/privacy` (Política de Privacidad) and `/legal/notice` (Aviso Legal).
  - A PrimeVue `Checkbox` component bound to a new reactive variable `acceptTerms` (boolean, default `false`).
  - Label text: `"He leído y acepto los términos y condiciones del campamento"`
  - A validation error message (red text below checkbox) shown if the user tries to confirm without checking the box.

#### 2. Disable confirmation until terms are accepted

**File:** `frontend/src/views/registrations/RegisterForCampPage.vue`

- The "Confirmar inscripción" button should be disabled when `acceptTerms` is `false`.
- Update the `:disabled` condition on the confirm button:

  ```
  :disabled="selectedMembers.length === 0 || !acceptTerms"
  ```

#### 3. New reactive state

**File:** `frontend/src/views/registrations/RegisterForCampPage.vue`

- Add: `const acceptTerms = ref<boolean>(false)`
- No changes needed to types or composables — this is a frontend-only validation gate (the backend does not need to store this consent separately since it already has the user's account-level consent).

### Backend Changes

#### Option A: Frontend-only (Recommended for MVP)

No backend changes. The checkbox is a UI gate that prevents submission without acceptance. The act of completing the registration implies acceptance.

#### Option B: Backend tracking (Future enhancement)

If the organization later requires explicit consent records per registration:

- Add `acceptedTermsAt: DateTime?` field to the `Registration` entity.
- Add `acceptedTerms: boolean` to `CreateRegistrationRequest`.
- Add FluentValidation rule: `RuleFor(x => x.AcceptedTerms).Equal(true)`.
- Store the timestamp of acceptance.
- **This is NOT required for the initial implementation.**

### Files to Modify

| File | Change |
|------|--------|
| `frontend/src/views/registrations/RegisterForCampPage.vue` | Add `acceptTerms` ref, legal notice block with checkbox in confirm step, update button disabled condition |

### No new files needed

This change is contained within the existing registration wizard page.

## UI Mockup (Confirm Step)

```
┌──────────────────────────────────────────────┐
│  Revisa y confirma                           │
│  Comprueba los datos antes de confirmar...   │
│                                              │
│  ┌─ Participantes seleccionados ───────────┐ │
│  │  ...                                    │ │
│  └─────────────────────────────────────────┘ │
│                                              │
│  ┌─ Precios de referencia ─────────────────┐ │
│  │  ...                                    │ │
│  └─────────────────────────────────────────┘ │
│                                              │
│  ┌─ Extras seleccionados ──────────────────┐ │
│  │  ...                                    │ │
│  └─────────────────────────────────────────┘ │
│                                              │
│  ┌─ Notas adicionales ─────────────────────┐ │
│  │  [textarea]                             │ │
│  └─────────────────────────────────────────┘ │
│                                              │
│  ┌─ ⚠ Aviso Legal ────────────────────────┐ │
│  │                                         │ │
│  │  Al confirmar esta inscripción,         │ │
│  │  declaro que:                           │ │
│  │                                         │ │
│  │  • He leído y acepto las normas del     │ │
│  │    campamento.                          │ │
│  │  • Autorizo el tratamiento de datos     │ │
│  │    personales según la                  │ │
│  │    [política de privacidad].            │ │
│  │  • Acepto las condiciones de pago       │ │
│  │    y cancelación establecidas en el     │ │
│  │    [aviso legal].                       │ │
│  │                                         │ │
│  │  ☐ He leído y acepto los términos y     │ │
│  │    condiciones del campamento           │ │
│  │                                         │ │
│  └─────────────────────────────────────────┘ │
│                                              │
│  [← Atrás]              [Confirmar ✓]       │
│                          (disabled if ☐)     │
└──────────────────────────────────────────────┘
```

## Acceptance Criteria

1. **Legal notice is visible**: In the confirmation step, a clearly styled block displays the legal terms summary with links to `/legal/privacy` and `/legal/notice`.
2. **Checkbox is mandatory**: A checkbox with label "He leído y acepto los términos y condiciones del campamento" must be checked before the user can confirm.
3. **Button is disabled**: The "Confirmar inscripción" button is disabled when the checkbox is unchecked.
4. **Links work**: The privacy policy and legal notice links open in a new tab (`target="_blank"`).
5. **No backend changes**: This is a frontend-only change for the initial implementation.
6. **Responsive**: The legal notice block renders correctly on mobile and desktop.
7. **Consistent styling**: Uses the same design patterns as existing info blocks (borders, rounded corners, Tailwind utilities).

## Non-Functional Requirements

- **Accessibility**: The checkbox must have a proper `id` and associated `<label>` for screen readers. Links should have `rel="noopener noreferrer"` when using `target="_blank"`.
- **Testing**: Add a unit test verifying the confirm button is disabled when `acceptTerms` is `false` and enabled when `true`.
- **Data attribute**: Add `data-testid="accept-terms-checkbox"` for E2E testing.

## Out of Scope

- Creating a new legal page specific to camp registration terms (can use existing `/legal/notice` and `/legal/privacy`).
- Backend consent tracking (future enhancement).
- Customizable terms per camp edition (future enhancement).
- PDF download of terms and conditions.

## Dependencies

- None. All required components and pages already exist.

## Estimated Complexity

**Low** — Single file change, UI-only, no API or data model changes.
