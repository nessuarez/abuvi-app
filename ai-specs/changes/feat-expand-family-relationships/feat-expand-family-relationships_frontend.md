# Frontend Implementation Plan: feat-expand-family-relationships

## Overview

Update the frontend `FamilyRelationship` enum and Spanish labels to match the 6 new relationship types added in the backend. This is a minimal change affecting only the TypeScript types file — all components already consume the enum dynamically.

## Architecture Context

- **Types file**: `frontend/src/types/family-unit.ts` — enum definition and labels
- **Components using the enum** (no changes needed, they consume dynamically):
  - `frontend/src/components/family-units/FamilyMemberForm.vue` — dropdown selector
  - `frontend/src/components/family-units/FamilyMemberList.vue` — display label
  - `frontend/src/components/registrations/RegistrationMemberSelector.vue` — display label
  - `frontend/src/views/ProfilePage.vue` — display label
- **No routing, store, or composable changes needed**

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Name**: `feature/feat-expand-family-relationships-frontend`
- **Implementation Steps**:
  1. Ensure you're on the latest `dev` branch
  2. Pull latest changes: `git pull origin dev`
  3. Create new branch: `git checkout -b feature/feat-expand-family-relationships-frontend`
  4. Verify branch creation: `git branch`

### Step 1: Update `FamilyRelationship` Enum

- **File**: `frontend/src/types/family-unit.ts`
- **Action**: Add 6 new values to the enum, keeping `Other` last
- **Current code** (lines 1-7):

```typescript
export enum FamilyRelationship {
  Parent = 'Parent',
  Child = 'Child',
  Sibling = 'Sibling',
  Spouse = 'Spouse',
  Other = 'Other'
}
```

- **New code**:

```typescript
export enum FamilyRelationship {
  Parent = 'Parent',
  Child = 'Child',
  Sibling = 'Sibling',
  Spouse = 'Spouse',
  Grandparent = 'Grandparent',
  Grandchild = 'Grandchild',
  UncleAunt = 'UncleAunt',
  NephewNiece = 'NephewNiece',
  Cousin = 'Cousin',
  InLaw = 'InLaw',
  Other = 'Other'
}
```

### Step 2: Update `FamilyRelationshipLabels`

- **File**: `frontend/src/types/family-unit.ts`
- **Action**: Add Spanish labels for the 6 new values
- **Current code** (lines 84-90):

```typescript
export const FamilyRelationshipLabels: Record<FamilyRelationship, string> = {
  [FamilyRelationship.Parent]: 'Padre/Madre',
  [FamilyRelationship.Child]: 'Hijo/Hija',
  [FamilyRelationship.Sibling]: 'Hermano/Hermana',
  [FamilyRelationship.Spouse]: 'Cónyuge',
  [FamilyRelationship.Other]: 'Otro'
}
```

- **New code**:

```typescript
export const FamilyRelationshipLabels: Record<FamilyRelationship, string> = {
  [FamilyRelationship.Parent]: 'Padre/Madre',
  [FamilyRelationship.Child]: 'Hijo/Hija',
  [FamilyRelationship.Sibling]: 'Hermano/Hermana',
  [FamilyRelationship.Spouse]: 'Cónyuge',
  [FamilyRelationship.Grandparent]: 'Abuelo/Abuela',
  [FamilyRelationship.Grandchild]: 'Nieto/Nieta',
  [FamilyRelationship.UncleAunt]: 'Tío/Tía',
  [FamilyRelationship.NephewNiece]: 'Sobrino/Sobrina',
  [FamilyRelationship.Cousin]: 'Primo/Prima',
  [FamilyRelationship.InLaw]: 'Familia política',
  [FamilyRelationship.Other]: 'Otro'
}
```

### Step 3: Verify Component Compatibility

- **Action**: Verify that components using the enum iterate dynamically (no hardcoded values)
- **Files to check**:
  - `FamilyMemberForm.vue` — confirm dropdown options are built from the enum/labels object
  - `FamilyMemberList.vue` — confirm it uses `FamilyRelationshipLabels[member.relationship]`
  - `RegistrationMemberSelector.vue` — same check
  - `ProfilePage.vue` — same check
- **Expected**: No changes needed if components use `Object.entries(FamilyRelationshipLabels)` or similar dynamic patterns

### Step 4: Update Technical Documentation

- **Action**: No additional frontend documentation updates needed — the data model documentation is updated in the backend ticket

## Implementation Order

1. Step 0: Create feature branch
2. Step 1: Update `FamilyRelationship` enum
3. Step 2: Update `FamilyRelationshipLabels`
4. Step 3: Verify component compatibility
5. Step 4: Documentation verification

## Testing Checklist

- [ ] TypeScript compiles without errors (`npm run type-check`)
- [ ] `FamilyMemberForm.vue` dropdown shows all 11 relationship options
- [ ] Labels display correctly in Spanish for all new values
- [ ] Existing family members with old relationships render correctly
- [ ] Creating a new member with a new relationship type works
- [ ] Editing a member's relationship to a new type works

## UI/UX Considerations

- **Dropdown ordering**: Options should appear in the logical order defined in the enum (direct family first, extended family, then Other)
- **Dropdown width**: Verify that longer labels like "Familia política" fit without overflow
- No responsive design changes needed — existing form layout accommodates the dropdown

## Dependencies

- No new npm packages required
- No new PrimeVue components needed

## Notes

- This ticket depends on the backend ticket being merged first (or at least deployed), so that the API accepts the new enum values
- The `Record<FamilyRelationship, string>` type ensures TypeScript will error if any enum value is missing from the labels — this is the compile-time safety net
- All labels are in Spanish as per the existing pattern

## Implementation Verification

- [ ] TypeScript strict: no `any`, all enum values have labels
- [ ] Dropdown renders all 11 options in correct order
- [ ] No console errors or warnings
- [ ] Existing data displays correctly
