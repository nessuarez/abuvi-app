# Frontend Implementation Plan: feat-onboarding-tools — In-App Onboarding & Guided Tours

## Overview

Implement an in-app onboarding system using **Driver.js** wrapped in a Vue 3 composable (`useOnboarding`), providing interactive step-by-step walkthroughs for new users. The system follows the project's composable-based architecture (Vue 3 Composition API, PrimeVue + Tailwind CSS) and includes a reusable help button, tour progress persistence, media support, and role-based tour triggering.

---

## Architecture Context

### Components & Composables Involved

| Layer | File | Purpose |
|---|---|---|
| Composable | `frontend/src/composables/useOnboarding.ts` | Core Driver.js wrapper: tour registry, progress tracking, auto-trigger |
| Types | `frontend/src/types/onboarding.ts` | TypeScript interfaces for tours, steps, media |
| Tour Registry | `frontend/src/onboarding/index.ts` | Registers all tours, exports lookup helpers |
| Tour Definitions | `frontend/src/onboarding/tours/*.tour.ts` | Individual tour step configurations |
| UI Component | `frontend/src/components/ui/OnboardingButton.vue` | Floating help button with tour menu |
| Layout | `frontend/src/layouts/AuthenticatedLayout.vue` | Mount OnboardingButton globally |
| Entry | `frontend/src/main.ts` | Import Driver.js CSS |

### State Management Approach

- **No Pinia store needed** — tour state is local (per-composable instance) and progress is persisted in `localStorage`.
- Key: `abuvi:onboarding:completed` → `string[]` (array of completed tour IDs).
- Future migration to backend API is trivial (swap localStorage calls for API calls in the composable).

### Routing Considerations

- No new routes required.
- Tours are triggered on existing pages via `onMounted()` in views or automatically by the composable using the current route path.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch.
- **Branch Naming**: `feature/feat-onboarding-tools-frontend`
- **Implementation Steps**:
  1. Ensure you're on the latest `main` branch: `git checkout main && git pull origin main`
  2. Create new branch: `git checkout -b feature/feat-onboarding-tools-frontend`
  3. Verify: `git branch`
- **Notes**: Must be the FIRST step before any code changes.

---

### Step 1: Install Driver.js

- **File**: `frontend/package.json`
- **Action**: Add `driver.js` as a production dependency.
- **Implementation Steps**:
  1. Run: `cd frontend && npm install driver.js`
  2. Verify it appears in `package.json` dependencies.
  3. Verify `package-lock.json` is updated.
- **Dependencies**: None (Driver.js has zero dependencies).
- **Implementation Notes**:
  - Driver.js is ~5 KB gzipped, MIT license, zero dependencies, native TypeScript support.
  - Current version: `1.3.x` — check latest on npm.

---

### Step 2: Import Driver.js CSS Globally

- **File**: `frontend/src/main.ts`
- **Action**: Add the Driver.js stylesheet import so the overlay and popover styles are available app-wide.
- **Implementation Steps**:
  1. Add `import 'driver.js/dist/driver.css'` near the top of `main.ts`, alongside other global style imports.
- **Implementation Notes**:
  - The CSS must be loaded globally because tours can be triggered from any page.
  - The stylesheet is small (~2 KB) and does not conflict with Tailwind or PrimeVue.

---

### Step 3: Define TypeScript Interfaces

- **File**: `frontend/src/types/onboarding.ts`
- **Action**: Create TypeScript interfaces for the onboarding system.
- **Implementation Steps**:
  1. Create the file `frontend/src/types/onboarding.ts`.
  2. Define the following interfaces:

```typescript
/** Media attachment for a tour step */
export interface OnboardingStepMedia {
  type: 'video' | 'audio' | 'image'
  src: string
  alt?: string
}

/** Single step in an onboarding tour */
export interface OnboardingStep {
  /** CSS selector for the target element */
  element: string
  /** Step title */
  title: string
  /** Step description (supports HTML) */
  description: string
  /** Popover position */
  side?: 'top' | 'bottom' | 'left' | 'right'
  /** Optional media to embed */
  media?: OnboardingStepMedia
  /** Callback when user advances */
  onNext?: () => void
  /** Callback when user goes back */
  onPrevious?: () => void
}

/** Tour definition */
export interface OnboardingTour {
  /** Unique tour identifier */
  id: string
  /** Human-readable tour name (for help menu) */
  name: string
  /** Optional description shown in help menu */
  description?: string
  /** Route path(s) where this tour is relevant */
  routes: string[]
  /** Whether this tour requires board role */
  requiresBoard?: boolean
  /** Tour steps */
  steps: OnboardingStep[]
}

/** Return type of useOnboarding composable */
export interface UseOnboardingReturn {
  /** Start a tour by its ID */
  startTour: (tourId: string) => void
  /** Check if a tour has been completed */
  hasCompletedTour: (tourId: string) => boolean
  /** Reset a specific tour's completion status */
  resetTour: (tourId: string) => void
  /** Reset all tour completion statuses */
  resetAllTours: () => void
  /** Get tours available for the current route */
  getAvailableTours: () => OnboardingTour[]
  /** Auto-trigger uncompleted tours for the current route */
  autoTrigger: () => void
  /** Whether a tour is currently active */
  isActive: Ref<boolean>
}
```

- **Implementation Notes**:
  - Follow existing type conventions in `frontend/src/types/` (PascalCase interfaces, explicit field docs).
  - Import `Ref` from `vue` for the `isActive` type.

---

### Step 4: Create `useOnboarding` Composable

- **File**: `frontend/src/composables/useOnboarding.ts`
- **Action**: Implement the core onboarding composable wrapping Driver.js.
- **Implementation Steps**:
  1. Create the file.
  2. Import `driver` from `driver.js` and types from `@/types/onboarding`.
  3. Implement a module-level tour registry (`Map<string, OnboardingTour>`).
  4. Implement `registerTour(tour: OnboardingTour)` to add tours to the registry.
  5. Implement the `useOnboarding()` composable function returning:
     - `startTour(tourId: string)` — looks up tour in registry, maps `OnboardingStep[]` to Driver.js `DriveStep[]`, creates a `driver()` instance and calls `.drive()`. Marks tour as completed on finish/destroy. Handles media rendering by injecting HTML into the `description` field.
     - `hasCompletedTour(tourId: string)` — reads from localStorage.
     - `resetTour(tourId: string)` — removes tour ID from localStorage array.
     - `resetAllTours()` — clears the localStorage key.
     - `getAvailableTours()` — filters registry by current route (use `useRoute()` from vue-router) and user role (use `useAuthStore()` to check board role).
     - `autoTrigger()` — calls `getAvailableTours()`, finds the first uncompleted tour, and starts it.
     - `isActive` — reactive `ref<boolean>` toggled by Driver.js `onDestroyStarted` / `onDestroyed` callbacks.
  6. Implement a private helper `buildMediaHtml(media: OnboardingStepMedia): string` that returns:
     - For `image`: `<img src="..." alt="..." class="max-w-full rounded mt-2" />`
     - For `video`: `<video src="..." controls class="max-w-full rounded mt-2"></video>`
     - For `audio`: `<audio src="..." controls class="mt-2"></audio>`
  7. Implement a private helper `mapStepsToDriverSteps(steps: OnboardingStep[]): DriveStep[]` that converts our step format to Driver.js format, appending media HTML to the description when present.

- **Dependencies**:
  - `driver.js` (npm package)
  - `vue` (`ref`, `Ref`)
  - `vue-router` (`useRoute`)
  - `@/stores/auth` (`useAuthStore`)
  - `@/types/onboarding`

- **Implementation Notes**:
  - Follow the existing composable patterns: consistent error handling, reactive state.
  - The `registerTour` function is called at module scope by the tour registry (Step 5), not inside the composable.
  - localStorage key: `abuvi:onboarding:completed`.
  - Driver.js `driver()` config should include: `showProgress: true`, `showButtons: ['next', 'previous', 'close']`, `animate: true`, `allowClose: true`, `overlayColor: 'rgba(0, 0, 0, 0.5)'`.
  - Steps that target elements not present in the DOM should be filtered out before starting (check `document.querySelector(step.element)` exists).
  - The `onDestroyed` callback should mark the tour as completed.
  - The `onNext` and `onPrevious` callbacks from our step definition should be wired into Driver.js `onNextClick` / `onPrevClick` hooks.

---

### Step 5: Create Tour Registry & Tour Definitions

#### 5a: Tour Registry

- **File**: `frontend/src/onboarding/index.ts`
- **Action**: Central registry that imports and registers all tour definitions.
- **Implementation Steps**:
  1. Create directory `frontend/src/onboarding/tours/`.
  2. Create `frontend/src/onboarding/index.ts`.
  3. Import `registerTour` from the composable.
  4. Import each tour definition and call `registerTour(tour)` for each.
  5. Export nothing — side-effect-only module that registers tours.
  6. This file should be imported in `main.ts` to ensure tours are registered at app startup.

#### 5b: Welcome Tour

- **File**: `frontend/src/onboarding/tours/welcome.tour.ts`
- **Action**: Define the welcome/dashboard tour for all users.
- **Implementation Steps**:
  1. Export a `const welcomeTour: OnboardingTour` with:
     - `id: 'welcome'`
     - `name: 'Welcome Tour'`
     - `description: 'Get to know the ABUVI platform'`
     - `routes: ['/home']`
     - `requiresBoard: false`
  2. Define steps targeting elements on the HomePage:
     - Step 1: Target the main heading/container → "Welcome to ABUVI"
     - Step 2: Target QuickAccessCards section → "Quick access to key features"
     - Step 3: Target the navigation header → "Navigate using the menu bar"
     - Step 4: Target the user menu → "Access your profile and settings"
  3. Add `data-onboarding="..."` attributes to the target elements in `HomePage.vue` and `AppHeader.vue`.

- **Implementation Notes**:
  - Use `data-onboarding` attributes as CSS selectors (e.g., `[data-onboarding="quick-access-cards"]`).
  - This is more resilient than class-based selectors which may change with Tailwind updates.
  - Keep step descriptions concise (1-2 sentences).

#### 5c: Registration Flow Tour

- **File**: `frontend/src/onboarding/tours/registration.tour.ts`
- **Action**: Define the registration wizard tour.
- **Implementation Steps**:
  1. Export `const registrationTour: OnboardingTour` with:
     - `id: 'registration-flow'`
     - `routes: ['/registrations/new']` (or the path used for `RegisterForCampPage`)
  2. Define steps targeting the Stepper wizard steps in `RegisterForCampPage.vue`.
  3. Add `data-onboarding` attributes to the stepper steps and key form elements.

#### 5d: Membership Tour

- **File**: `frontend/src/onboarding/tours/membership.tour.ts`
- **Action**: Define the membership management tour.
- **Implementation Steps**:
  1. Export `const membershipTour: OnboardingTour` with appropriate route(s).
  2. Target membership-related elements on the relevant page.

#### 5e: Camp Management Tour (Board Only)

- **File**: `frontend/src/onboarding/tours/camp-management.tour.ts`
- **Action**: Define the board-only camp management tour.
- **Implementation Steps**:
  1. Export `const campManagementTour: OnboardingTour` with:
     - `id: 'camp-management'`
     - `routes: ['/camps/locations', '/camps/editions']`
     - `requiresBoard: true`
  2. Target camp management elements on the relevant pages.

---

### Step 6: Create OnboardingButton Component

- **File**: `frontend/src/components/ui/OnboardingButton.vue`
- **Action**: Create a floating help button that lists available tours for the current page.
- **Implementation Steps**:
  1. Create the component using `<script setup lang="ts">`.
  2. Use `useOnboarding()` composable to get `getAvailableTours()` and `startTour()`.
  3. Use PrimeVue `SpeedDial` or a custom floating button with `Menu` component:
     - Floating button positioned at bottom-right: `fixed bottom-6 right-6 z-40`
     - Uses PrimeVue `Button` with `pi pi-question-circle` icon, rounded.
     - On click, opens a PrimeVue `Menu` with tour items.
     - Each menu item calls `startTour(tour.id)`.
     - If no tours available for the current route, button is hidden (use `v-if`).
  4. Include a "Reset all tours" option at the bottom of the menu as a secondary action.

- **Dependencies**:
  - PrimeVue: `Button`, `Menu`
  - `@/composables/useOnboarding`

- **Implementation Notes**:
  - Use Tailwind for positioning (`fixed bottom-6 right-6 z-40`).
  - z-index should be below the header (`z-50`) but above page content.
  - The button should NOT appear on public routes (only inside AuthenticatedLayout).
  - Use `watch` on `route.path` to reactively update available tours.

---

### Step 7: Integrate into Authenticated Layout

- **File**: `frontend/src/layouts/AuthenticatedLayout.vue`
- **Action**: Add the OnboardingButton component to the layout so it appears on all authenticated pages.
- **Implementation Steps**:
  1. Import `OnboardingButton` from `@/components/ui/OnboardingButton.vue`.
  2. Add `<OnboardingButton />` inside the template, after the `<router-view />`:

```vue
<template>
  <div class="flex min-h-screen flex-col">
    <AppHeader />
    <main class="flex-1 bg-white">
      <router-view />
    </main>
    <AppFooter />
    <OnboardingButton />
  </div>
</template>
```

---

### Step 8: Add `data-onboarding` Attributes to Existing Pages

- **Action**: Add `data-onboarding` HTML attributes to key elements on existing pages so Driver.js can target them.
- **Files to modify**:

| File | Attributes to Add |
|---|---|
| `frontend/src/views/HomePage.vue` | `data-onboarding="welcome-heading"`, `data-onboarding="quick-access-cards"` |
| `frontend/src/components/home/QuickAccessCards.vue` | `data-onboarding="quick-access-cards"` (on root element) |
| `frontend/src/components/layout/AppHeader.vue` | `data-onboarding="main-nav"`, `data-onboarding="user-menu"` |
| `frontend/src/components/layout/UserMenu.vue` | `data-onboarding="user-menu-trigger"` |
| `frontend/src/views/registrations/RegisterForCampPage.vue` | `data-onboarding="registration-stepper"`, attributes on each step |
| `frontend/src/views/camps/CampLocationsPage.vue` | `data-onboarding="camp-locations-table"` (board tour) |
| `frontend/src/views/camps/CampEditionsPage.vue` | `data-onboarding="camp-editions-table"` (board tour) |

- **Implementation Notes**:
  - Use descriptive attribute values that match the tour step selectors.
  - Ensure attributes don't interfere with existing `data-testid` attributes (they're different namespaces).
  - Only add attributes for elements that are targeted by a defined tour step.

---

### Step 9: Wire Auto-Trigger in Pages

- **Action**: Call `autoTrigger()` from the composable in key pages' `onMounted()` hooks.
- **Files to modify**:
  - `frontend/src/views/HomePage.vue` — auto-trigger welcome tour
- **Implementation Steps**:
  1. In `HomePage.vue`, import `useOnboarding`.
  2. In `onMounted()`, call `autoTrigger()`:

```typescript
const { autoTrigger } = useOnboarding()

onMounted(() => {
  // Small delay to ensure DOM elements are rendered
  nextTick(() => {
    autoTrigger()
  })
})
```

- **Implementation Notes**:
  - Use `nextTick()` to ensure target elements are in the DOM before the tour starts.
  - Only add auto-trigger to the welcome tour's page initially. Other tours can be auto-triggered in Phase 2.
  - Auto-trigger checks `hasCompletedTour()` internally, so it won't re-show completed tours.

---

### Step 10: Add Media Support

- **Action**: Create the `frontend/public/onboarding/` directory for media assets and ensure the composable handles media rendering.
- **Implementation Steps**:
  1. Create directory: `frontend/public/onboarding/`
  2. Add a placeholder `README.md` or `.gitkeep` so the directory is tracked.
  3. The media rendering logic is already built into the composable (Step 4, `buildMediaHtml` helper).
  4. Tour definitions can reference media like: `media: { type: 'image', src: '/onboarding/welcome-dashboard.png' }`.
- **Implementation Notes**:
  - Media files in `public/` are served at the root path.
  - Videos should be kept under 15-30 seconds, compressed for web.
  - Actual media asset creation is a content task, not a code task. The code infrastructure supports it.

---

### Step 11: Write Vitest Unit Tests

- **File**: `frontend/src/composables/__tests__/useOnboarding.spec.ts`
- **Action**: Write unit tests for the `useOnboarding` composable.
- **Implementation Steps**:
  1. Create the test file.
  2. Mock `driver.js` module.
  3. Mock `localStorage` (use `vi.stubGlobal` or jsdom's built-in).
  4. Mock `vue-router` (`useRoute` returning a configurable path).
  5. Mock `useAuthStore` to control role checks.
  6. Write tests for:
     - `registerTour()` — tour is added to registry
     - `startTour()` — calls Driver.js `drive()` with correct steps
     - `startTour()` with invalid ID — does nothing / logs warning
     - `hasCompletedTour()` — returns false initially, true after completion
     - `resetTour()` — removes tour from completed list
     - `resetAllTours()` — clears all completion data
     - `getAvailableTours()` — filters by current route
     - `getAvailableTours()` — filters board-only tours for non-board users
     - `autoTrigger()` — starts first uncompleted tour
     - `autoTrigger()` — does nothing when all tours completed
     - Media HTML generation — correct `<img>`, `<video>`, `<audio>` tags
     - Steps with missing DOM elements are filtered out

---

### Step 12: Write Component Tests for OnboardingButton

- **File**: `frontend/src/components/__tests__/OnboardingButton.spec.ts`
- **Action**: Write component tests for the OnboardingButton.
- **Implementation Steps**:
  1. Create the test file.
  2. Mock `useOnboarding` composable.
  3. Write tests for:
     - Renders when tours are available for current route
     - Hidden when no tours available
     - Clicking a tour item calls `startTour()` with correct ID
     - "Reset all tours" option calls `resetAllTours()`
     - Displays tour names and descriptions in menu

---

### Step 13: Write Cypress E2E Test

- **File**: `frontend/cypress/e2e/onboarding.cy.ts`
- **Action**: Write an E2E test for the welcome tour flow.
- **Implementation Steps**:
  1. Create the test file.
  2. Clear localStorage before test to ensure fresh state.
  3. Login and navigate to `/home`.
  4. Verify the tour auto-starts (Driver.js overlay is visible).
  5. Click through each step (Next button).
  6. Verify tour completion (overlay disappears).
  7. Reload page — verify tour does NOT auto-start again.
  8. Click the OnboardingButton → select Welcome Tour → verify it restarts.
  9. Verify the help button shows available tours.

---

### Step 14: Update Technical Documentation

- **Action**: Review and update technical documentation to reflect the new onboarding system.
- **Implementation Steps**:
  1. **Update `ai-specs/specs/frontend-standards.mdc`**:
     - Add a new section "Onboarding System" documenting:
       - Driver.js integration
       - `useOnboarding` composable API
       - Tour definition file conventions
       - `data-onboarding` attribute convention
  2. **Update dependency documentation**:
     - Document `driver.js` as a new production dependency with version and purpose.
  3. **Verify documentation accuracy** against implemented code.
- **References**: Follow `ai-specs/specs/documentation-standards.mdc`.
- **Notes**: This step is MANDATORY before considering implementation complete.

---

## Implementation Order

1. **Step 0**: Create feature branch `feature/feat-onboarding-tools-frontend`
2. **Step 1**: Install Driver.js
3. **Step 2**: Import Driver.js CSS in `main.ts`
4. **Step 3**: Define TypeScript interfaces in `types/onboarding.ts`
5. **Step 4**: Create `useOnboarding` composable
6. **Step 5**: Create tour registry and tour definitions (welcome tour first)
7. **Step 6**: Create `OnboardingButton.vue` component
8. **Step 7**: Integrate OnboardingButton into `AuthenticatedLayout.vue`
9. **Step 8**: Add `data-onboarding` attributes to existing pages
10. **Step 9**: Wire auto-trigger in `HomePage.vue`
11. **Step 10**: Add media support infrastructure
12. **Step 11**: Write Vitest unit tests
13. **Step 12**: Write component tests for OnboardingButton
14. **Step 13**: Write Cypress E2E test
15. **Step 14**: Update technical documentation

---

## Testing Checklist

- [ ] Vitest: `useOnboarding` composable — tour registration, progress tracking, auto-trigger, role filtering, media HTML
- [ ] Vitest: `OnboardingButton.vue` — rendering, tour list, click handlers
- [ ] Cypress E2E: Welcome tour auto-starts on first visit, completes, persists, can be restarted
- [ ] Manual: Tour overlay renders correctly on desktop and mobile
- [ ] Manual: Tour steps skip missing elements gracefully
- [ ] Manual: Board-only tours don't appear for regular members
- [ ] Manual: OnboardingButton shows correct tours per page
- [ ] Manual: Media (images) renders correctly in tour steps

---

## Error Handling Patterns

- **Tour start with missing elements**: Filter out steps targeting non-existent DOM elements before starting. If all steps are filtered, don't start the tour.
- **Driver.js initialization failure**: Wrap `driver()` creation in try-catch. Log error to console. Don't crash the app.
- **localStorage unavailable**: Catch `SecurityError` from localStorage access (private browsing). Fall back to in-memory tracking for the session.
- **Media load failure**: Use `onerror` attributes in HTML to hide broken media. Text content remains visible as fallback.

---

## UI/UX Considerations

- **PrimeVue Components**: `Button` (help trigger), `Menu` (tour list), `SpeedDial` (alternative)
- **Tailwind CSS**: `fixed bottom-6 right-6 z-40` for floating button, responsive positioning
- **Responsive Design**: Driver.js handles popover repositioning automatically. Steps targeting mobile-hidden elements are filtered via `document.querySelector()`.
- **Accessibility**: Driver.js provides built-in keyboard navigation (arrows, Escape). Popover content uses semantic HTML.
- **Loading States**: No loading states needed — tours are purely client-side.
- **Color & Contrast**: Driver.js overlay uses configurable opacity. Ensure popover text meets WCAG AA contrast.

---

## Dependencies

| Package | Version | Justification |
|---|---|---|
| `driver.js` | `^1.3.x` | Lightweight (~5 KB), MIT license, zero dependencies, TypeScript support, active maintenance |

**PrimeVue Components Used**:

- `Button` — help trigger
- `Menu` — tour selection dropdown

---

## Notes

- **English Only**: All tour titles, descriptions, and code must be in English.
- **TypeScript Strict**: No `any` types. All interfaces fully typed.
- **No `<style>` blocks**: Use Tailwind utility classes for all styling in OnboardingButton.
- **`data-onboarding` vs `data-testid`**: These are separate concerns. `data-onboarding` is for tour targeting, `data-testid` is for testing. Both can coexist on the same element.
- **Phase 1 Scope**: This plan covers Phase 1 (Driver.js + composable + welcome tour + OnboardingButton + tests). Phase 2 (registration + membership tours + media content) and Phase 3 (camp management tour + i18n) are follow-up tickets.
- **i18n Preparation**: Tour content strings are centralized in tour definition files. When Vue I18n is introduced, swap string literals for `t('onboarding.welcome.step1.title')` calls.

---

## Next Steps After Implementation

1. **Phase 2**: Implement registration flow tour, membership tour, and populate actual media assets.
2. **Phase 3**: Implement camp management tour (board only), family unit tour, and prepare i18n keys.
3. **Future**: Evaluate Product Fruits ($89/mo) if non-technical team members need to author tours without developer involvement.

---

## Implementation Verification

- [ ] **Code Quality**: All files use TypeScript strict mode, no `any`, `<script setup lang="ts">` in OnboardingButton
- [ ] **Functionality**: Welcome tour starts on first visit, completes, persists, restarts from help button
- [ ] **Testing**: Vitest unit tests pass, Cypress E2E test passes
- [ ] **Integration**: OnboardingButton visible in AuthenticatedLayout, tours target correct elements
- [ ] **Documentation**: Frontend standards updated with onboarding system documentation
- [ ] **Bundle Size**: Verify Driver.js adds < 10 KB to production build
