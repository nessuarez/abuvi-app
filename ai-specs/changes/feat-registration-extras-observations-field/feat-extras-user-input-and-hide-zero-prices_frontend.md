# Frontend Implementation Plan: feat-extras-user-input-and-hide-zero-prices

## Overview

This plan implements two frontend features for the Camp Extras registration flow:

1. **User input text field for extras** - Allow registrants to provide free-text input when selecting extras that require additional information (e.g., size, dietary preference)
2. **Hide 0 EUR pricing** - Replace "0,00 EUR" display with "Incluido", remove the "Precios de referencia" table from the confirm step, and hide 0 EUR extras from the pricing breakdown

Architecture: Vue 3 Composition API, PrimeVue components, Tailwind CSS utility classes, TypeScript strict typing.

---

## Architecture Context

### Components/Files Involved

| File | Action | Purpose |
|------|--------|---------|
| `frontend/src/types/camp-edition.ts` | Modify | Add `requiresUserInput`, `userInputLabel` to types |
| `frontend/src/types/registration.ts` | Modify | Add `userInput` to wizard and request types |
| `frontend/src/components/registrations/RegistrationExtrasSelector.vue` | Modify | Add textarea for user input + hide 0 EUR pricing |
| `frontend/src/views/registrations/RegisterForCampPage.vue` | Modify | Remove "Precios de referencia" table, wire `userInput`, show user input in confirm summary |
| `frontend/src/components/registrations/RegistrationPricingBreakdown.vue` | Modify | Hide 0 EUR extras from breakdown |
| `frontend/src/components/camps/CampEditionExtrasFormDialog.vue` | Modify | Add admin toggle for `requiresUserInput` + label field |
| `frontend/src/components/camps/CampEditionExtrasList.vue` | Modify | Show indicator for extras requiring user input |

### State Management

- No new Pinia stores needed. All state is local to the registration wizard (`extrasSelections` array in `RegisterForCampPage.vue`).
- The `userInput` field flows through props/emits between `RegistrationExtrasSelector` and `RegisterForCampPage`.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a frontend-specific branch
- **Branch Naming**: `feature/feat-extras-user-input-and-hide-zero-prices-frontend`
- **Implementation Steps**:
  1. Ensure you're on the latest `dev` branch
  2. Pull latest changes: `git pull origin dev`
  3. Create new branch: `git checkout -b feature/feat-extras-user-input-and-hide-zero-prices-frontend`
  4. Verify branch creation: `git branch`
- **Notes**: The backend changes (migration, entity/DTO updates) from branch `feat/registration-extras-refinement` must be deployed or available for the API to accept the new fields. If not yet merged, coordinate with backend.

---

### Step 1: Update TypeScript Interfaces

#### Step 1a: `frontend/src/types/camp-edition.ts`

- **Action**: Add `requiresUserInput` and `userInputLabel` to extra-related interfaces
- **Implementation Steps**:
  1. Add to `CampEditionExtra` interface:

     ```typescript
     requiresUserInput: boolean
     userInputLabel?: string
     ```

  2. Add to `CreateCampExtraRequest` interface:

     ```typescript
     requiresUserInput?: boolean
     userInputLabel?: string
     ```

  3. Add to `UpdateCampExtraRequest` interface:

     ```typescript
     requiresUserInput?: boolean
     userInputLabel?: string
     ```

#### Step 1b: `frontend/src/types/registration.ts`

- **Action**: Add `userInput` to wizard selection and API request types
- **Implementation Steps**:
  1. Add to `WizardExtrasSelection` interface:

     ```typescript
     userInput?: string
     ```

  2. Add to `ExtraSelectionRequest` interface:

     ```typescript
     userInput?: string
     ```

  3. Add to `ExtraPricingDetail` interface:

     ```typescript
     userInput?: string
     ```

---

### Step 2: Update `RegistrationExtrasSelector.vue` - Hide 0 EUR + Add User Input

- **File**: `frontend/src/components/registrations/RegistrationExtrasSelector.vue`
- **Action**: Modify pricing label for 0 EUR extras and add textarea for extras requiring user input
- **Dependencies**: PrimeVue `Textarea` component import

#### Implementation Steps

1. **Import Textarea** from PrimeVue:

   ```typescript
   import Textarea from 'primevue/textarea'
   ```

2. **Modify `pricingLabel()` function** to return "Incluido" when price is 0:

   ```typescript
   const pricingLabel = (extra: CampEditionExtra): string => {
     if (extra.price === 0) return 'Incluido'
     const type = PRICING_TYPE_LABELS[extra.pricingType]
     const period = PRICING_PERIOD_LABELS[extra.pricingPeriod]
     return `${formatCurrency(extra.price)} ${type}${period}`
   }
   ```

3. **Add `getUserInput()` helper**:

   ```typescript
   const getUserInput = (extra: CampEditionExtra): string => {
     const selection = props.modelValue.find(s => s.campEditionExtraId === extra.id)
     return selection?.userInput ?? ''
   }
   ```

4. **Add `updateUserInput()` function**:

   ```typescript
   const updateUserInput = (extra: CampEditionExtra, value: string) => {
     const updated = props.modelValue.map(s =>
       s.campEditionExtraId === extra.id ? { ...s, userInput: value } : s
     )
     emit('update:modelValue', updated)
   }
   ```

5. **Preserve `userInput` in existing `updateQuantity()` function**: When updating quantity, carry over existing `userInput` value. When quantity becomes 0, clear `userInput`:

   ```typescript
   // In the updateQuantity function, when creating the new selection entry:
   const existingSelection = props.modelValue.find(s => s.campEditionExtraId === extra.id)
   // Include in the new entry:
   {
     campEditionExtraId: extra.id,
     name: extra.name,
     quantity: value,
     unitPrice: extra.price,
     userInput: value > 0 ? (existingSelection?.userInput ?? '') : undefined
   }
   ```

6. **Add textarea in template** below each extra card, inside the `v-for` loop, after the existing extra card content:

   ```html
   <div v-if="extra.requiresUserInput && getQuantity(extra) > 0" class="mt-2">
     <label class="mb-1 block text-xs font-medium text-gray-600">
       {{ extra.userInputLabel || 'Informacion adicional' }}
     </label>
     <Textarea
       :model-value="getUserInput(extra)"
       @update:model-value="updateUserInput(extra, $event)"
       :rows="2"
       :maxlength="500"
       class="w-full"
       :placeholder="extra.userInputLabel || 'Escribe aqui...'"
     />
   </div>
   ```

- **Implementation Notes**:
  - The textarea should appear inside the same card container as the extra, below the quantity selector
  - No minimum character validation enforced (spec says "at least 200 characters" refers to allowed capacity, not minimum required)
  - maxlength=500 enforced at the HTML level

---

### Step 3: Update `RegisterForCampPage.vue` - Remove Reference Prices + Wire User Input

- **File**: `frontend/src/views/registrations/RegisterForCampPage.vue`
- **Action**: Remove "Precios de referencia" table from confirm step, pass `userInput` to API, show user input in confirm summary

#### Implementation Steps

1. **Remove "Precios de referencia" section**: Delete the entire reference prices table (approximately lines 467-588). This is the blue-bordered box showing pricing by attendee category (Adulto/a, Nino/Nina, Bebe) with columns for complete, first week, second week, and weekend.

2. **Update confirm extras summary** to show user input text below each selected extra:

   ```html
   <!-- In the "Extras seleccionados" section -->
   <li v-for="sel in extrasSelections.filter(e => e.quantity > 0)" :key="sel.campEditionExtraId">
     <span>{{ sel.name }} x {{ sel.quantity }}</span>
     <p v-if="sel.userInput" class="mt-1 text-sm text-gray-500 italic">
       {{ sel.userInput }}
     </p>
   </li>
   ```

3. **Update `handleConfirm` mapping** to include `userInput` when sending extras to the API:

   ```typescript
   // In the extras mapping for the API request:
   .map((e) => ({
     campEditionExtraId: e.campEditionExtraId,
     quantity: e.quantity,
     userInput: e.userInput || null
   }))
   ```

- **Implementation Notes**:
  - The "Precios de referencia" removal is a significant template deletion. Verify that no other logic depends on it (the computed properties `allowsPartialAttendance`, `allowsWeekendVisit` may be used elsewhere in the confirm step - check before removing).
  - If `allowsPartialAttendance` and `allowsWeekendVisit` computed properties are ONLY used by the reference prices table, they can be removed too. If used elsewhere (e.g., member summary), keep them.

---

### Step 4: Update `RegistrationPricingBreakdown.vue` - Hide 0 EUR Extras

- **File**: `frontend/src/components/registrations/RegistrationPricingBreakdown.vue`
- **Action**: Filter out extras with `totalAmount === 0` from the breakdown table

#### Implementation Steps

1. **Add computed property** to filter paid extras:

   ```typescript
   const paidExtras = computed(() => props.pricing.extras.filter(e => e.totalAmount > 0))
   ```

2. **Replace `pricing.extras`** usage in template with `paidExtras`:
   - Update the extras table rows to iterate over `paidExtras` instead of `pricing.extras`
   - Update the `v-if` condition for showing the extras section: show only when `paidExtras.length > 0`
   - The extras subtotal should still use `pricing.extrasAmount` from the backend (since it already computes the correct total)

3. **Show `userInput`** in the extras table if present:

   ```html
   <!-- Below the extra name in each row -->
   <p v-if="extra.userInput" class="mt-0.5 text-xs text-gray-500 italic">{{ extra.userInput }}</p>
   ```

- **Implementation Notes**:
  - The empty state message "Sin extras seleccionados" should only show when `paidExtras.length === 0` (regardless of whether there are 0-priced extras)
  - The total still reflects backend-calculated amounts, so no pricing logic changes are needed

---

### Step 5: Update `CampEditionExtrasFormDialog.vue` - Admin Toggle for User Input

- **File**: `frontend/src/components/camps/CampEditionExtrasFormDialog.vue`
- **Action**: Add `requiresUserInput` toggle and `userInputLabel` text input to the form

#### Implementation Steps

1. **Add reactive state** for new fields:

   ```typescript
   const requiresUserInput = ref(false)
   const userInputLabel = ref('')
   ```

2. **Initialize from existing extra** in edit mode (in the watch or initialization logic that populates form fields from `props.extra`):

   ```typescript
   requiresUserInput.value = props.extra?.requiresUserInput ?? false
   userInputLabel.value = props.extra?.userInputLabel ?? ''
   ```

3. **Add form fields in template** after the `isRequired` toggle:

   ```html
   <!-- Requires user input toggle -->
   <div class="flex items-center justify-between">
     <label class="text-sm font-medium">Requires additional info</label>
     <ToggleSwitch v-model="requiresUserInput" />
   </div>

   <!-- User input label (conditional) -->
   <div v-if="requiresUserInput">
     <label class="mb-1 block text-sm font-medium">Input label</label>
     <InputText
       v-model="userInputLabel"
       maxlength="200"
       class="w-full"
       placeholder="e.g., Indica tu talla"
     />
     <small class="text-xs text-gray-400">Custom prompt shown to the user (max 200 chars)</small>
   </div>
   ```

4. **Include in `handleSave`** for both create and update paths:

   ```typescript
   // Add to the request object in both create and update:
   requiresUserInput: requiresUserInput.value,
   userInputLabel: requiresUserInput.value ? userInputLabel.value.trim() || null : null
   ```

5. **Reset on dialog close/open**: Clear `requiresUserInput` and `userInputLabel` when opening for create mode.

- **Implementation Notes**:
  - When `requiresUserInput` is toggled off, clear the `userInputLabel` field visually (hide it) but also send `null` to the API
  - No validation required for `userInputLabel` (it's optional)

---

### Step 6: Update `CampEditionExtrasList.vue` - Show Indicator

- **File**: `frontend/src/components/camps/CampEditionExtrasList.vue`
- **Action**: Add visual indicator for extras that require user input

#### Implementation Steps

1. **Add indicator in the Name column** template, after the extra name/description:

   ```html
   <!-- Inside the name column template, after existing content -->
   <span v-if="extra.requiresUserInput" class="ml-2 inline-flex items-center gap-1 text-xs text-blue-600">
     <i class="pi pi-pencil text-xs" />
     Requires info
   </span>
   ```

- **Implementation Notes**:
  - Use blue color to differentiate from the red "Required" badge
  - Keep it subtle - just an icon + short text next to the name
  - If `userInputLabel` is set, could optionally show it as a tooltip, but keep it simple for now

---

### Step 7: Write Vitest Unit Tests

- **Action**: Add/update unit tests for the modified components

#### Step 7a: `RegistrationExtrasSelector` Tests

- **File**: `frontend/src/__tests__/components/registrations/RegistrationExtrasSelector.test.ts` (or existing test file location - find via glob)
- **Test Cases**:
  1. **"Incluido" label for 0 EUR extras**: Render an extra with `price: 0`, assert that "Incluido" text is displayed instead of "0,00"
  2. **Textarea appears when requiresUserInput and quantity > 0**: Render an extra with `requiresUserInput: true`, set quantity > 0, assert textarea is visible
  3. **Textarea hidden when quantity is 0**: Same extra with `requiresUserInput: true` but quantity 0, assert textarea is not rendered
  4. **Textarea emits userInput update**: Type in textarea, verify emitted `modelValue` includes `userInput` value
  5. **Custom label displayed**: Extra with `userInputLabel: "Indica tu talla"`, verify that label text appears

#### Step 7b: `RegistrationPricingBreakdown` Tests

- **File**: `frontend/src/__tests__/components/registrations/RegistrationPricingBreakdown.test.ts` (or existing test file location)
- **Test Cases**:
  1. **0 EUR extras hidden**: Provide pricing with an extra that has `totalAmount: 0`, assert it's not rendered in the table
  2. **Paid extras shown**: Provide pricing with extras having `totalAmount > 0`, assert they appear
  3. **Mixed extras**: Provide both 0 EUR and paid extras, assert only paid ones appear
  4. **UserInput shown in breakdown**: Provide extra with `userInput: "Size L"`, assert text appears

---

### Step 8: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. **Review Changes**: Analyze all code changes made during implementation
  2. **Identify Documentation Files**: Determine which documentation files need updates:
     - API endpoint changes: Update `ai-specs/specs/api-spec.yml` if it exists (new request/response fields)
     - Type changes: No separate type documentation needed (types are self-documenting)
  3. **Update Documentation**: For each affected file:
     - Update content in English
     - Maintain consistency with existing documentation structure
  4. **Verify Documentation**: Confirm all changes are accurately reflected
  5. **Report Updates**: Document which files were updated

---

## Implementation Order

1. Step 0: Create Feature Branch
2. Step 1: Update TypeScript Interfaces (types/camp-edition.ts, types/registration.ts)
3. Step 2: Update RegistrationExtrasSelector.vue (hide 0 EUR + user input textarea)
4. Step 3: Update RegisterForCampPage.vue (remove reference prices + wire userInput)
5. Step 4: Update RegistrationPricingBreakdown.vue (hide 0 EUR extras)
6. Step 5: Update CampEditionExtrasFormDialog.vue (admin toggle)
7. Step 6: Update CampEditionExtrasList.vue (indicator)
8. Step 7: Write Vitest Unit Tests
9. Step 8: Update Technical Documentation

---

## Testing Checklist

- [ ] `RegistrationExtrasSelector`: "Incluido" shown for 0 EUR extras
- [ ] `RegistrationExtrasSelector`: Textarea appears for extras with `requiresUserInput: true` and quantity > 0
- [ ] `RegistrationExtrasSelector`: Textarea hidden when quantity is 0
- [ ] `RegistrationExtrasSelector`: `userInput` value emitted correctly
- [ ] `RegistrationPricingBreakdown`: 0 EUR extras hidden from breakdown
- [ ] `RegistrationPricingBreakdown`: Paid extras still displayed correctly
- [ ] `RegisterForCampPage`: "Precios de referencia" table no longer present in confirm step
- [ ] `RegisterForCampPage`: User input text shown in confirm summary for extras that have it
- [ ] `RegisterForCampPage`: `userInput` sent in API request on confirm
- [ ] `CampEditionExtrasFormDialog`: Toggle for `requiresUserInput` works
- [ ] `CampEditionExtrasFormDialog`: `userInputLabel` field appears/disappears with toggle
- [ ] `CampEditionExtrasList`: Indicator shown for extras with `requiresUserInput: true`
- [ ] All components render without console errors
- [ ] Responsive design works on mobile viewports

---

## Error Handling Patterns

- No new API error handling needed - existing composable patterns (loading/error/data refs) cover the extended fields
- `userInput` is optional - no frontend validation errors for empty input
- `maxlength="500"` on Textarea prevents exceeding backend limit at the HTML level
- `maxlength="200"` on admin `userInputLabel` InputText prevents exceeding backend limit

---

## UI/UX Considerations

- **Textarea placement**: Below the extra card content, inside the same card container, only visible when quantity > 0
- **"Incluido" label**: Uses the same text position as the price label, no additional styling needed
- **Confirm step cleanup**: Removing "Precios de referencia" simplifies the page significantly - users see only their actual selections
- **Admin indicator**: Subtle blue icon + text, doesn't interfere with existing red "Obligatorio" badge
- **Responsive**: Textarea uses `w-full` class, works on all viewports
- **Accessibility**: Labels associated with textareas, placeholder text provides guidance

---

## Dependencies

- **PrimeVue Components Used**: Textarea (new import in RegistrationExtrasSelector), ToggleSwitch, InputText, Dialog, InputNumber, Button, DataTable, Column, Tag, Select (all existing)
- **No new npm packages required**

---

## Notes

- All user-facing text in the registration flow is in Spanish (as per existing pattern)
- Admin-facing labels in the form dialog use English (as per existing pattern in the codebase)
- The `userInputLabel` field supports Spanish content set by the admin (e.g., "Indica tu talla")
- Backend must be deployed with the new fields before frontend changes go live
- The `UpdateCampExtraRequest` currently does NOT include `pricingType`/`pricingPeriod` (immutable after creation) - the new `requiresUserInput` and `userInputLabel` fields ARE mutable on update

---

## Next Steps After Implementation

1. Coordinate with backend deployment to ensure new fields are available
2. Manual QA of the full registration flow end-to-end
3. Verify admin create/edit extras workflow with new fields
4. Check that existing registrations with no `userInput` display correctly (null handling)

---

## Implementation Verification

- [ ] Code Quality: TypeScript strict, no `any`, `<script setup lang="ts">`
- [ ] Functionality: All components render correctly with new fields
- [ ] Testing: Vitest unit tests cover key scenarios
- [ ] Integration: Composables connect to backend API correctly with extended DTOs
- [ ] Documentation: Technical docs updated to reflect changes
- [ ] No `<style>` blocks added - Tailwind utility classes only
