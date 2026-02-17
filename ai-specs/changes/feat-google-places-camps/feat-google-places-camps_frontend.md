# Frontend Implementation Plan: feat-google-places-camps - Google Places Autocomplete

## Overview

Implement Google Places autocomplete functionality for the camp location form, enabling users to search and auto-populate camp location data (name, address, coordinates) using Google Places API. This feature reduces manual data entry errors and improves user experience when creating or editing camp locations.

**Architecture Principles:**

- Vue 3 Composition API with TypeScript strict mode
- Composable-based architecture for API communication
- PrimeVue AutoComplete component for search interface
- Tailwind CSS for styling
- Debouncing with VueUse to optimize API calls
- Graceful error handling with fallback to manual entry

## Architecture Context

### Components/Composables Involved

**New Files:**

- `frontend/src/composables/useGooglePlaces.ts` - Composable for Google Places API calls
- `frontend/src/composables/__tests__/useGooglePlaces.test.ts` - Unit tests for composable
- `frontend/cypress/e2e/camps/google-places-autocomplete.cy.ts` - E2E tests

**Modified Files:**

- `frontend/src/types/camp.ts` - Add `googlePlaceId` field to camp types
- `frontend/src/components/camps/CampLocationForm.vue` - Add autocomplete functionality
- `frontend/src/components/camps/__tests__/CampLocationForm.test.ts` - Update component tests

### Backend Dependencies

**Required API Endpoints (already implemented in backend):**

- `POST /api/places/autocomplete` - Search for places by text input
- `POST /api/places/details` - Get detailed information for a selected place

**Authentication:** Both endpoints require JWT authentication

### State Management Approach

**Local Component State:**

- Form data (reactive object in component)
- Autocomplete suggestions (ref in component)
- Selected place (ref in component)
- Auto-fill indicator flag (ref in component)

**No Pinia Store needed** - This feature uses transient UI state that doesn't need to be shared across components

### Routing Considerations

No routing changes required. The autocomplete functionality will be integrated into existing camp creation/editing forms.

## Implementation Steps

### Step 0: Create Feature Branch

**Action:** Create and switch to a new feature branch following the development workflow

**Branch Naming:** `feature/feat-google-places-camps-frontend`

**Implementation Steps:**

1. Check current branch status: `git status`
2. Ensure on latest main branch: `git checkout main && git pull origin main`
3. Create new feature branch: `git checkout -b feature/feat-google-places-camps-frontend`
4. Verify branch creation: `git branch`

**Notes:**

- This MUST be the FIRST step before any code changes
- Branch name follows project convention: `feature/[ticket-id]-frontend`
- This separates frontend concerns from backend implementation

### Step 1: Update TypeScript Types

**File:** `frontend/src/types/camp.ts`

**Action:** Add `googlePlaceId` field to camp-related TypeScript interfaces

**Implementation Steps:**

1. Open `frontend/src/types/camp.ts`
2. Add `googlePlaceId` field to `Camp` interface:

   ```typescript
   export interface Camp {
     id: string
     name: string
     description: string | null
     location: string | null
     latitude: number | null
     longitude: number | null
     googlePlaceId: string | null // NEW
     pricePerAdult: number
     pricePerChild: number
     pricePerBaby: number
     isActive: boolean
     createdAt: string
     updatedAt: string
   }
   ```

3. Add `googlePlaceId` to `CreateCampRequest` interface:

   ```typescript
   export interface CreateCampRequest {
     name: string
     description: string | null
     location: string | null
     latitude: number | null
     longitude: number | null
     googlePlaceId: string | null // NEW
     pricePerAdult: number
     pricePerChild: number
     pricePerBaby: number
   }
   ```

4. Update `UpdateCampRequest` (extends CreateCampRequest, so inherits the field)

**Dependencies:** None

**Implementation Notes:**

- Field is nullable to support existing camps without Google Place ID
- Field is optional during creation/update (user can skip autocomplete)

### Step 2: Create Google Places Composable

**File:** `frontend/src/composables/useGooglePlaces.ts`

**Action:** Create a composable to encapsulate Google Places API communication

**Function Signature:**

```typescript
export function useGooglePlaces(): {
  loading: Ref<boolean>
  error: Ref<string | null>
  searchPlaces: (input: string) => Promise<PlaceAutocomplete[]>
  getPlaceDetails: (placeId: string) => Promise<PlaceDetails | null>
}
```

**Implementation Steps:**

1. Create file `frontend/src/composables/useGooglePlaces.ts`

2. Define TypeScript interfaces for Place data:

   ```typescript
   export interface PlaceAutocomplete {
     placeId: string
     description: string
     mainText: string
     secondaryText: string
   }

   export interface PlaceDetails {
     placeId: string
     name: string
     formattedAddress: string
     latitude: number
     longitude: number
     types: string[]
   }
   ```

3. Import required dependencies:

   ```typescript
   import { ref } from 'vue'
   import { api } from '@/utils/api'
   import type { ApiResponse } from '@/types/api'
   ```

4. Implement `searchPlaces` function:
   - Accept `input` string parameter
   - Return empty array if input is less than 3 characters
   - Set `loading.value = true` before request
   - Call `POST /api/places/autocomplete` with `{ input }`
   - Handle success: extract `response.data.data` and return array
   - Handle errors: set `error.value` with user-friendly message
   - Set `loading.value = false` in finally block
   - Return empty array on error

5. Implement `getPlaceDetails` function:
   - Accept `placeId` string parameter
   - Set `loading.value = true` before request
   - Call `POST /api/places/details` with `{ placeId }`
   - Handle success: return `response.data.data`
   - Handle errors: set `error.value` with user-friendly message
   - Set `loading.value = false` in finally block
   - Return null on error

6. Return reactive refs and functions from composable

**Dependencies:**

- `vue` (ref)
- `@/utils/api` (axios instance)
- `@/types/api` (ApiResponse type)

**Implementation Notes:**

- Minimum 3 characters before search to reduce unnecessary API calls
- All error messages should be user-friendly and in Spanish
- Loading state helps show spinner in UI
- Error state allows displaying error messages to user

### Step 3: Update CampLocationForm Component with Autocomplete

**File:** `frontend/src/components/camps/CampLocationForm.vue`

**Action:** Replace plain name input with PrimeVue AutoComplete, add debouncing, implement auto-fill logic

**Implementation Steps:**

1. **Update component imports:**

   ```typescript
   import { ref, reactive, watch } from 'vue'
   import { useDebounceFn } from '@vueuse/core'
   import AutoComplete from 'primevue/autocomplete'
   import Message from 'primevue/message'
   import { useGooglePlaces, type PlaceAutocomplete } from '@/composables/useGooglePlaces'
   ```

2. **Update Props interface** (if needed):
   - No changes needed to props, initialData already supports all fields

3. **Add reactive state for autocomplete:**

   ```typescript
   const placeSuggestions = ref<PlaceAutocomplete[]>([])
   const selectedPlace = ref<PlaceAutocomplete | null>(null)
   const autoFilledFromPlaces = ref(false)
   const searchQuery = ref(formData.name)
   ```

4. **Initialize composable:**

   ```typescript
   const { loading: placesLoading, error: placesError, searchPlaces, getPlaceDetails } = useGooglePlaces()
   ```

5. **Create debounced search function:**

   ```typescript
   const debouncedSearch = useDebounceFn(async (query: string) => {
     if (!query || query.length < 3) {
       placeSuggestions.value = []
       return
     }
     placeSuggestions.value = await searchPlaces(query)
   }, 300)
   ```

6. **Add watcher for search query:**

   ```typescript
   watch(searchQuery, (newQuery) => {
     debouncedSearch(newQuery)
   })
   ```

7. **Implement place selection handler:**

   ```typescript
   const handlePlaceSelected = async (event: { value: PlaceAutocomplete }) => {
     const place = event.value
     if (!place) return

     selectedPlace.value = place
     const details = await getPlaceDetails(place.placeId)

     if (details) {
       formData.name = details.name
       formData.location = details.formattedAddress
       formData.latitude = details.latitude
       formData.longitude = details.longitude
       formData.googlePlaceId = details.placeId

       // Auto-generate description if empty
       if (!formData.description) {
         formData.description = generateDescription(details)
       }

       autoFilledFromPlaces.value = true
     }
   }
   ```

8. **Implement description generator:**

   ```typescript
   const generateDescription = (details: { name: string; formattedAddress: string; types: string[] }): string => {
     const typeDescriptions: Record<string, string> = {
       'campground': 'Zona de camping',
       'park': 'Parque natural',
       'lodging': 'Alojamiento',
       'establishment': 'Establecimiento'
     }

     const matchedType = details.types.find(t => typeDescriptions[t])
     const typeDesc = matchedType ? typeDescriptions[matchedType] : 'Ubicación'

     return `${typeDesc} ubicada en ${details.formattedAddress}`
   }
   ```

9. **Implement clear autocomplete function:**

   ```typescript
   const clearAutocomplete = () => {
     selectedPlace.value = null
     autoFilledFromPlaces.value = false
     formData.googlePlaceId = null
     searchQuery.value = formData.name
     placeSuggestions.value = []
   }
   ```

10. **Update template - Replace name InputText with AutoComplete:**

    ```vue
    <div>
      <label for="name" class="mb-1 block text-sm font-medium text-gray-700">
        Nombre del Campamento *
        <span class="text-xs text-gray-500">(Empieza a escribir para buscar)</span>
      </label>

      <AutoComplete
        id="name"
        v-model="searchQuery"
        :suggestions="placeSuggestions"
        option-label="description"
        placeholder="Buscar ubicación..."
        class="w-full"
        :loading="placesLoading"
        @complete="debouncedSearch(searchQuery)"
        @item-select="handlePlaceSelected"
      >
        <template #item="{ item }">
          <div class="flex flex-col">
            <span class="font-semibold">{{ item.mainText }}</span>
            <span class="text-sm text-gray-500">{{ item.secondaryText }}</span>
          </div>
        </template>
      </AutoComplete>

      <small v-if="errors.name" class="text-red-500">{{ errors.name }}</small>

      <Button
        v-if="autoFilledFromPlaces"
        label="Escribir manualmente"
        icon="pi pi-pencil"
        text
        size="small"
        class="mt-1"
        @click="clearAutocomplete"
      />
    </div>
    ```

11. **Add auto-fill indicator message:**

    ```vue
    <Message
      v-if="autoFilledFromPlaces"
      severity="info"
      :closable="false"
      class="mt-2"
    >
      <i class="pi pi-check-circle mr-2"></i>
      Datos cargados desde Google Places. Puedes ajustarlos antes de guardar.
    </Message>
    ```

12. **Add error message display:**

    ```vue
    <Message v-if="placesError" severity="error" :closable="true">
      {{ placesError }}
    </Message>
    ```

13. **Add auto-filled indicators to location/coordinates fields:**

    ```vue
    <label for="location" class="mb-1 block text-sm font-medium text-gray-700">
      Ubicación
      <span v-if="autoFilledFromPlaces" class="text-xs text-blue-600">(Auto-completado)</span>
    </label>
    ```

**Dependencies:**

- `@vueuse/core` (useDebounceFn) - install if not present: `npm install @vueuse/core`
- `primevue` (AutoComplete, Message components)

**Implementation Notes:**

- Debounce delay of 300ms balances responsiveness and API call reduction
- All auto-filled fields remain editable for manual override
- "Write manually" button provides clear escape hatch
- Auto-generated description is helpful but can be overridden
- Loading state shown in AutoComplete component
- Error messages are user-friendly and in Spanish

### Step 4: Write Unit Tests for useGooglePlaces Composable

**File:** `frontend/src/composables/__tests__/useGooglePlaces.test.ts`

**Action:** Create comprehensive unit tests for the composable

**Implementation Steps:**

1. Create test file with Vitest imports:

   ```typescript
   import { describe, it, expect, vi, beforeEach } from 'vitest'
   import { useGooglePlaces } from '@/composables/useGooglePlaces'
   import { api } from '@/utils/api'

   vi.mock('@/utils/api')
   ```

2. Write test: "should search places successfully"
   - Mock `api.post` to return successful response with mock places
   - Call `searchPlaces('Camping')`
   - Assert result matches mock data
   - Assert loading is false, error is null
   - Assert `api.post` called with correct parameters

3. Write test: "should return empty array for input less than 3 characters"
   - Call `searchPlaces('Ca')`
   - Assert result is empty array
   - Assert `api.post` was not called

4. Write test: "should get place details successfully"
   - Mock `api.post` to return place details
   - Call `getPlaceDetails('ChIJ1')`
   - Assert result matches mock details

5. Write test: "should set error when API call fails"
   - Mock `api.post` to reject with error
   - Call `searchPlaces('Camping')`
   - Assert result is empty array
   - Assert error.value contains error message

6. Add beforeEach hook to clear mocks

**Dependencies:**

- `vitest` (testing framework)
- `@vue/test-utils` (Vue testing utilities)

**Implementation Notes:**

- Aim for 90%+ code coverage
- Test both success and error scenarios
- Mock all external dependencies (API calls)

### Step 5: Update Component Tests

**File:** `frontend/src/components/camps/__tests__/CampLocationForm.test.ts`

**Action:** Update existing component tests to cover new autocomplete functionality

**Implementation Steps:**

1. Add mock for `useGooglePlaces` composable:

   ```typescript
   import { useGooglePlaces } from '@/composables/useGooglePlaces'
   vi.mock('@/composables/useGooglePlaces')
   ```

2. Update existing test setup to provide mocked composable:

   ```typescript
   beforeEach(() => {
     vi.mocked(useGooglePlaces).mockReturnValue({
       loading: { value: false },
       error: { value: null },
       searchPlaces: vi.fn(),
       getPlaceDetails: vi.fn()
     })
   })
   ```

3. Write test: "should render autocomplete field for name"
   - Mount component
   - Assert AutoComplete component exists

4. Write test: "should auto-fill fields when place is selected"
   - Mock `getPlaceDetails` to return mock place data
   - Call `handlePlaceSelected` with mock place
   - Assert formData.name, location, latitude, longitude, googlePlaceId updated
   - Assert autoFilledFromPlaces is true

5. Write test: "should show auto-fill indicator message"
   - Set autoFilledFromPlaces to true
   - Mount component
   - Assert info message is visible

6. Write test: "should clear autocomplete when manual entry button clicked"
   - Set autoFilledFromPlaces to true
   - Click "Escribir manualmente" button
   - Assert autoFilledFromPlaces is false
   - Assert googlePlaceId is null

7. Update existing validation tests if needed

**Dependencies:**

- `vitest`
- `@vue/test-utils`

**Implementation Notes:**

- Test component behavior, not implementation details
- Focus on user interactions and expected outcomes

### Step 6: Write E2E Tests with Cypress

**File:** `frontend/cypress/e2e/camps/google-places-autocomplete.cy.ts`

**Action:** Create end-to-end tests for the autocomplete feature

**Implementation Steps:**

1. Create test file with authentication setup:

   ```typescript
   describe('Google Places Autocomplete for Camps', () => {
     beforeEach(() => {
       cy.login('admin@abuvi.org', 'Admin123!@#')
       cy.visit('/camps/locations')
     })
   })
   ```

2. Write test: "should autocomplete camp data when selecting from Google Places"
   - Mock `POST /api/places/autocomplete` endpoint with sample data
   - Mock `POST /api/places/details` endpoint with sample data
   - Click "New Camp" button
   - Type in name field
   - Wait for autocomplete request
   - Click first suggestion
   - Wait for details request
   - Assert all fields are auto-filled correctly
   - Assert auto-fill indicator is visible

3. Write test: "should allow manual entry after clearing autocomplete"
   - Type in name field and select suggestion
   - Click "Escribir manualmente" button
   - Clear and type custom values
   - Assert custom values are preserved
   - Assert auto-fill indicator is not visible

4. Write test: "should handle Google Places API errors gracefully"
   - Mock API error (503 status)
   - Type in name field
   - Wait for error
   - Assert error message is displayed
   - Verify manual entry still works

5. Write test: "should not search with less than 3 characters"
   - Type only 2 characters in name field
   - Assert autocomplete dropdown does not appear
   - Assert no API call was made

**Dependencies:**

- `cypress` (E2E testing framework)

**Implementation Notes:**

- Use `cy.intercept()` to mock API responses
- Test critical user flows only
- Include happy path and error scenarios
- Use data-testid attributes for reliable selectors

### Step 7: Update Technical Documentation

**Action:** Review and update technical documentation according to changes made

**Implementation Steps:**

1. **Review Changes:**
   - New composable: `useGooglePlaces`
   - Updated types: `camp.ts` with `googlePlaceId`
   - Updated component: `CampLocationForm.vue` with autocomplete
   - New dependency: `@vueuse/core`

2. **Identify Documentation Files:**
   - `ai-specs/specs/frontend-standards.mdc` - Component patterns, composable patterns
   - Check if routing documentation needs updates (no changes in this case)

3. **Update `ai-specs/specs/frontend-standards.mdc`:**
   - Add example of debounced autocomplete pattern using VueUse
   - Document composable pattern for external API integration
   - Add notes about PrimeVue AutoComplete component usage
   - Document error handling pattern for external services

4. **Verify Documentation:**
   - Ensure all changes are accurately reflected
   - Check documentation follows established structure
   - Confirm all content is in English

5. **Report Updates:**
   - Document which files were updated
   - List specific sections added/modified

**References:**

- Follow process in `ai-specs/specs/documentation-standards.mdc`
- All documentation must be in English

**Notes:** This is MANDATORY before marking implementation complete

## Implementation Order

1. Step 0: Create Feature Branch
2. Step 1: Update TypeScript Types
3. Step 2: Create Google Places Composable
4. Step 3: Update CampLocationForm Component
5. Step 4: Write Unit Tests for Composable
6. Step 5: Update Component Tests
7. Step 6: Write E2E Tests with Cypress
8. Step 7: Update Technical Documentation

**Total Estimated Time:** 2-3 days

## Testing Checklist

### Unit Tests (Vitest)

- [ ] `useGooglePlaces.searchPlaces()` returns places for valid input
- [ ] `useGooglePlaces.searchPlaces()` returns empty for input < 3 chars
- [ ] `useGooglePlaces.searchPlaces()` handles API errors
- [ ] `useGooglePlaces.getPlaceDetails()` returns details for valid placeId
- [ ] `useGooglePlaces.getPlaceDetails()` handles API errors
- [ ] Composable sets loading state correctly
- [ ] Composable sets error state correctly

### Component Tests (Vitest + @vue/test-utils)

- [ ] CampLocationForm renders AutoComplete component
- [ ] Selecting place auto-fills all fields
- [ ] Auto-fill indicator message appears when fields are populated
- [ ] "Write manually" button clears autocomplete state
- [ ] Form validation still works with new field
- [ ] Error messages display for API failures

### E2E Tests (Cypress)

- [ ] User can search and select place from autocomplete
- [ ] All fields auto-populate correctly on selection
- [ ] User can clear and enter data manually
- [ ] API errors display user-friendly messages
- [ ] Form submission works with auto-filled data
- [ ] Form submission works with manual data

### Manual Testing Checklist

- [ ] Autocomplete appears after typing 3+ characters
- [ ] Autocomplete shows loading spinner during search
- [ ] Selecting a place fills all fields instantly
- [ ] Generated description is meaningful
- [ ] Auto-fill indicator is visible and clear
- [ ] All auto-filled fields are editable
- [ ] "Write manually" button clears autocomplete state
- [ ] Error messages are in Spanish and user-friendly
- [ ] Form can be submitted successfully
- [ ] Manual entry works without using autocomplete

## Error Handling Patterns

### Composable Error Handling

```typescript
// In useGooglePlaces.ts
try {
  const response = await api.post('/places/autocomplete', { input })
  if (response.data.success && response.data.data) {
    return response.data.data
  }
  error.value = response.data.error?.message || 'Error al buscar lugares'
  return []
} catch (err: any) {
  error.value = err.response?.data?.error?.message || 'Error al buscar lugares'
  return []
} finally {
  loading.value = false
}
```

### Component Error Display

```vue
<!-- User-friendly error messages -->
<Message v-if="placesError" severity="error" :closable="true">
  {{ placesError }}
</Message>

<!-- Fallback to manual entry always available -->
<Button
  v-if="autoFilledFromPlaces"
  label="Escribir manualmente"
  @click="clearAutocomplete"
/>
```

### Error Recovery

- **API Unavailable:** Show error message, allow manual entry
- **Invalid Place ID:** Show "Place not found" message, keep form editable
- **Network Error:** Show retry option, fall back to manual entry
- **Validation Error:** Show field-level error messages in Spanish

## UI/UX Considerations

### PrimeVue Components

- **AutoComplete:** Main search interface with custom item template
- **Message:** Display auto-fill indicator and error messages
- **Button:** "Write manually" action button with icon
- **InputText/InputNumber:** Retain for other fields

### Tailwind CSS

- Responsive layout: Form already responsive, no changes needed
- Spacing: `gap-4` for form fields, `mt-2` for messages
- Colors: `text-blue-600` for auto-fill indicators, `text-red-500` for errors
- Typography: `text-sm`, `text-xs` for labels and hints

### Loading States

- AutoComplete `:loading` prop shows spinner during search
- Disable submit button while places are loading: `:disabled="placesLoading"`

### Accessibility

- AutoComplete has proper ARIA labels
- Error messages associated with fields
- Keyboard navigation works (native in PrimeVue AutoComplete)
- Screen reader announces auto-fill changes

### User Feedback

- Visual indicator when fields are auto-filled
- Clear button to revert to manual entry
- Informative placeholder text
- Helper text for minimum character requirement
- Success-style message for auto-fill confirmation

## Dependencies

### npm Packages

- `@vueuse/core` - For `useDebounceFn` utility
  - Install: `npm install @vueuse/core`
  - Version: Latest compatible with Vue 3

### PrimeVue Components

- `AutoComplete` - Already in project
- `Message` - Already in project
- `Button` - Already in project

### Existing Project Dependencies

- `primevue` - UI component library
- `vue` - Framework
- `axios` - HTTP client (via api utility)

### Development Dependencies

- `vitest` - Unit testing
- `@vue/test-utils` - Component testing
- `cypress` - E2E testing

**Third-party Packages Justification:**

- `@vueuse/core`: Industry-standard Vue utilities library, provides optimized debouncing

## Notes

### Important Reminders

- All text must be in **Spanish** for user-facing elements (labels, messages, placeholders)
- All code comments and documentation must be in **English**
- TypeScript strict mode enabled - no `any` types allowed
- Use `<script setup lang="ts">` syntax for all components
- Follow existing validation patterns (frontend + backend validation)

### Business Rules

- Autocomplete is **optional** - users can skip it and enter data manually
- All auto-filled fields remain **editable** - users can override any value
- `googlePlaceId` is **nullable** - existing camps won't have it
- Minimum **3 characters** required before autocomplete search
- **300ms debounce** to reduce API calls
- API requires **authentication** - only logged-in users can use autocomplete

### Language Requirements

- User interface: **Spanish** (es-ES)
- Error messages: **Spanish**
- Code/comments: **English**
- Documentation: **English**

### Performance Considerations

- Debounce autocomplete to 300ms
- Minimum 3 characters before search
- Loading indicators for better perceived performance
- Consider caching suggestions in future (not in this phase)

### Future Enhancements (Not in Scope)

- Photo import from Google Places
- Map preview with marker
- Nearby campground suggestions
- Review data from Google Places

## Next Steps After Implementation

### Post-Implementation Tasks

1. **Code Review:** Request review from team members
2. **Integration Testing:** Test with actual Google Places API key
3. **User Acceptance Testing:** Get feedback from actual users
4. **Performance Monitoring:** Monitor API usage and costs
5. **Bug Fixes:** Address any issues found during testing

### Deployment Preparation

1. Ensure backend endpoints are deployed and configured
2. Verify Google API key is configured in backend
3. Update environment variables if needed (frontend feature flags)
4. Smoke test in staging environment
5. Monitor error logs after deployment

### Documentation Updates (Already Covered in Step 7)

- Technical documentation updated
- API integration patterns documented
- Component usage examples added

## Implementation Verification

### Final Verification Checklist

#### Code Quality

- [ ] TypeScript strict typing enforced (no `any` types)
- [ ] All components use `<script setup lang="ts">` syntax
- [ ] Composables follow project naming convention (`use*`)
- [ ] Error handling implemented for all API calls
- [ ] Loading states implemented for all async operations

#### Functionality

- [ ] AutoComplete renders correctly
- [ ] Autocomplete search works with debouncing
- [ ] Selecting place auto-fills all fields
- [ ] Auto-generated description is meaningful
- [ ] Manual override works ("Write manually" button)
- [ ] All fields remain editable after auto-fill
- [ ] Form validation works with new field
- [ ] Form submission includes `googlePlaceId`

#### Testing

- [ ] Vitest unit tests pass with 90%+ coverage
- [ ] Component tests pass
- [ ] Cypress E2E tests pass
- [ ] Manual testing checklist completed
- [ ] No console errors in browser

#### Integration

- [ ] Composable connects to backend endpoints correctly
- [ ] Authentication header included in API calls
- [ ] Error responses handled gracefully
- [ ] Success responses parsed correctly

#### Documentation

- [ ] Technical documentation updated (frontend-standards.mdc)
- [ ] Code comments added where needed (in English)
- [ ] README updated if new dependencies added
- [ ] All documentation in English

#### UX/Accessibility

- [ ] User-facing text in Spanish
- [ ] Error messages user-friendly
- [ ] Loading indicators visible
- [ ] Keyboard navigation works
- [ ] Screen reader compatible

### Success Criteria

✅ All tests pass (unit, component, E2E)
✅ Code review approved
✅ Documentation updated
✅ Manual testing checklist complete
✅ No TypeScript errors
✅ Feature works in local development environment
✅ Backend integration verified

### Ready for Code Review When

- All steps completed
- All tests passing
- Documentation updated
- Manual testing completed
- Branch pushed to GitHub
- PR created with description

---

**Estimated Total Effort:** 2-3 development days

**Dependencies:**

- Backend endpoints deployed and accessible
- Google Places API key configured in backend
- `@vueuse/core` installed

**Risk Mitigation:**

- Fallback to manual entry always available
- Error handling for all API failures
- Graceful degradation if backend unavailable
