# User Story: Add Consent Checkbox, DNI Requirement, and UX Improvements to Family Creation

## ID
`feat-family-creation-validations`

## Summary
Enhance the family creation and member management flow with: (1) a mandatory consent checkbox when creating a family unit, (2) a warning about duplicate families, (3) mandatory DNI for adult members, (4) an informational hint about email for future platform access, and (5) a more sensible default date of birth for the auto-created representative member.

## User Story
**As a** user creating a family unit,
**I want to** confirm that I am the family representative with consent from all members, be required to provide my DNI if I'm an adult, and see helpful guidance about email usage,
**So that** the organization has proper consent records, identification for adults, and members understand how to enable future platform access.

## Context
- The family unit creation form (`FamilyUnitForm.vue`) currently only has a `name` field with no consent or legal acknowledgments.
- The family member form (`FamilyMemberForm.vue`) has `documentNumber` as optional for all members regardless of age.
- The auto-created representative member gets `DateOnly.MinValue` (0001-01-01) as default date of birth, which is confusing for users.
- Members added to a family can optionally provide an email, which triggers auto-linking to existing platform users (`FamilyUnitsService.cs:158-168`), but there is no guidance explaining this to the user.

## Detailed Requirements

### Frontend Changes

#### 1. Add consent checkbox and duplicate warning to family creation form
**File:** `frontend/src/components/family-units/FamilyUnitForm.vue`

Before the submit button, add:

- **Duplicate warning message** (always visible, informational):
  - Styled as an info/warning block (e.g., PrimeVue `Message` component with `severity="warn"`).
  - Text: `"Antes de crear una familia, asegurate de que otro miembro de tu familia no la haya creado ya. Si es asi, contacta con la directiva para que te asocien a ella."`

- **Mandatory consent checkbox**:
  - PrimeVue `Checkbox` bound to a new reactive variable `consentAccepted` (boolean, default `false`).
  - Label text: `"Actuare como representante de la familia y sere quien realice la inscripcion al campamento. Ademas, confirmo que tengo el consentimiento de todos los miembros de la familia para darles de alta en la plataforma."`
  - Validation: The "Crear" (submit) button must be disabled while `consentAccepted` is `false`.
  - Show a validation error message if the user attempts to submit without checking the box.
  - Add `data-testid="consent-checkbox"` for testing.

- **Updated submit button disabled condition**:
  ```
  :disabled="!isValid || !consentAccepted"
  ```

#### 2. Add email guidance hint to family member form
**File:** `frontend/src/components/family-units/FamilyMemberForm.vue`

- Below the `email` field, add a small helper text (PrimeVue `small` or `Message` with `severity="info"`):
  - Text: `"Si este miembro quiere registrarse en la plataforma para acceder al contenido de la web, indica aqui el email con el que se registrara posteriormente."`
  - This is informational only, no validation change to the email field itself.

#### 3. Make DNI mandatory for adult members (18+)
**File:** `frontend/src/components/family-units/FamilyMemberForm.vue`

- Compute whether the member is an adult based on `dateOfBirth`:
  ```typescript
  const isAdult = computed(() => {
    if (!dateOfBirth.value) return false
    const today = new Date()
    const birth = new Date(dateOfBirth.value)
    let age = today.getFullYear() - birth.getFullYear()
    const monthDiff = today.getMonth() - birth.getMonth()
    if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < birth.getDate())) {
      age--
    }
    return age >= 18
  })
  ```

- When `isAdult` is `true`:
  - The `documentNumber` field becomes **required** (add required asterisk to label).
  - Add validation: if `isAdult && !documentNumber`, show error `"El DNI/documento es obligatorio para mayores de edad"`.
  - The form should not submit if this validation fails.

- When `isAdult` is `false`:
  - The `documentNumber` field remains optional (current behavior).

- Add a helper text below the `documentNumber` field visible when `isAdult` is `true`:
  - Text: `"Obligatorio para mayores de edad."`

- Update the existing `validate()` function to include this new rule.

#### 4. Change default date of birth for auto-created representative
**File:** `frontend/src/components/family-units/FamilyMemberForm.vue`

- No frontend change needed for the default — this is set server-side when the representative member is auto-created.

### Backend Changes

#### 1. Change representative default date of birth
**File:** `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs`

- In the `CreateFamilyUnitAsync` method (around line 58), change the default `DateOfBirth` for the auto-created representative member:
  ```csharp
  // Before:
  DateOfBirth = DateOnly.MinValue
  // After:
  DateOfBirth = new DateOnly(1976, 1, 1)
  ```
  - 1976 is the founding year of ABUVI, making it a more recognizable placeholder that prompts the user to update it.

#### 2. Add backend validation for adult DNI (defense in depth)
**File:** `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsValidators.cs` (or wherever FluentValidation rules for family members are defined)

- Add a conditional validation rule for `CreateFamilyMemberRequest` and `UpdateFamilyMemberRequest`:
  ```csharp
  RuleFor(x => x.DocumentNumber)
      .NotEmpty()
      .When(x => IsAdult(x.DateOfBirth))
      .WithMessage("Document number is required for members aged 18 or older.");
  ```
  - Helper method:
  ```csharp
  private static bool IsAdult(DateOnly dateOfBirth)
  {
      var today = DateOnly.FromDateTime(DateTime.UtcNow);
      var age = today.Year - dateOfBirth.Year;
      if (dateOfBirth > today.AddYears(-age)) age--;
      return age >= 18;
  }
  ```

### Files to Modify

| File | Change |
|------|--------|
| `frontend/src/components/family-units/FamilyUnitForm.vue` | Add duplicate warning message, consent checkbox with validation, disable submit until consent given |
| `frontend/src/components/family-units/FamilyMemberForm.vue` | Add email helper text, computed `isAdult`, conditional DNI requirement with validation and helper text |
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs` | Change representative default DOB from `DateOnly.MinValue` to `new DateOnly(1976, 1, 1)` |
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsValidators.cs` | Add conditional validation: DNI required when member is 18+ |

### No new files needed
All changes are contained within existing components and services.

## UI Mockup (Family Unit Creation Form)

```
+------------------------------------------------+
|  Crear Familia                                  |
|                                                 |
|  Nombre de la familia *                         |
|  [____________________________]                 |
|                                                 |
|  +-- ! Aviso -----------------------------+    |
|  | Antes de crear una familia, asegurate   |    |
|  | de que otro miembro de tu familia no la |    |
|  | haya creado ya. Si es asi, contacta con |    |
|  | la directiva para que te asocien a ella.|    |
|  +-----------------------------------------+    |
|                                                 |
|  [x] Actuare como representante de la familia   |
|      y sere quien realice la inscripcion al     |
|      campamento. Ademas, confirmo que tengo el  |
|      consentimiento de todos los miembros de la |
|      familia para darles de alta en la          |
|      plataforma.                                |
|                                                 |
|  [Cancelar]              [Crear] (disabled if   |
|                           checkbox unchecked)    |
+------------------------------------------------+
```

## UI Mockup (Family Member Form - Adult)

```
+------------------------------------------------+
|  Nuevo Miembro                                  |
|                                                 |
|  Nombre *          Apellidos *                  |
|  [____________]    [____________]               |
|                                                 |
|  Fecha de nacimiento *                          |
|  [01/01/1990      ]                             |
|                                                 |
|  Parentesco *                                   |
|  [Parent v]                                     |
|                                                 |
|  DNI / Documento *                              |
|  [____________]                                 |
|  Obligatorio para mayores de edad.              |
|                                                 |
|  Email                                          |
|  [____________]                                 |
|  Si este miembro quiere registrarse en la       |
|  plataforma para acceder al contenido de la     |
|  web, indica aqui el email con el que se        |
|  registrara posteriormente.                     |
|                                                 |
|  Telefono                                       |
|  [____________]                                 |
|                                                 |
|  [Cancelar]                    [Guardar]        |
+------------------------------------------------+
```

## Acceptance Criteria

1. **Duplicate warning visible**: When opening the family creation form, a warning message reminds the user to check that another family member hasn't already created the family.
2. **Consent checkbox is mandatory**: The family creation form includes a consent checkbox that must be checked before the "Crear" button becomes enabled.
3. **Submit blocked without consent**: The user cannot submit the family creation form without checking the consent checkbox.
4. **Email hint displayed**: The family member form shows an informational message below the email field explaining its use for future platform registration.
5. **DNI required for adults**: When the member's date of birth indicates they are 18 or older, the `documentNumber` field becomes required with a visible indicator and validation error.
6. **DNI optional for minors**: When the member is under 18, the `documentNumber` field remains optional.
7. **Backend validates adult DNI**: The API rejects `CreateFamilyMember` and `UpdateFamilyMember` requests where the member is 18+ and `documentNumber` is null/empty, returning a 400 validation error.
8. **Representative default DOB is 1976-01-01**: When a family unit is created, the auto-generated representative member gets `1976-01-01` as default date of birth instead of `0001-01-01`.
9. **Responsive**: All new UI elements render correctly on mobile and desktop.
10. **Consistent styling**: Uses PrimeVue components and existing Tailwind patterns.

## Non-Functional Requirements

- **Accessibility**: Checkbox must have proper `id` and associated `<label>`. Helper texts should use `aria-describedby` linking to the relevant input.
- **Testing**:
  - Frontend: Unit tests for consent checkbox disabling submit, adult DNI validation toggle, and email hint visibility.
  - Backend: Unit test for the new FluentValidation rule (adult with no DNI rejected, minor without DNI accepted, adult with DNI accepted).
- **Data attributes**: Add `data-testid="consent-checkbox"`, `data-testid="dni-required-hint"`, `data-testid="email-hint"` for E2E testing.
- **Security**: Backend validation ensures DNI requirement cannot be bypassed by skipping frontend checks.

## Out of Scope
- Storing consent acceptance timestamp in the database (future enhancement if legal requires it).
- Custom legal page for family creation terms.
- Validating DNI format against Spanish NIF/NIE algorithms (current alphanumeric validation is sufficient).
- Changing the consent text dynamically per camp edition.

## Dependencies
- None. All required components, services, and validation infrastructure already exist.

## Estimated Complexity
**Low-Medium** — Changes span 4 files across frontend and backend. Frontend changes are mostly UI additions and computed validation. Backend changes are a one-line default change and a new conditional validation rule.
