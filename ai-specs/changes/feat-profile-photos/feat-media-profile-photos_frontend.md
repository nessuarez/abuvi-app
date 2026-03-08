# Frontend Implementation Plan: feat-media-profile-photos — Profile Photos for FamilyMember and FamilyUnit

## Overview

This feature adds profile photo display and upload/delete functionality to the family profile experience. Family representatives can upload photos for their family members and family unit, with avatars displayed throughout the profile page and family member lists. The implementation follows Vue 3 Composition API patterns with PrimeVue + Tailwind CSS, using composables for API communication and the existing `useBlobStorage` composable for file uploads.

---

## Architecture Context

### Components/composables involved

| File | Action |
|------|--------|
| `frontend/src/types/family-unit.ts` | Add `profilePhotoUrl` to response types |
| `frontend/src/types/blob-storage.ts` | Add `'profile-photos'` to `BlobFolder` type |
| `frontend/src/composables/useFamilyUnits.ts` | Add `uploadMemberProfilePhoto`, `removeMemberProfilePhoto`, `uploadUnitProfilePhoto`, `removeUnitProfilePhoto` methods |
| `frontend/src/components/family-units/ProfilePhotoAvatar.vue` | **New** — reusable avatar component with photo/fallback display and edit overlay |
| `frontend/src/views/ProfilePage.vue` | Show family unit avatar and member avatars; wire upload/delete flow |
| `frontend/src/views/FamilyUnitPage.vue` | Show family unit avatar with upload; show member avatars in list |
| `frontend/src/components/family-units/FamilyMemberList.vue` | Add avatar column to data table |

### Routing considerations

No new routes needed. Profile page (`/profile`) and family unit page (`/family-unit`) already exist.

### State management approach

Local component state — no new Pinia store needed. The `useFamilyUnits` composable already manages reactive `familyUnit` and `familyMembers` state. After a photo upload, refresh the relevant entity from the composable to update the UI.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to `feat/media-profile-photos-frontend`
- **Implementation Steps**:
  1. `git checkout dev && git pull origin dev`
  2. `git checkout -b feat/media-profile-photos-frontend`
  3. `git branch` to verify
- **Notes**: If `feat/media-profile-photos` already exists (backend work done), branch from it instead. Use the `-frontend` suffix per project convention.

---

### Step 1: Update TypeScript Types

#### 1a: Add `profilePhotoUrl` to Family Types

- **File**: `frontend/src/types/family-unit.ts`
- **Action**: Add `profilePhotoUrl` to response types

Add to `FamilyUnitResponse`:
```typescript
export interface FamilyUnitResponse {
  id: string
  name: string
  representativeUserId: string
  profilePhotoUrl: string | null  // <-- ADD
  createdAt: string
  updatedAt: string
}
```

Add to `FamilyMemberResponse`:
```typescript
export interface FamilyMemberResponse {
  id: string
  familyUnitId: string
  userId: string | null
  firstName: string
  lastName: string
  dateOfBirth: string
  relationship: FamilyRelationship
  documentNumber: string | null
  email: string | null
  phone: string | null
  hasMedicalNotes: boolean
  hasAllergies: boolean
  profilePhotoUrl: string | null  // <-- ADD
  createdAt: string
  updatedAt: string
}
```

#### 1b: Add `'profile-photos'` to BlobFolder

- **File**: `frontend/src/types/blob-storage.ts`
- **Action**: Extend the `BlobFolder` type

```typescript
export type BlobFolder = 'photos' | 'media-items' | 'camp-locations' | 'camp-photos' | 'payment-proofs' | 'profile-photos'
```

---

### Step 2: Add Profile Photo API Methods to useFamilyUnits Composable

- **File**: `frontend/src/composables/useFamilyUnits.ts`
- **Action**: Add 4 new methods for profile photo management

```typescript
// ── Profile Photo: Family Member ──

const uploadMemberProfilePhoto = async (
  familyUnitId: string,
  memberId: string,
  file: File
): Promise<FamilyMemberResponse | null> => {
  loading.value = true
  error.value = null
  try {
    const formData = new FormData()
    formData.append('file', file)

    const response = await api.put<ApiResponse<FamilyMemberResponse>>(
      `/family-units/${familyUnitId}/members/${memberId}/profile-photo`,
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } }
    )
    if (response.data.success && response.data.data) {
      // Update local state
      const idx = familyMembers.value.findIndex(m => m.id === memberId)
      if (idx !== -1) {
        familyMembers.value[idx] = response.data.data
      }
      return response.data.data
    }
    return null
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : 'Error al subir la foto de perfil'
    return null
  } finally {
    loading.value = false
  }
}

const removeMemberProfilePhoto = async (
  familyUnitId: string,
  memberId: string
): Promise<boolean> => {
  loading.value = true
  error.value = null
  try {
    await api.delete(`/family-units/${familyUnitId}/members/${memberId}/profile-photo`)
    // Update local state
    const idx = familyMembers.value.findIndex(m => m.id === memberId)
    if (idx !== -1) {
      familyMembers.value[idx] = { ...familyMembers.value[idx], profilePhotoUrl: null }
    }
    return true
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : 'Error al eliminar la foto de perfil'
    return false
  } finally {
    loading.value = false
  }
}

// ── Profile Photo: Family Unit ──

const uploadUnitProfilePhoto = async (
  familyUnitId: string,
  file: File
): Promise<FamilyUnitResponse | null> => {
  loading.value = true
  error.value = null
  try {
    const formData = new FormData()
    formData.append('file', file)

    const response = await api.put<ApiResponse<FamilyUnitResponse>>(
      `/family-units/${familyUnitId}/profile-photo`,
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } }
    )
    if (response.data.success && response.data.data) {
      familyUnit.value = response.data.data
      return response.data.data
    }
    return null
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : 'Error al subir la foto familiar'
    return null
  } finally {
    loading.value = false
  }
}

const removeUnitProfilePhoto = async (
  familyUnitId: string
): Promise<boolean> => {
  loading.value = true
  error.value = null
  try {
    await api.delete(`/family-units/${familyUnitId}/profile-photo`)
    if (familyUnit.value) {
      familyUnit.value = { ...familyUnit.value, profilePhotoUrl: null }
    }
    return true
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : 'Error al eliminar la foto familiar'
    return false
  } finally {
    loading.value = false
  }
}
```

Return all four methods from the composable's return object.

- **Implementation Notes**:
  - Uses `api.put` with `multipart/form-data` — same pattern as `useBlobStorage.uploadFile()`.
  - Updates local reactive state immediately after success, no need for a full refetch.
  - Error messages in Spanish, matching project convention.

---

### Step 3: Create ProfilePhotoAvatar Component

- **File**: `frontend/src/components/family-units/ProfilePhotoAvatar.vue` (**new**)
- **Action**: Create a reusable avatar component with photo display, initials fallback, and optional edit overlay

```vue
<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'

const props = defineProps<{
  photoUrl: string | null
  initials: string
  size?: 'sm' | 'md' | 'lg'
  editable?: boolean
  loading?: boolean
}>()

const emit = defineEmits<{
  (e: 'upload', file: File): void
  (e: 'remove'): void
}>()

const fileInput = ref<HTMLInputElement | null>(null)
const imgError = ref(false)

const sizeClasses = computed(() => {
  switch (props.size) {
    case 'sm': return 'h-10 w-10 text-sm'
    case 'lg': return 'h-20 w-20 text-2xl'
    default: return 'h-14 w-14 text-xl'
  }
})

const showPhoto = computed(() => props.photoUrl && !imgError.value)

function triggerUpload() {
  fileInput.value?.click()
}

function onFileSelected(event: Event) {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  if (file) {
    emit('upload', file)
  }
  // Reset input to allow re-selecting the same file
  if (fileInput.value) {
    fileInput.value.value = ''
  }
}
</script>

<template>
  <div class="group relative inline-flex">
    <!-- Avatar circle -->
    <div
      :class="[
        'flex shrink-0 items-center justify-center rounded-full font-bold',
        sizeClasses,
        showPhoto ? '' : 'bg-primary-100 text-primary-700'
      ]"
    >
      <img
        v-if="showPhoto"
        :src="photoUrl!"
        alt="Foto de perfil"
        class="h-full w-full rounded-full object-cover"
        @error="imgError = true"
      />
      <span v-else>{{ initials }}</span>
    </div>

    <!-- Edit overlay (shown on hover when editable) -->
    <div
      v-if="editable && !loading"
      class="absolute inset-0 flex items-center justify-center rounded-full bg-black/50 opacity-0 transition-opacity group-hover:opacity-100"
    >
      <div class="flex gap-1">
        <Button
          icon="pi pi-camera"
          severity="secondary"
          text
          rounded
          size="small"
          class="!text-white"
          @click="triggerUpload"
          aria-label="Subir foto"
        />
        <Button
          v-if="photoUrl"
          icon="pi pi-trash"
          severity="danger"
          text
          rounded
          size="small"
          class="!text-white"
          @click="$emit('remove')"
          aria-label="Eliminar foto"
        />
      </div>
    </div>

    <!-- Loading spinner overlay -->
    <div
      v-if="loading"
      class="absolute inset-0 flex items-center justify-center rounded-full bg-black/40"
    >
      <i class="pi pi-spin pi-spinner text-white" />
    </div>

    <!-- Hidden file input -->
    <input
      v-if="editable"
      ref="fileInput"
      type="file"
      accept=".jpg,.jpeg,.png,.webp"
      class="hidden"
      @change="onFileSelected"
    />
  </div>
</template>
```

- **Implementation Notes**:
  - Three sizes: `sm` (40px, for data table rows), `md` (56px, default), `lg` (80px, for family unit header).
  - Fallback: colored circle with initials when no photo or image load error.
  - Edit overlay appears on hover with camera (upload) and trash (remove) icons.
  - File input accepts only `.jpg`, `.jpeg`, `.png`, `.webp` — matching backend validation.
  - `imgError` ref handles broken image URLs gracefully.
  - Loading state shows a spinner overlay during upload.
  - No `<style>` block — all Tailwind utilities.
  - Accessible with `aria-label` on buttons.

---

### Step 4: Integrate ProfilePhotoAvatar into ProfilePage

- **File**: `frontend/src/views/ProfilePage.vue`
- **Action**: Replace the CSS initials avatar with `ProfilePhotoAvatar` for both the user/family-unit header and family members

#### 4a: Family Unit Avatar (top of profile)

Replace the existing initials `<div>` (currently showing user initials) with:

```vue
<ProfilePhotoAvatar
  :photo-url="familyUnit?.profilePhotoUrl ?? null"
  :initials="(auth.user?.firstName?.[0] ?? '') + (auth.user?.lastName?.[0] ?? '')"
  size="lg"
  :editable="isRepresentative"
  :loading="uploadingUnitPhoto"
  @upload="onUploadUnitPhoto"
  @remove="onRemoveUnitPhoto"
/>
```

#### 4b: Family Member Avatars (in the members section)

In the family members DataTable or list area, add an avatar before each member's name:

```vue
<ProfilePhotoAvatar
  :photo-url="member.profilePhotoUrl"
  :initials="(member.firstName?.[0] ?? '') + (member.lastName?.[0] ?? '')"
  size="sm"
  :editable="isRepresentative"
  :loading="uploadingMemberPhotoId === member.id"
  @upload="(file) => onUploadMemberPhoto(member.id, file)"
  @remove="() => onRemoveMemberPhoto(member.id)"
/>
```

#### 4c: Script additions

Add to `<script setup>`:

```typescript
import ProfilePhotoAvatar from '@/components/family-units/ProfilePhotoAvatar.vue'

const {
  uploadMemberProfilePhoto,
  removeMemberProfilePhoto,
  uploadUnitProfilePhoto,
  removeUnitProfilePhoto
} = useFamilyUnits()

const uploadingUnitPhoto = ref(false)
const uploadingMemberPhotoId = ref<string | null>(null)

const isRepresentative = computed(() =>
  familyUnit.value?.representativeUserId === auth.user?.id
)

async function onUploadUnitPhoto(file: File) {
  if (!familyUnit.value) return
  uploadingUnitPhoto.value = true
  const result = await uploadUnitProfilePhoto(familyUnit.value.id, file)
  uploadingUnitPhoto.value = false
  if (result) {
    toast.add({ severity: 'success', summary: 'Foto actualizada', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error al subir la foto', life: 5000 })
  }
}

async function onRemoveUnitPhoto() {
  if (!familyUnit.value) return
  uploadingUnitPhoto.value = true
  const ok = await removeUnitProfilePhoto(familyUnit.value.id)
  uploadingUnitPhoto.value = false
  if (ok) {
    toast.add({ severity: 'success', summary: 'Foto eliminada', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error al eliminar la foto', life: 5000 })
  }
}

async function onUploadMemberPhoto(memberId: string, file: File) {
  if (!familyUnit.value) return
  uploadingMemberPhotoId.value = memberId
  const result = await uploadMemberProfilePhoto(familyUnit.value.id, memberId, file)
  uploadingMemberPhotoId.value = null
  if (result) {
    toast.add({ severity: 'success', summary: 'Foto actualizada', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error al subir la foto', life: 5000 })
  }
}

async function onRemoveMemberPhoto(memberId: string) {
  if (!familyUnit.value) return
  uploadingMemberPhotoId.value = memberId
  const ok = await removeMemberProfilePhoto(familyUnit.value.id, memberId)
  uploadingMemberPhotoId.value = null
  if (ok) {
    toast.add({ severity: 'success', summary: 'Foto eliminada', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error al eliminar la foto', life: 5000 })
  }
}
```

- **Implementation Notes**:
  - `isRepresentative` controls the `editable` prop — only the family representative can upload/delete.
  - Admin users viewing via admin panel will use the FamilyUnitPage, not ProfilePage, so admin edit is handled there (Step 5).
  - Toast notifications provide user feedback in Spanish.
  - Per-member loading state uses `uploadingMemberPhotoId` to show the spinner on the correct avatar only.

---

### Step 5: Integrate ProfilePhotoAvatar into FamilyUnitPage

- **File**: `frontend/src/views/FamilyUnitPage.vue`
- **Action**: Add family unit avatar at the top and pass avatars through to the member list

#### 5a: Family Unit Header Avatar

Add `ProfilePhotoAvatar` next to the family unit name in the header section:

```vue
<ProfilePhotoAvatar
  :photo-url="familyUnit?.profilePhotoUrl ?? null"
  :initials="familyUnit?.name?.[0] ?? 'F'"
  size="lg"
  :editable="canEdit"
  :loading="uploadingUnitPhoto"
  @upload="onUploadUnitPhoto"
  @remove="onRemoveUnitPhoto"
/>
```

Where `canEdit` is the existing editability check (representative or admin viewing their own family).

#### 5b: Script additions

Same upload/remove handlers as ProfilePage (Step 4c), adapted to the FamilyUnitPage's existing composable usage.

- **Implementation Notes**: `FamilyUnitPage` already has `canEdit` / `readOnly` logic. The `editable` prop on `ProfilePhotoAvatar` should be `!readOnly` (or `canEdit && !readOnly`).

---

### Step 6: Add Avatar Column to FamilyMemberList

- **File**: `frontend/src/components/family-units/FamilyMemberList.vue`
- **Action**: Add a photo avatar to each row in the DataTable

#### 6a: Add avatar before member name

In the Name column template, prepend the avatar:

```vue
<template #body="{ data }">
  <div class="flex items-center gap-2">
    <ProfilePhotoAvatar
      :photo-url="data.profilePhotoUrl"
      :initials="(data.firstName?.[0] ?? '') + (data.lastName?.[0] ?? '')"
      size="sm"
      :editable="!readOnly"
      :loading="uploadingMemberId === data.id"
      @upload="(file: File) => $emit('uploadPhoto', data.id, file)"
      @remove="() => $emit('removePhoto', data.id)"
    />
    <span>{{ data.firstName }} {{ data.lastName }}</span>
  </div>
</template>
```

#### 6b: Add new props and emits

```typescript
const props = defineProps<{
  members: FamilyMemberResponse[]
  loading: boolean
  canManageMemberships: boolean
  readOnly: boolean
  uploadingMemberId?: string | null  // <-- ADD
}>()

const emit = defineEmits<{
  (e: 'edit', member: FamilyMemberResponse): void
  (e: 'delete', member: FamilyMemberResponse): void
  (e: 'manageMembership', member: FamilyMemberResponse): void
  (e: 'uploadPhoto', memberId: string, file: File): void    // <-- ADD
  (e: 'removePhoto', memberId: string): void                 // <-- ADD
}>()
```

- **Implementation Notes**:
  - The `uploadingMemberId` prop tells the component which member's avatar is currently uploading.
  - Photo upload/remove events bubble up to the parent page (ProfilePage or FamilyUnitPage) which calls the composable.
  - `readOnly` controls whether the avatar is editable (hover overlay visible).

---

### Step 7: Write Vitest Unit Tests

- **File**: `frontend/src/__tests__/components/family-units/ProfilePhotoAvatar.spec.ts` (**new**)
- **Framework**: Vitest + Vue Test Utils

#### Test cases:

1. `renders initials when photoUrl is null` — shows initials span, no img tag
2. `renders image when photoUrl is provided` — shows img tag with correct src
3. `falls back to initials on image load error` — emit error event on img, verify initials shown
4. `applies correct size classes for sm/md/lg` — verify CSS classes
5. `shows edit overlay on hover when editable` — verify overlay elements present with group-hover classes
6. `hides edit overlay when not editable` — verify no overlay
7. `emits upload event when file selected` — trigger change on file input, verify emit
8. `emits remove event when trash button clicked` — click remove, verify emit
9. `hides remove button when no existing photo` — verify no trash button when photoUrl is null
10. `shows loading spinner when loading prop is true` — verify spinner overlay

- **File**: `frontend/src/__tests__/composables/useFamilyUnits.profilePhoto.spec.ts` (**new**)

#### Test cases:

11. `uploadMemberProfilePhoto sends PUT with FormData` — mock api.put, verify call
12. `uploadMemberProfilePhoto updates local state on success` — verify familyMembers ref updated
13. `uploadMemberProfilePhoto sets error on failure` — mock rejected request
14. `removeMemberProfilePhoto sends DELETE and clears photoUrl` — verify local state nulled
15. `uploadUnitProfilePhoto updates familyUnit ref on success`
16. `removeUnitProfilePhoto clears familyUnit.profilePhotoUrl`

---

### Step 8: Write Cypress E2E Tests (Optional — Critical Flow)

- **File**: `frontend/cypress/e2e/profile-photo.cy.ts` (**new**)
- **Framework**: Cypress

#### Test cases:

1. `Representative can upload a profile photo for a family member` — navigate to profile, hover avatar, click camera, select file, verify toast success and image displayed
2. `Representative can remove a family member profile photo` — upload first, then hover, click trash, verify fallback initials shown
3. `Representative can upload family unit photo` — same flow for unit avatar
4. `Non-representative does not see edit overlay on avatars` — log in as non-representative member, verify no hover overlay

- **Implementation Notes**: Cypress tests may require API stubbing (`cy.intercept`) for the multipart upload endpoints since the backend may not be available in CI.

---

### Step 9: Update Technical Documentation

- **Action**: Review and update technical documentation
- **Implementation Steps**:
  1. **Types documentation**: Note the addition of `profilePhotoUrl` to `FamilyUnitResponse` and `FamilyMemberResponse` in any API type documentation.
  2. **Component documentation**: If there's a component index, add `ProfilePhotoAvatar.vue` under `family-units/`.
  3. **BlobFolder type**: Document the addition of `'profile-photos'` as a valid folder.
  4. **Composable documentation**: Note the 4 new methods added to `useFamilyUnits`.
- **Notes**: All documentation in English per `documentation-standards.mdc`.

---

## Implementation Order

1. **Step 0** — Create feature branch
2. **Step 1** — Update TypeScript types (`family-unit.ts`, `blob-storage.ts`)
3. **Step 2** — Add profile photo API methods to `useFamilyUnits` composable
4. **Step 3** — Create `ProfilePhotoAvatar.vue` component
5. **Step 6** — Add avatar column to `FamilyMemberList.vue`
6. **Step 4** — Integrate into `ProfilePage.vue`
7. **Step 5** — Integrate into `FamilyUnitPage.vue`
8. **Step 7** — Write Vitest unit tests
9. **Step 8** — Write Cypress E2E tests
10. **Step 9** — Update technical documentation

---

## Testing Checklist

- [ ] `ProfilePhotoAvatar` renders photo when URL is set
- [ ] `ProfilePhotoAvatar` renders initials fallback when URL is null
- [ ] `ProfilePhotoAvatar` shows edit overlay on hover when `editable=true`
- [ ] `ProfilePhotoAvatar` hides edit overlay when `editable=false`
- [ ] `ProfilePhotoAvatar` handles image load errors gracefully
- [ ] `useFamilyUnits.uploadMemberProfilePhoto` sends correct PUT request
- [ ] `useFamilyUnits.removeMemberProfilePhoto` sends correct DELETE request
- [ ] `useFamilyUnits.uploadUnitProfilePhoto` sends correct PUT request
- [ ] `useFamilyUnits.removeUnitProfilePhoto` sends correct DELETE request
- [ ] Local reactive state updates after upload/delete
- [ ] Toast notifications shown on success/failure
- [ ] Loading spinner shows during upload
- [ ] File input accepts only image extensions
- [ ] E2E: Full upload flow works end-to-end

---

## Error Handling Patterns

| Scenario | Handling |
|----------|----------|
| Upload fails (network/server error) | `error.value` set in composable; toast error shown in component |
| File too large (413) | Axios interceptor or catch block; toast with "El archivo es demasiado grande" |
| Invalid file type | Prevented at file input level (`accept` attribute); additional backend validation returns 400 |
| Image load error (broken URL) | `imgError` ref in `ProfilePhotoAvatar`; falls back to initials |
| Not authorized (403) | Composable catches; toast with "No tienes permiso" |
| Entity not found (404) | Composable catches; toast with "No se encontro" |

---

## UI/UX Considerations

- **Avatar sizes**: `sm` (40px) for data table rows, `md` (56px) for inline displays, `lg` (80px) for family unit header.
- **Hover interaction**: Edit overlay (camera + trash icons) appears on hover with a dark semi-transparent background. On mobile, consider a tap-to-toggle approach — the `group-hover` may need a `@click` fallback for touch devices.
- **Fallback**: Colored circle (`bg-primary-100`) with initials in `text-primary-700` — matches the existing ProfilePage pattern.
- **Loading state**: Spinner overlay during upload prevents double-click issues.
- **Responsive**: Avatar sizes are fixed (not responsive breakpoints) since they're small enough for any screen.
- **Accessibility**: `aria-label` on upload/remove buttons; `alt` text on profile images.

---

## Dependencies

### npm packages (already installed)
- `primevue` — Button component for edit overlay
- `vue` — Composition API (ref, computed)
- No new packages needed

### PrimeVue components used
- `Button` — camera and trash icons in edit overlay
- `Toast` (via `useToast`) — success/error notifications

---

## Notes

1. **Spanish UI text**: All labels, tooltips, and toast messages are in Spanish (matching project convention). Technical code/comments in English.
2. **File accept filter**: The `<input accept>` attribute restricts file picker to `.jpg,.jpeg,.png,.webp`. This is a UX convenience — backend enforces the real validation.
3. **No dedicated composable**: Profile photo operations are added to `useFamilyUnits` rather than creating a separate `useProfilePhotos` composable, since the operations are tightly coupled to the FamilyUnit/FamilyMember entity lifecycle.
4. **Existing BlobUploadButton not reused**: The `BlobUploadButton.vue` targets the generic `/blobs/upload` endpoint. Profile photo endpoints are entity-specific (`PUT /family-units/{id}/profile-photo`), so a custom avatar component with integrated file input is more appropriate.
5. **Admin editing**: Admins viewing a family unit via the admin panel (`FamilyUnitPage`) can also upload/delete photos. The `canEdit` / `readOnly` logic already controls this.
6. **Mobile touch**: The hover-based edit overlay may not work on pure touch devices. Consider adding an optional tap interaction (e.g., single tap toggles overlay visibility) as a follow-up enhancement if needed.

---

## Next Steps After Implementation

1. **Backend dependency**: This frontend work depends on the backend `feat/media-profile-photos-backend` branch being merged first (4 new endpoints must exist).
2. **Image optimization**: Consider adding client-side image resizing before upload to reduce bandwidth (e.g., resize to max 1024px before sending). This is optional and can be a follow-up.
3. **Onboarding**: If the onboarding wizard includes family member creation, consider adding photo upload there as well (future enhancement).

---

## Implementation Verification

- [ ] **Code Quality**: TypeScript strict, no `any`, all components use `<script setup lang="ts">`, no `<style>` blocks
- [ ] **Functionality**: Avatars render with photo or initials fallback; upload/delete flow works; toast feedback shown
- [ ] **Testing**: Vitest unit tests for `ProfilePhotoAvatar` and composable methods; Cypress E2E for upload flow
- [ ] **Integration**: Composable methods call correct backend endpoints; local state updates reactively
- [ ] **Accessibility**: `aria-label` on buttons, `alt` on images
- [ ] **Documentation**: Updated types and component index documented
