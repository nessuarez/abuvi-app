# Feature Spec: Camp Extras — User Input Text Field & Hide 0€ Prices

## Status: ENRICHED — Ready for implementation planning

## Origin

User request: "We need to add an extra input text for Camp Extras. In some cases we will ask people to fill in that part with extra information. So, people can write at least 200 characters. Also, avoid showing 0€ in the registration form when an extra has no cost. The reference prices in the registration process confuse users in the final screen, where they expect to see the actual cost summary."

---

## Problem Statement

Two issues in the current Camp Extras registration flow:

1. **Missing user input for extras**: Some extras require the family to provide additional information (e.g., preferred size, dietary choice, specific notes). Currently there is no way for the user to enter free-text input when selecting an extra.

2. **Confusing 0€ pricing display**: When an extra has `price: 0`, the registration form shows "0,00 € por persona" (or per family), which confuses users. In the final confirmation screen, the "Precios de referencia" table adds noise and users expect to see only the actual cost summary of their selections.

---

## Scope

### In scope (this feature)

1. **New `requiresUserInput` flag on `CampEditionExtra`** — admin can mark an extra as requiring free-text input from the registrant
2. **New `userInputLabel` field on `CampEditionExtra`** — optional custom label/prompt for the input field (e.g., "Indica tu talla", "Especifica tu opción")
3. **New `userInput` field on `RegistrationExtra`** — stores the text provided by the family during registration (up to 500 chars)
4. **Frontend text input** in `RegistrationExtrasSelector.vue` — shown when an extra has `requiresUserInput: true` and `quantity > 0`
5. **Hide 0€ pricing label** in `RegistrationExtrasSelector.vue` — when `price === 0`, show "Incluido" instead of "0,00 €"
6. **Remove "Precios de referencia" table** from the confirm step in `RegisterForCampPage.vue` — the confirmation screen should only show the user's actual selections, not reference prices
7. **Hide 0€ extras** in `RegistrationPricingBreakdown.vue` — when an extra has `totalAmount === 0`, omit it from the pricing breakdown table (both in registration detail and admin views)

### Out of scope

- Structured input (dropdowns, checkboxes) for extras — only free-text for now
- Making user input mandatory at the API level (frontend-only validation based on `requiresUserInput`)
- Changes to the admin `CampEditionExtrasList.vue` pricing display (admin should still see 0€ for clarity)

---

## Data Model

### `CampEditionExtra` — Add fields

| New Field | Type | Constraints | Description |
| --------- | ---- | ----------- | ----------- |
| `RequiresUserInput` | `bool` | NOT NULL, default `false` | Whether this extra needs free-text from the registrant |
| `UserInputLabel` | `string?` | NULL, max 200 chars | Custom prompt/label for the text input (e.g. "Indica tu talla") |

### `RegistrationExtra` — Add field

| New Field | Type | Constraints | Description |
| --------- | ---- | ----------- | ----------- |
| `UserInput` | `string?` | NULL, max 500 chars | Free-text input provided by the family during registration |

---

## Key Business Rules

1. When `requiresUserInput` is `true` and the user selects the extra (quantity > 0), a text area appears below the extra card
2. The text area allows at least 200 characters (max 500 to be generous while preventing abuse)
3. The input label defaults to "Información adicional" if `userInputLabel` is null/empty
4. When `price === 0`, the pricing label in the extras selector shows **"Incluido"** instead of "0,00 € por persona"
5. The "Precios de referencia" table in the confirmation step is removed entirely — it causes confusion when users expect a cost summary
6. In `RegistrationPricingBreakdown`, extras with `totalAmount === 0` are hidden from the breakdown table to avoid displaying "0,00 €" rows
7. The `userInput` value is sent to the backend when setting extras on a registration

---

## Backend Implementation

### 1. Migration — Add new columns

**New migration file**: `src/Abuvi.API/Migrations/XXXXXXXX_AddExtraUserInputFields.cs`

Add columns to `camp_edition_extras`:

- `requires_user_input` (boolean, NOT NULL, default false)
- `user_input_label` (varchar(200), NULL)

Add column to `registration_extras`:

- `user_input` (varchar(500), NULL)

### 2. `CampsModels.cs` — Update entity and DTOs

**File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`

- `CampEditionExtra` entity: add `RequiresUserInput` (bool) and `UserInputLabel` (string?)
- `CampEditionExtraResponse` record: add `RequiresUserInput` (bool) and `UserInputLabel` (string?)
- `CreateCampEditionExtraRequest` record: add `RequiresUserInput` (bool, default false) and `UserInputLabel` (string?)
- `UpdateCampEditionExtraRequest` record: add `RequiresUserInput` (bool) and `UserInputLabel` (string?)

### 3. `RegistrationsModels.cs` — Update entity and DTOs

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`

- `RegistrationExtra` entity: add `UserInput` (string?)
- `ExtraPricingDetail` record: add `UserInput` (string?)
- `ExtraSelectionRequest` (used in `SetExtrasRequest`): add `UserInput` (string?)

### 4. Camps endpoints — Pass new fields through

**File**: `src/Abuvi.API/Features/Camps/CampsEndpoints.cs` (or wherever extras CRUD handlers live)

- Map `RequiresUserInput` and `UserInputLabel` in create/update/response mappings

### 5. Registration extras handler — Persist `userInput`

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs` (or set-extras handler)

- When creating `RegistrationExtra` records, map `UserInput` from the request

### 6. EF Configuration — Update if needed

**File**: `src/Abuvi.API/Data/Configurations/CampEditionExtraConfiguration.cs`

- Configure `UserInputLabel` max length (200)
- Configure `RegistrationExtra.UserInput` max length (500) in corresponding configuration

---

## Frontend Implementation

### 1. Types — `frontend/src/types/camp-edition.ts`

- `CampEditionExtra`: add `requiresUserInput: boolean` and `userInputLabel?: string`
- `CreateCampExtraRequest`: add `requiresUserInput?: boolean` and `userInputLabel?: string`
- `UpdateCampExtraRequest`: add `requiresUserInput?: boolean` and `userInputLabel?: string`

### 2. Types — `frontend/src/types/registration.ts`

- `WizardExtrasSelection`: add `userInput?: string`
- `ExtraSelectionRequest` (sent to API): add `userInput?: string`

### 3. `RegistrationExtrasSelector.vue` — User input + hide 0€

**File**: `frontend/src/components/registrations/RegistrationExtrasSelector.vue`

Changes to `pricingLabel()`:

```typescript
const pricingLabel = (extra: CampEditionExtra): string => {
  if (extra.price === 0) return 'Incluido'
  const type = PRICING_TYPE_LABELS[extra.pricingType]
  const period = PRICING_PERIOD_LABELS[extra.pricingPeriod]
  return `${formatCurrency(extra.price)} ${type}${period}`
}
```

Add `userInput` handling in `updateQuantity` and add a new `updateUserInput` function.

In the template, below each extra card (inside the `v-for` loop), add a `Textarea` when `extra.requiresUserInput && getQuantity(extra) > 0`:

```html
<div v-if="extra.requiresUserInput && getQuantity(extra) > 0" class="mt-2">
  <label class="mb-1 block text-xs font-medium text-gray-600">
    {{ extra.userInputLabel || 'Información adicional' }}
  </label>
  <Textarea
    :model-value="getUserInput(extra)"
    @update:model-value="updateUserInput(extra, $event)"
    :rows="2"
    :maxlength="500"
    class="w-full"
    :placeholder="extra.userInputLabel || 'Escribe aquí...'"
  />
  <small class="text-xs text-gray-400">Mín. 200 caracteres recomendados</small>
</div>
```

Props `modelValue` type (`WizardExtrasSelection[]`) already includes `userInput` after the type update.

### 4. `RegisterForCampPage.vue` — Remove reference prices table + pass userInput

**File**: `frontend/src/views/registrations/RegisterForCampPage.vue`

- **Remove** the entire "Precios de referencia" section (lines ~467–586) from the confirm step
- In `handleConfirm`, include `userInput` when mapping `extrasSelections` to the API request:

  ```typescript
  .map((e) => ({
    campEditionExtraId: e.campEditionExtraId,
    quantity: e.quantity,
    userInput: e.userInput || null
  }))
  ```

- In the "Extras seleccionados" summary in the confirm step, show the user input text below each extra if provided

### 5. `RegistrationPricingBreakdown.vue` — Hide 0€ extras

**File**: `frontend/src/components/registrations/RegistrationPricingBreakdown.vue`

- Filter out extras with `totalAmount === 0` from the extras table:

  ```typescript
  const paidExtras = computed(() => props.pricing.extras.filter(e => e.totalAmount > 0))
  ```

- Use `paidExtras` in the template instead of `pricing.extras`
- Update the `v-if` condition: show the extras section only when `paidExtras.length > 0`

### 6. `CampEditionExtrasFormDialog.vue` — Admin toggle for user input

**File**: `frontend/src/components/camps/CampEditionExtrasFormDialog.vue`

- Add `requiresUserInput` toggle switch (ToggleSwitch)
- Add `userInputLabel` text input (InputText, max 200 chars), shown only when `requiresUserInput` is true
- Wire both fields through `handleSave` for create and update

### 7. `CampEditionExtrasList.vue` — Show indicator

**File**: `frontend/src/components/camps/CampEditionExtrasList.vue`

- Show a small icon or badge (e.g., `pi pi-pencil` or text "Requiere info") next to extras that have `requiresUserInput: true`

---

## API Endpoints Summary

No new endpoints required. Existing endpoints are extended:

| Method | URL | Changes |
| ------ | --- | ------- |
| GET | `/camps/editions/{editionId}/extras` | Response includes `requiresUserInput`, `userInputLabel` |
| POST | `/camps/editions/{editionId}/extras` | Request accepts `requiresUserInput`, `userInputLabel` |
| PUT | `/camps/editions/extras/{extraId}` | Request accepts `requiresUserInput`, `userInputLabel` |
| PUT | `/registrations/{id}/extras` | Request accepts `userInput` per extra |
| GET | `/registrations/{id}` | Response `ExtraPricingDetail` includes `userInput` |

---

## Acceptance Criteria

1. **Admin** can create/edit an extra with `requiresUserInput: true` and an optional `userInputLabel`
2. **Registration form**: when a user selects an extra that requires input (quantity > 0), a text area appears with the configured label
3. **Registration form**: extras with price 0€ show "Incluido" instead of "0,00 € por persona/familia"
4. **Confirmation step**: the "Precios de referencia" table is removed; only actual selections are shown
5. **Confirmation step**: selected extras with user input show the provided text in the summary
6. **Pricing breakdown** (registration detail page): extras with totalAmount 0 are hidden
7. **Backend** persists the `userInput` text in `RegistrationExtra` and returns it in responses
8. **Admin views**: extras list shows an indicator for extras that require user input

---

## Testing Requirements

### Unit tests to add/update

1. `RegistrationExtrasSelector.test.ts` — test that "Incluido" is shown when price is 0 instead of "0,00 €"
2. `RegistrationExtrasSelector.test.ts` — test that text area appears when `requiresUserInput: true` and quantity > 0
3. `RegistrationExtrasSelector.test.ts` — test that text area is hidden when quantity is 0
4. `RegistrationPricingBreakdown.test.ts` — test that 0€ extras are hidden from the breakdown

---

## Implementation Order

1. **Backend**: Migration + entity/DTO changes
2. **Backend**: Update create/update/response mappings for Camps extras endpoints
3. **Backend**: Update registration set-extras handler to persist `userInput`
4. **Frontend types**: Update TypeScript interfaces
5. **Frontend**: `RegistrationExtrasSelector.vue` — hide 0€ + add user input textarea
6. **Frontend**: `RegisterForCampPage.vue` — remove reference prices table + wire userInput
7. **Frontend**: `RegistrationPricingBreakdown.vue` — hide 0€ extras
8. **Frontend**: `CampEditionExtrasFormDialog.vue` — admin toggle for requiresUserInput
9. **Frontend**: `CampEditionExtrasList.vue` — show indicator
10. **Tests**: Update/add unit tests

---

## Document Control

| Field | Value |
| ----- | ----- |
| Feature ID | feat-extras-user-input-and-hide-zero-prices |
| Created | 2026-03-05 |
| Status | Enriched |
