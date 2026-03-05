# Enriched User Story: Top Bar Logo Adaptation & Hamburger Button Fix

## Context

The Abuvi logo has horizontal proportions that don't fit well in the 64px-tall top bar (`AppHeader.vue`). Currently, the logo is rendered at `h-10 w-10` (40x40px) — forcing a square crop of what is naturally a horizontal/landscape logo. Additionally, the hamburger menu button (3 horizontal bars) on the right side of the top bar does nothing on desktop, and should be hidden on desktop viewports (it's only useful for mobile navigation).

## User Stories

### US-1: Adaptive Logo Display (Logo vs Text by Context)

**As a** user,
**I want** the Abuvi logo to display correctly depending on available space,
**So that** the brand identity is properly represented without distortion or awkward cropping.

#### Acceptance Criteria

1. **Desktop (≥1024px / `lg:` breakpoint):** Display the full horizontal logo (landscape format) in the top bar, replacing the current square icon + "ABUVI" text combination. The logo should fit within the header height (`h-16` / 64px) with appropriate vertical padding.
2. **Mobile (<1024px):** Display either:
   - The square/compact logo icon (`h-10 w-10`) alongside the "ABUVI" text (current behavior), OR
   - Just the text "ABUVI" if the compact logo still doesn't fit well.
3. The logo must remain a clickable `<router-link>` to `/home`.
4. The logo should have appropriate `alt` text for accessibility.

#### Implementation Details

**Logo Assets Required:**

- A **horizontal/landscape version** of the Abuvi logo (SVG preferred for scalability). This needs to be provided or exported from the existing brand assets. Save to `frontend/src/assets/images/logo-horizontal.svg`.
- The existing `logo.svg` can continue to be used as the compact/icon version for mobile.

**File to Modify:** [AppHeader.vue](frontend/src/components/layout/AppHeader.vue)

**Changes to the Logo Section (lines 33-41):**

Replace the current logo block with a responsive version:

```vue
<!-- Logo -->
<router-link to="/home" class="flex items-center gap-3">
  <!-- Desktop: horizontal logo -->
  <img
    src="@/assets/images/logo-horizontal.svg"
    alt="ABUVI"
    class="hidden h-8 lg:block"
  />
  <!-- Mobile: compact icon + text -->
  <img
    src="@/assets/images/logo.svg"
    alt="ABUVI Logo"
    class="h-10 w-10 lg:hidden"
  />
  <span class="text-xl font-bold text-primary-600 lg:hidden">ABUVI</span>
</router-link>
```

> **Note:** The exact height class for the horizontal logo (`h-8`, `h-10`, etc.) should be adjusted based on the actual proportions of the horizontal logo asset. The logo should have vertical padding within the 64px header.

---

### US-2: Hide Hamburger Menu Button on Desktop

**As a** user on desktop,
**I want** the hamburger menu button to not be visible,
**So that** the interface is clean and doesn't have non-functional UI elements.

#### Acceptance Criteria

1. The hamburger menu button (3 horizontal bars / `pi pi-bars`) must **not** be visible on desktop viewports (≥1024px / `lg:` breakpoint).
2. The button must remain visible and functional on mobile viewports (<1024px).
3. The mobile navigation menu must continue to toggle correctly when the button is clicked on mobile.

#### Current State Analysis

Looking at the code in [AppHeader.vue:80-86](frontend/src/components/layout/AppHeader.vue#L80-L86):

```vue
<Button
  icon="pi pi-bars"
  text
  rounded
  class="lg:hidden"
  @click="toggleMobileMenu"
/>
```

**The `lg:hidden` class is already present**, which should hide the button on desktop (≥1024px). This suggests one of two possibilities:

1. **PrimeVue Button component override:** PrimeVue's `<Button>` component may be applying inline styles or CSS that override Tailwind's `lg:hidden` utility. The `display` property set by PrimeVue may have higher specificity than Tailwind's responsive class.
2. **CSS specificity issue:** The `text` and `rounded` props on PrimeVue Button generate classes that may include `display: inline-flex` with higher specificity.

#### Implementation Details

**File to Modify:** [AppHeader.vue](frontend/src/components/layout/AppHeader.vue)

**Option A — Wrap in a div (Recommended):**
Wrap the Button in a container div that handles visibility, since PrimeVue may override the button's own display property:

```vue
<!-- Mobile Menu Button -->
<div class="lg:hidden">
  <Button
    icon="pi pi-bars"
    text
    rounded
    @click="toggleMobileMenu"
  />
</div>
```

**Option B — Use `!important` via Tailwind:**

```vue
<Button
  icon="pi pi-bars"
  text
  rounded
  class="lg:!hidden"
  @click="toggleMobileMenu"
/>
```

Option A is preferred as it avoids `!important` and is more maintainable.

---

## Prerequisites / Blockers

- **Logo asset:** A horizontal/landscape version of the Abuvi logo in SVG format must be created or provided before US-1 can be fully implemented. If not available, the developer should:
  1. Check with the design/brand team for the asset.
  2. As a temporary measure, use just the "ABUVI" text on desktop (without the square icon) until the horizontal logo is available.

## Testing

### Unit Tests

**File:** `frontend/src/components/layout/__tests__/AppHeader.test.ts`

1. **Logo rendering test:** Verify that the horizontal logo has `hidden lg:block` classes and the compact logo has `lg:hidden` classes.
2. **Hamburger visibility test:** Verify that the hamburger button container has `lg:hidden` class.
3. **Mobile menu toggle test:** Verify that clicking the hamburger button toggles the mobile navigation.

### Manual Testing

1. Open the app in a desktop browser (≥1024px wide):
   - Verify the horizontal logo is displayed correctly in the top bar.
   - Verify the hamburger button is not visible.
2. Resize the browser to mobile width (<1024px):
   - Verify the compact logo/text is displayed.
   - Verify the hamburger button is visible and toggles the mobile menu.
3. Click the logo on both viewports — should navigate to `/home`.

## Non-Functional Requirements

- **Performance:** SVG logo should be optimized (minified, no unnecessary metadata). Keep file size under 50KB.
- **Accessibility:** Both logo images must have descriptive `alt` attributes.
- **Responsive:** Transitions between desktop/mobile layouts should be seamless with no layout shift.

## Definition of Done

- [ ] Horizontal logo asset added to `frontend/src/assets/images/`
- [ ] `AppHeader.vue` updated with responsive logo rendering
- [ ] Hamburger button hidden on desktop (fix specificity issue)
- [ ] Unit tests pass with ≥90% coverage
- [ ] Manual testing completed on desktop and mobile viewports
- [ ] No regressions in existing navigation functionality
