# User Story: In-App Onboarding & Guided Tours

## Summary

As a **new user** of the ABUVI platform, I want to be guided through the main application flows (registration, camp management, memberships, etc.) with interactive, step-by-step walkthroughs so that I can quickly understand how to use the tool without external documentation or training.

As a **product owner / board member**, I want to define and maintain onboarding flows easily, with optional audio/video support, so that users always have up-to-date guidance.

---

## Problem Statement

New users arriving at ABUVI have no in-app guidance to understand the available features and workflows. Key processes like camp registration (multi-step wizard), membership management, and family unit administration are complex enough that users may abandon tasks or make errors without guided onboarding.

Currently there is **no existing onboarding, tour, or walkthrough system** in the application.

---

## Tool Evaluation & Recommendation

### Recommended: Driver.js (Primary) + Custom Vue Composable

After evaluating 11+ tools (open-source and SaaS), **Driver.js** is the best fit for the ABUVI stack:

| Criteria | Driver.js |
|---|---|
| **Vue 3 compatibility** | Framework-agnostic, works via imperative API in composables |
| **License** | MIT (free for commercial use) |
| **Bundle size** | ~5 KB gzipped |
| **Dependencies** | Zero |
| **TypeScript** | Native support |
| **Maintenance** | Active (25K+ GitHub stars, 366K+ weekly npm downloads) |
| **Custom HTML in popovers** | Yes (allows embedding `<video>`, `<audio>`, PrimeVue components) |
| **Highlights & animations** | Yes (smooth cutout overlay on target elements) |
| **Keyboard navigation** | Yes |
| **Programmatic control** | Full (start, stop, next, previous, destroy) |

### Alternatives Considered

| Tool | Verdict | Reason to Reject |
|---|---|---|
| **Shepherd.js** | AGPL license | Requires commercial license for proprietary apps |
| **Intro.js** | AGPL license | Same licensing issue; Vue 3 wrapper less polished |
| **@globalhive/vuejs-tour** | Vue-native, MIT | Smaller community, fewer features than Driver.js |
| **Product Fruits** (SaaS) | Best no-code option | $89+/mo recurring cost; consider only if non-technical team needs to author tours |
| **Toonimo** | Best audio narration | $7,200+/year; enterprise-oriented, overkill for current needs |

### Future Consideration

If the team grows and non-technical members need to author tours without developer involvement, **Product Fruits** ($89/mo, Vue.js support, SOC 2 certified) could complement Driver.js as a no-code layer.

---

## Functional Requirements

### FR-1: Onboarding Composable (`useOnboarding`)

Create a reusable composable that wraps Driver.js and provides:

- **Tour definition**: Declarative step configuration (target element, title, description, position, media)
- **Tour registry**: Register multiple named tours (e.g., `welcome`, `registration`, `membership-management`)
- **Progress persistence**: Store completed tours per user in localStorage (or backend API later)
- **Auto-trigger**: Option to auto-start a tour on first visit to a page
- **Manual trigger**: Ability to restart any tour from a help menu or button

```typescript
// Example API design
const { startTour, hasCompletedTour, resetTour } = useOnboarding()

// Start a named tour
startTour('registration-flow')

// Check if user already completed it
if (!hasCompletedTour('welcome')) {
  startTour('welcome')
}
```

### FR-2: Tour Step Configuration

Each tour step should support:

| Field | Type | Required | Description |
|---|---|---|---|
| `element` | `string` | Yes | CSS selector for the target element |
| `title` | `string` | Yes | Step title |
| `description` | `string` | Yes | Step explanation text (supports HTML) |
| `side` | `'top' \| 'bottom' \| 'left' \| 'right'` | No | Popover position (default: auto) |
| `media` | `{ type: 'video' \| 'audio' \| 'image', src: string }` | No | Optional media to embed in the step |
| `onNext` | `() => void` | No | Callback when user advances |
| `onPrevious` | `() => void` | No | Callback when user goes back |

### FR-3: Tour Definition Files

Tours should be defined in dedicated configuration files for maintainability:

```
frontend/src/onboarding/
├── index.ts                    # Tour registry & exports
├── tours/
│   ├── welcome.tour.ts         # Welcome / dashboard tour
│   ├── registration.tour.ts    # Registration wizard tour
│   ├── membership.tour.ts      # Membership management tour
│   ├── camp-management.tour.ts # Camp management tour (board)
│   └── family-unit.tour.ts     # Family unit management tour
└── components/
    └── OnboardingButton.vue    # "Help" / "Take a tour" trigger button
```

### FR-4: Initial Tours to Implement

#### 4.1 Welcome Tour (all users)
- Dashboard overview
- Navigation sidebar explanation
- Profile / account settings
- How to access help

#### 4.2 Registration Flow Tour (all users)
- How to start a camp registration
- Selecting family members
- Choosing attendance periods
- Reviewing and submitting

#### 4.3 Membership Management Tour (all users)
- Viewing membership status
- Paying fees
- Understanding membership types

#### 4.4 Camp Management Tour (board members only)
- Proposing camp editions
- Managing locations
- Viewing registrations

### FR-5: Media Support

- Embed short videos (`.mp4` / `.webm`) or images within tour step popovers using Driver.js custom HTML
- Videos should be **short** (15-30 seconds max per step)
- Media files stored in `frontend/public/onboarding/` or loaded from external CDN
- Fallback to text-only if media fails to load

### FR-6: Help Button / Tour Restart

- Add an `OnboardingButton.vue` component (floating action button or in the header/sidebar)
- Shows a dropdown/menu listing available tours for the current page
- Allows re-starting any previously completed tour
- Uses PrimeVue `SpeedDial` or `Menu` component for the trigger

### FR-7: Conditional Tour Triggering

- Tours auto-start on first visit to a page (if not previously completed)
- Board-only tours only shown to users with board role (`requiresBoard`)
- Tours skip steps for elements not present on the page (responsive handling)

---

## Non-Functional Requirements

### NFR-1: Performance
- Driver.js adds only ~5 KB to the bundle (gzipped)
- Tour definition files should be lazy-loaded per page to avoid loading all tours upfront
- Media files should be lazy-loaded only when the step containing them is reached

### NFR-2: Accessibility
- Keyboard navigation (arrow keys, Escape to close) — built into Driver.js
- Screen reader support for popover content
- Sufficient color contrast for overlay and popovers

### NFR-3: Responsiveness
- Tours must work on mobile viewports
- Popover positioning should adapt to screen size (Driver.js handles this automatically)
- Steps targeting elements not visible on mobile should be skipped

### NFR-4: Internationalization
- Tour content (titles, descriptions) should be externalized for future i18n support
- Consider using Vue I18n keys in tour definitions from the start

### NFR-5: Security
- No user data should be exposed in tour content
- Media files should be served from the same origin or a trusted CDN

---

## Technical Implementation Plan

### Step 1: Install Driver.js
```bash
cd frontend && npm install driver.js
```

### Step 2: Create `useOnboarding` Composable
- File: `frontend/src/composables/useOnboarding.ts`
- Wraps Driver.js `driver()` API
- Manages tour registry, progress tracking (localStorage), and auto-trigger logic
- Provides `startTour()`, `hasCompletedTour()`, `resetTour()`, `resetAllTours()`

### Step 3: Create Tour Definition Structure
- Directory: `frontend/src/onboarding/tours/`
- Each tour exports a typed array of step objects
- Types defined in `frontend/src/types/onboarding.ts`

### Step 4: Create `OnboardingButton.vue` Component
- File: `frontend/src/components/ui/OnboardingButton.vue`
- Floating button or header integration using PrimeVue
- Lists available tours for the current route

### Step 5: Integrate into Existing Pages
- Add `data-onboarding="step-id"` attributes to key elements in existing pages
- Wire up auto-start logic in page `onMounted()` hooks
- Start with the Welcome tour on the Home/Dashboard page

### Step 6: Add Media Support
- Extend step configuration to accept media objects
- Create a helper that renders `<video>` / `<img>` HTML for Driver.js popover content
- Store media in `frontend/public/onboarding/`

### Step 7: Write Tests
- Unit tests for `useOnboarding` composable (Vitest)
  - Tour registration and retrieval
  - Progress tracking (localStorage mock)
  - Auto-trigger logic
- Component tests for `OnboardingButton.vue` (Vue Test Utils)
- E2E test for welcome tour flow (Cypress)

---

## Files to Create / Modify

### New Files
| File | Purpose |
|---|---|
| `frontend/src/composables/useOnboarding.ts` | Core onboarding composable |
| `frontend/src/types/onboarding.ts` | TypeScript types for tours and steps |
| `frontend/src/onboarding/index.ts` | Tour registry |
| `frontend/src/onboarding/tours/welcome.tour.ts` | Welcome tour definition |
| `frontend/src/onboarding/tours/registration.tour.ts` | Registration flow tour |
| `frontend/src/onboarding/tours/membership.tour.ts` | Membership tour |
| `frontend/src/onboarding/tours/camp-management.tour.ts` | Camp management tour (board) |
| `frontend/src/components/ui/OnboardingButton.vue` | Help/tour trigger button |
| `frontend/src/components/__tests__/useOnboarding.spec.ts` | Composable unit tests |
| `frontend/src/components/__tests__/OnboardingButton.spec.ts` | Component tests |

### Modified Files
| File | Change |
|---|---|
| `frontend/package.json` | Add `driver.js` dependency |
| `frontend/src/main.ts` | Import Driver.js CSS globally |
| `frontend/src/views/HomeView.vue` (or equivalent dashboard) | Add onboarding auto-trigger |
| `frontend/src/views/registrations/RegisterForCampPage.vue` | Add `data-onboarding` attributes |
| `frontend/src/components/memberships/MembershipDialog.vue` | Add `data-onboarding` attributes |
| `frontend/src/layouts/AuthenticatedLayout.vue` | Add `OnboardingButton` component |

---

## Acceptance Criteria

- [ ] Driver.js is installed and integrated
- [ ] `useOnboarding` composable is created with full TypeScript types
- [ ] At least one complete tour (Welcome) is functional end-to-end
- [ ] Tour progress is persisted per user (localStorage)
- [ ] Tours auto-start on first page visit
- [ ] Users can re-trigger tours from an in-app help button
- [ ] Tour steps support embedded video/image media
- [ ] Unit tests pass for the onboarding composable
- [ ] Component tests pass for OnboardingButton
- [ ] Tours work on mobile viewports
- [ ] All code follows project conventions (TypeScript, Composition API, `<script setup>`, PascalCase files for components, camelCase for TS)

---

## Estimation

| Phase | Scope |
|---|---|
| **Phase 1** | Install Driver.js + `useOnboarding` composable + Welcome tour + OnboardingButton + tests |
| **Phase 2** | Registration flow tour + Membership tour + media support |
| **Phase 3** | Camp management tour (board) + Family unit tour + i18n preparation |
| **Future** | Evaluate Product Fruits if non-technical tour authoring is needed |
