# Frontend Implementation Plan: feat-registration-extra-fields2 — Registration Guardian & Preference Fields

## Overview

This feature extends the registration wizard and detail pages with guardian fields for minor members and two family-level preference fields (`SpecialNeeds`, `CampatesPreference`). Guardian info is collected per-member in the member selector, and the two preference fields are added to the existing wizard flow. The detail page and pricing breakdown are updated to display the new data.

**Architecture**: Vue 3 Composition API with `<script setup lang="ts">`, PrimeVue components, Tailwind CSS utility classes, composable-based API communication.

**Backend dependency**: The backend plan (`feat-registration-extra-fields2_backend.md`) must be implemented first.

### Scope Reduction

The following fields from the original Google Forms spec were **extracted to separate tickets**:

| Field | Extracted To | Reason |
| ----- | ------------ | ------ |
| `AccommodationPreferences` | `feat-registration-accommodations` | Needs structured model for family placement logic |
| `VegetarianCount` | `feat-camp-edition-extras-registration` | Generic extras system (existing `CampEditionExtra` entity) |
| `NeedsTruck` | `feat-camp-edition-extras-registration` | Generic extras system (existing `CampEditionExtra` entity) |
| `Activities` | `feat-registration-activities` | Needs structured model with conditions per edition |

**This ticket only implements**: `SpecialNeeds`, `CampatesPreference`, `GuardianName`, `GuardianDocumentNumber`.

---

## Architecture Context

### Components/Composables Involved

| File | Change Type |
| ---- | ----------- |
| `frontend/src/types/registration.ts` | Extend interfaces with new fields |
| `frontend/src/composables/useRegistrations.ts` | No changes needed (already sends `CreateRegistrationRequest` to API) |
| `frontend/src/views/registrations/RegisterForCampPage.vue` | Add preference fields to wizard, update `handleConfirm` |
| `frontend/src/views/registrations/RegistrationDetailPage.vue` | Add new read-only sections for preference fields |
| `frontend/src/components/registrations/RegistrationPricingBreakdown.vue` | Show guardian info inline for minor members |
| `frontend/src/components/registrations/RegistrationMemberSelector.vue` | Add guardian input fields for minor members |

### State Management

- **Local state only** — no Pinia store needed. All new state lives in the wizard's `<script setup>` as `ref()` values.
- The existing `useRegistrations` composable already handles `CreateRegistrationRequest` — it just needs the TypeScript interface updated to accept the new fields.

### Routing

- No new routes. Existing routes `/registrations/new/:editionId` and `/registrations/:id` remain unchanged.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a frontend-specific feature branch
- **Branch Naming**: `feature/feat-registration-extra-fields2-frontend`
- **Implementation Steps**:
  1. Ensure you're on the latest `main` branch
  2. Pull latest changes: `git pull origin main`
  3. Create new branch: `git checkout -b feature/feat-registration-extra-fields2-frontend`
  4. Verify branch creation: `git branch`
- **Notes**: This must be the FIRST step before any code changes.

---

### Step 1: Extend TypeScript Types in `registration.ts`

- **File**: `frontend/src/types/registration.ts`
- **Action**: Extend existing interfaces with new fields

#### 1a. Extend `MemberAttendanceRequest` with guardian fields

**Current**:

```typescript
export interface MemberAttendanceRequest {
  memberId: string
  attendancePeriod: AttendancePeriod
  visitStartDate?: string | null
  visitEndDate?: string | null
}
```

**Replace with**:

```typescript
export interface MemberAttendanceRequest {
  memberId: string
  attendancePeriod: AttendancePeriod
  visitStartDate?: string | null
  visitEndDate?: string | null
  guardianName?: string | null
  guardianDocumentNumber?: string | null
}
```

#### 1b. Extend `WizardMemberSelection` with guardian fields

**Current**:

```typescript
export interface WizardMemberSelection {
  memberId: string
  attendancePeriod: AttendancePeriod
  visitStartDate: string | null
  visitEndDate: string | null
}
```

**Replace with**:

```typescript
export interface WizardMemberSelection {
  memberId: string
  attendancePeriod: AttendancePeriod
  visitStartDate: string | null
  visitEndDate: string | null
  guardianName: string | null
  guardianDocumentNumber: string | null
}
```

#### 1c. Extend `MemberPricingDetail` with guardian fields

Add two new fields after `individualAmount`:

```typescript
export interface MemberPricingDetail {
  familyMemberId: string
  fullName: string
  ageAtCamp: number
  ageCategory: AgeCategory
  attendancePeriod: AttendancePeriod
  attendanceDays: number
  visitStartDate: string | null
  visitEndDate: string | null
  individualAmount: number
  guardianName: string | null       // NEW
  guardianDocumentNumber: string | null  // NEW
}
```

#### 1d. Extend `RegistrationResponse` with 2 new fields

Add after `updatedAt`:

```typescript
export interface RegistrationResponse {
  id: string
  familyUnit: RegistrationFamilyUnitSummary
  campEdition: RegistrationCampEditionSummary
  status: RegistrationStatus
  notes: string | null
  pricing: PricingBreakdown
  payments: PaymentSummary[]
  amountPaid: number
  amountRemaining: number
  createdAt: string
  updatedAt: string
  // NEW — preference fields
  specialNeeds: string | null
  campatesPreference: string | null
}
```

#### 1e. Extend `CreateRegistrationRequest` with 2 new fields

**Current**:

```typescript
export interface CreateRegistrationRequest {
  campEditionId: string
  familyUnitId: string
  members: MemberAttendanceRequest[]
  notes?: string | null
}
```

**Replace with**:

```typescript
export interface CreateRegistrationRequest {
  campEditionId: string
  familyUnitId: string
  members: MemberAttendanceRequest[]
  notes?: string | null
  specialNeeds: string | null
  campatesPreference: string | null
}
```

- **Implementation Notes**: No backward compatibility needed — application is not in production. All existing callers must be updated.

---

### Step 2: Update `RegistrationMemberSelector.vue` — Add Guardian Fields for Minors

- **File**: `frontend/src/components/registrations/RegistrationMemberSelector.vue`
- **Action**: Add guardian name and document number inputs for members who are minors (determined by `dateOfBirth`)

#### 2a. Add a helper to detect if a member is a minor

Add in the `<script setup>` section:

```typescript
const isMinor = (member: FamilyMemberResponse): boolean => {
  const dob = new Date(member.dateOfBirth)
  const today = new Date()
  let age = today.getFullYear() - dob.getFullYear()
  const monthDiff = today.getMonth() - dob.getMonth()
  if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < dob.getDate())) {
    age--
  }
  return age < 18
}
```

- **Implementation Notes**: This is a local approximation. The exact age at camp is calculated by the backend. We use current age as a UI hint to show/hide guardian fields.

#### 2b. Add `updateGuardianField` handler

Add after `updateVisitDate` function:

```typescript
const updateGuardianField = (
  memberId: string,
  field: 'guardianName' | 'guardianDocumentNumber',
  value: string
) => {
  emit(
    'update:modelValue',
    props.modelValue.map((s) =>
      s.memberId === memberId ? { ...s, [field]: value || null } : s
    )
  )
}
```

#### 2c. Update `toggleMember` to include guardian fields in the default selection

**Current**:

```typescript
{ memberId, attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null }
```

**Replace with**:

```typescript
{ memberId, attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null }
```

#### 2d. Add guardian input fields to the template

Add a new block for guardian fields, visible when the member is selected AND is a minor:

```vue
<!-- Guardian info for minors -->
<div
  v-if="isSelected(member.id) && isMinor(member)"
  class="mt-2 space-y-2 border-t border-gray-100 pt-2"
  @click.stop
>
  <p class="text-xs font-medium text-gray-500">Datos del tutor/a legal</p>
  <InputText
    :model-value="getSelection(member.id)?.guardianName ?? ''"
    placeholder="Nombre completo del tutor/a"
    class="w-full text-sm"
    :maxlength="200"
    :data-testid="`guardian-name-${member.id}`"
    @update:model-value="(v: string) => updateGuardianField(member.id, 'guardianName', v)"
  />
  <InputText
    :model-value="getSelection(member.id)?.guardianDocumentNumber ?? ''"
    placeholder="DNI / Documento del tutor/a"
    class="w-full text-sm"
    :maxlength="50"
    :data-testid="`guardian-doc-${member.id}`"
    @update:model-value="(v: string) => updateGuardianField(member.id, 'guardianDocumentNumber', v)"
  />
</div>
```

#### 2e. Add `InputText` import

Add to the imports at the top of the script:

```typescript
import InputText from 'primevue/inputtext'
```

---

### Step 3: Add Preference Fields to `RegisterForCampPage.vue`

- **File**: `frontend/src/views/registrations/RegisterForCampPage.vue`
- **Action**: Add `SpecialNeeds` and `CampatesPreference` fields to the existing wizard flow (in Step 2: Extras, or as a small section before the Confirm step)

#### 3a. Add new reactive state

Add after existing reactive state:

```typescript
const specialNeeds = ref<string>('')
const campatesPreference = ref<string>('')
```

#### 3b. Add preference fields to an existing wizard step

Add the following fields to the existing **Step 2 (Extras)** panel, or create a small section within it:

```vue
<!-- Special needs -->
<div class="mb-5">
  <label class="mb-1 block text-sm font-medium text-gray-700">
    Necesidades especiales
  </label>
  <Textarea
    v-model="specialNeeds"
    :rows="2"
    :maxlength="2000"
    placeholder="Dietas especiales, necesidades de movilidad, etc."
    class="w-full"
    data-testid="special-needs"
  />
</div>

<!-- Campmates preference -->
<div class="mb-5">
  <label class="mb-1 block text-sm font-medium text-gray-700">
    Preferencia de acampantes
  </label>
  <Textarea
    v-model="campatesPreference"
    :rows="2"
    :maxlength="500"
    placeholder="Con quien te gustaria acampar cerca..."
    class="w-full"
    data-testid="campates-preference"
  />
</div>
```

#### 3c. Update `handleConfirm` to send the new fields

**Current** `handleConfirm` sends:

```typescript
const created = await createRegistration({
  campEditionId: editionId.value,
  familyUnitId: familyUnit.value.id,
  members: selectedMembers.value.map((s) => ({
    memberId: s.memberId,
    attendancePeriod: s.attendancePeriod,
    visitStartDate: s.visitStartDate ?? null,
    visitEndDate: s.visitEndDate ?? null
  })),
  notes: notes.value || null
})
```

**Replace with**:

```typescript
const created = await createRegistration({
  campEditionId: editionId.value,
  familyUnitId: familyUnit.value.id,
  members: selectedMembers.value.map((s) => ({
    memberId: s.memberId,
    attendancePeriod: s.attendancePeriod,
    visitStartDate: s.visitStartDate ?? null,
    visitEndDate: s.visitEndDate ?? null,
    guardianName: s.guardianName || null,
    guardianDocumentNumber: s.guardianDocumentNumber || null
  })),
  notes: notes.value || null,
  specialNeeds: specialNeeds.value || null,
  campatesPreference: campatesPreference.value || null
})
```

#### 3d. Add preference fields summary to the Confirm step review

In the Confirm step, add a section after the Notes summary:

```vue
<!-- Preference fields summary -->
<div
  v-if="specialNeeds || campatesPreference"
  class="mb-4 rounded-lg border border-gray-200 p-4"
>
  <h3 class="mb-2 text-sm font-semibold text-gray-700">Informacion adicional</h3>
  <dl class="space-y-1 text-sm">
    <div v-if="specialNeeds" class="flex gap-2">
      <dt class="text-gray-500">Necesidades:</dt>
      <dd class="text-gray-800">{{ specialNeeds }}</dd>
    </div>
    <div v-if="campatesPreference" class="flex gap-2">
      <dt class="text-gray-500">Acampantes:</dt>
      <dd class="text-gray-800">{{ campatesPreference }}</dd>
    </div>
  </dl>
</div>
```

---

### Step 4: Update `RegistrationDetailPage.vue` — Display Preference Fields

- **File**: `frontend/src/views/registrations/RegistrationDetailPage.vue`
- **Action**: Add a new section below the Notes section to display the preference fields

```vue
<!-- Preference fields -->
<div
  v-if="registration.specialNeeds || registration.campatesPreference"
  class="mb-6 rounded-lg border border-gray-200 bg-gray-50 p-4"
>
  <h2 class="mb-3 text-sm font-semibold text-gray-700">Informacion adicional</h2>
  <dl class="space-y-2 text-sm">
    <!-- Special needs -->
    <div v-if="registration.specialNeeds" class="flex flex-col gap-0.5">
      <dt class="font-medium text-gray-600">Necesidades especiales</dt>
      <dd class="whitespace-pre-line text-gray-800">{{ registration.specialNeeds }}</dd>
    </div>

    <!-- Campmates preference -->
    <div v-if="registration.campatesPreference" class="flex flex-col gap-0.5">
      <dt class="font-medium text-gray-600">Preferencia de acampantes</dt>
      <dd class="text-gray-800">{{ registration.campatesPreference }}</dd>
    </div>
  </dl>
</div>
```

---

### Step 5: Update `RegistrationPricingBreakdown.vue` — Show Guardian Info

- **File**: `frontend/src/components/registrations/RegistrationPricingBreakdown.vue`
- **Action**: Show guardian name and document number inline for member rows where `guardianName` is set

**Current**:

```vue
<td class="px-4 py-2 text-gray-900">{{ member.fullName }}</td>
```

**Replace with**:

```vue
<td class="px-4 py-2">
  <span class="text-gray-900">{{ member.fullName }}</span>
  <span
    v-if="member.guardianName"
    class="block text-xs text-gray-400"
    :data-testid="`guardian-info-${member.familyMemberId}`"
  >
    Tutor/a: {{ member.guardianName }}
    <span v-if="member.guardianDocumentNumber"> · {{ member.guardianDocumentNumber }}</span>
  </span>
</td>
```

---

### Step 6: Update Tests

#### 6a. Update `RegistrationPricingBreakdown.test.ts`

- **File**: `frontend/src/components/registrations/__tests__/RegistrationPricingBreakdown.test.ts`
- **Action**: Update `baseMember` and test data to include the new `guardianName` and `guardianDocumentNumber` fields, and add new tests for guardian info display

**Update `baseMember`** — add the two new fields with `null`:

```typescript
const baseMember: MemberPricingDetail = {
  familyMemberId: 'member-1',
  fullName: 'Ana Garcia',
  ageAtCamp: 35,
  ageCategory: 'Adult',
  attendancePeriod: 'Complete',
  attendanceDays: 14,
  visitStartDate: null,
  visitEndDate: null,
  individualAmount: 450,
  guardianName: null,
  guardianDocumentNumber: null
}
```

**Also update** all inline member objects in other tests to include the two new fields with `null`.

**Add new tests**:

```typescript
it('should show guardian info for members with guardianName set', () => {
  const pricingWithGuardian: PricingBreakdown = {
    ...basePricing,
    members: [
      {
        ...baseMember,
        familyMemberId: 'child-1',
        fullName: 'Pau Garcia',
        ageAtCamp: 8,
        ageCategory: 'Child',
        individualAmount: 300,
        guardianName: 'Ana Garcia',
        guardianDocumentNumber: '12345678A'
      }
    ],
    baseTotalAmount: 300,
    totalAmount: 300
  }
  const wrapper = mountComponent({ pricing: pricingWithGuardian })
  expect(wrapper.find('[data-testid="guardian-info-child-1"]').exists()).toBe(true)
  expect(wrapper.text()).toContain('Tutor/a: Ana Garcia')
  expect(wrapper.text()).toContain('12345678A')
})

it('should NOT show guardian info when guardianName is null', () => {
  const wrapper = mountComponent({ pricing: basePricing })
  expect(wrapper.find('[data-testid="guardian-info-member-1"]').exists()).toBe(false)
})
```

#### 6b. Update `RegistrationMemberSelector.test.ts`

- **File**: `frontend/src/components/registrations/__tests__/RegistrationMemberSelector.test.ts`
- **Action**: Update test data for `WizardMemberSelection` objects to include the new `guardianName` and `guardianDocumentNumber` fields (both `null`), wherever `WizardMemberSelection` objects are created in tests.

---

### Step 7: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. No new API endpoints were added — `api-spec.yml` updates handled by backend plan
  2. No new components created — `frontend-standards.mdc` structure unchanged
  3. No new npm packages — `package.json` unchanged
  4. Verify the TypeScript types mirror the backend DTOs accurately
  5. Update this plan file with any deviations made during implementation
  6. Confirm all code comments are in English
- **Notes**: All documentation in English. This step is MANDATORY.

---

## Implementation Order

1. **Step 0**: Create feature branch `feature/feat-registration-extra-fields2-frontend`
2. **Step 1**: Extend TypeScript types in `registration.ts` (interfaces)
3. **Step 2**: Update `RegistrationMemberSelector.vue` — guardian fields for minors
4. **Step 3**: Add preference fields to `RegisterForCampPage.vue`
5. **Step 4**: Update `RegistrationDetailPage.vue` — display preference fields
6. **Step 5**: Update `RegistrationPricingBreakdown.vue` — guardian info display
7. **Step 6**: Update tests
8. **Step 7**: Update technical documentation

---

## Testing Checklist

### Vitest Unit Tests

- [ ] `RegistrationPricingBreakdown`: guardian info displays when `guardianName` is set
- [ ] `RegistrationPricingBreakdown`: guardian info hidden when `guardianName` is null
- [ ] `RegistrationPricingBreakdown`: existing tests pass with updated `MemberPricingDetail` type (add null guardian fields)
- [ ] `RegistrationMemberSelector`: existing tests pass with updated `WizardMemberSelection` type

### Manual / E2E Verification

- [ ] Wizard Step 1: minor members show guardian input fields
- [ ] Wizard Step 1: adult members do NOT show guardian fields
- [ ] Wizard: special needs textarea works (max 2000 chars)
- [ ] Wizard: campmates preference textarea works (max 500 chars)
- [ ] Wizard Confirm step: preference fields summary displays correctly
- [ ] Wizard Confirm step: guardian info is included in API request
- [ ] Detail page: preference fields section displays when data is present
- [ ] Detail page: preference fields section hidden when all fields are null/empty
- [ ] Pricing breakdown: guardian info shown under minor member names

---

## Error Handling Patterns

- **No new error handling needed** — the composable `useRegistrations.createRegistration()` already handles API errors and sets `error.value`. Validation errors from the backend are displayed via the existing toast pattern.
- **Loading states** — the existing `loading` ref from `useRegistrations` covers the confirm action. No additional loading states needed for the new form fields (they are local state only).

---

## UI/UX Considerations

### PrimeVue Components Used

| Component | Usage |
| --------- | ----- |
| `Textarea` | Special needs, campmates preference |
| `InputText` | Guardian name, guardian document number |

### Layout

- Preference fields use consistent `mb-5` spacing between field groups
- Labels use `text-sm font-medium text-gray-700` (matching existing form patterns)
- Guardian fields use `text-xs font-medium text-gray-500` label for visual hierarchy

### Accessibility

- All form fields have associated `<label>` elements
- `maxlength` attributes on text inputs for visual feedback
- `data-testid` attributes on key interactive elements

### Responsive Design

- Preference fields stack vertically on all screen sizes (simple textarea layout)
- Guardian fields are nested inside member cards and follow their responsive behavior

---

## Dependencies

### npm Packages

No new packages required. All PrimeVue components used are already available:

- `primevue/inputtext` — already used elsewhere
- `primevue/textarea` — already used in the wizard

---

## Notes

### Business Rules

1. **All new fields are optional.** The wizard should make it clear these fields are optional.
2. **Guardian info is shown for minors only** (approximate age < 18 based on `dateOfBirth`). The backend does not enforce guardian info at submission time.
3. **SpecialNeeds and CampatesPreference are free-text fields** — no validation beyond max length.

### Important Caveats

- **Guardian fields use approximate age**: The `isMinor()` helper uses current date, not camp start date. This is intentional — it's just a UI hint. The backend calculates exact age at camp.
- **`WizardMemberSelection` type change**: Adding `guardianName` and `guardianDocumentNumber` means every place that creates a `WizardMemberSelection` object must include these fields. The `RegistrationMemberSelector.toggleMember()` default already handles this (Step 2c).

### Language

- All code (variables, comments, test names) in English
- UI labels and placeholder text in Spanish (matching existing convention)

### Extracted Tickets

The following frontend features will be implemented in separate tickets:

1. **`feat-registration-accommodations`** — Accommodation preference UI (ranked selection, drag-to-rank or dropdowns)
2. **`feat-registration-activities`** — Activity sign-up UI (checkbox group with conditions display)
3. **`feat-camp-edition-extras-registration`** — Extras selection during registration (quantity/boolean inputs with pricing)

---

## Next Steps After Implementation

1. Integration testing with the backend after both branches are merged
2. Implement extracted tickets: accommodations, activities, extras-registration
3. Consider adding a visual warning for minors without guardian info (post-creation)
4. Admin export view for the new fields (separate ticket)

---

## Implementation Verification

- [ ] **Code Quality**: TypeScript strict, no `any`, `<script setup lang="ts">` on all components
- [ ] **Functionality**: Wizard flow works end-to-end with preference fields, detail page displays new fields
- [ ] **Testing**: Vitest tests pass, guardian info tests added to `RegistrationPricingBreakdown.test.ts`
- [ ] **Integration**: `CreateRegistrationRequest` sends all new fields to backend API correctly
- [ ] **Documentation**: Updated as needed
