# Replace Home Quick Access Bar with Hero Carousel

## Summary

Remove the Quick Access Cards section from the Home page (`/home`) and replace it with a full-width Hero carousel featuring slides with background images, overlaid text, and call-to-action buttons. Also remove the standalone `AnniversarySection` since its content will be absorbed into the first carousel slide. Update the Driver.js welcome tour to reflect the new layout.

## Motivation

The Quick Access Cards bar duplicates navigation already available in the top navigation bar (`AppHeader`), providing no shortcut value. A Hero carousel better uses the prime real-estate of the home page to highlight key announcements and drive engagement.

## Detailed Requirements

### 1. New Component: `HomeHeroCarousel.vue`

**Location**: `frontend/src/components/home/HomeHeroCarousel.vue`

A full-width carousel component using **PrimeVue Galleria** (already used in `AnniversaryHero.vue`) with the following characteristics:

- **Auto-play**: Slides rotate automatically every 6 seconds
- **Circular**: Loops back to the first slide after the last
- **Indicators**: Show dot indicators at the bottom for manual navigation
- **Item navigators**: Show left/right arrows on hover (desktop) for manual navigation
- **Responsive height**: `min-h-[60vh] md:min-h-[70vh]` to be impactful without overwhelming
- **Each slide** consists of:
  - A full-bleed background image with `object-cover`
  - A dark gradient overlay for text legibility (similar pattern to `AnniversaryHero.vue`)
  - Overlaid content: headline, short description, and a CTA `<router-link>` button
- **Slide data structure**:
  ```ts
  interface HeroSlide {
    image: string        // imported image asset
    imageAlt: string
    headline: string
    description: string
    ctaLabel: string
    ctaPath: string      // vue-router path
  }
  ```

### 2. Slide Content (minimum 3 slides)

| # | Headline | Description | CTA Label | CTA Path | Background Image |
|---|----------|-------------|-----------|----------|-----------------|
| 1 | **50 Años de Buena Vida** | Desde 1976 creando recuerdos. Celebra con nosotros medio siglo de ABUVI compartiendo tus historias y fotos. | Participar en el Aniversario | `/anniversary` | `grupo-abuvi.jpg` |
| 2 | **Campamento 2026** | 15 días inolvidables en plena naturaleza. Segunda quincena de agosto. ¡Prepárate para la aventura! | Ver detalles del campamento | `/camp` | `camping-tents-generic.jpg` |
| 3 | **Configura tu Familia** | Actualiza los datos de tus familiares para facilitar las inscripciones y mantener tu información al día. | Ir a Mi Perfil | `/profile` | `camping-friends.jpg` |

> Note: All images already exist in `frontend/src/assets/images/`. Use static imports as done in `AnniversaryHero.vue`.

### 3. Home Page Changes (`HomePage.vue`)

- **Remove** import and usage of `QuickAccessCards`
- **Remove** import and usage of `AnniversarySection`
- **Add** import and usage of `HomeHeroCarousel`
- The carousel renders **outside** the `<Container>` wrapper so it can be full-width
- Keep the `data-onboarding="welcome-heading"` attribute on the carousel wrapper div (or reassign to the new hero section) so the onboarding tour can still target it
- Keep the `useOnboarding` auto-trigger logic intact

**Target template structure:**
```vue
<template>
  <div class="bg-gray-50">
    <div data-onboarding="hero-carousel">
      <HomeHeroCarousel />
    </div>
    <!-- Future: additional home sections can go here inside Container -->
  </div>
</template>
```

### 4. Driver.js Welcome Tour Update (`welcome.tour.ts`)

The current welcome tour has 4 steps. After this change:

- **Step 1** (`welcome-heading` → `hero-carousel`): Update the element selector from `[data-onboarding="welcome-heading"]` to `[data-onboarding="hero-carousel"]`. Update title and description to reference the Hero carousel instead of "panel de inicio":
  - Title: `"Bienvenido a ABUVI"`
  - Description: `"Este es el carrusel de novedades. Aquí encontrarás las últimas noticias y eventos destacados de ABUVI."`
- **Step 2** (`quick-access-cards`): **Remove entirely** — this element no longer exists.
- **Steps 3 & 4** (`main-nav`, `user-menu`): Keep as-is, they become steps 2 and 3.

**Updated tour (3 steps):**
```ts
steps: [
  {
    element: '[data-onboarding="hero-carousel"]',
    title: 'Bienvenido a ABUVI',
    description: 'Este es el carrusel de novedades. Aquí encontrarás las últimas noticias y eventos destacados de ABUVI.',
    side: 'bottom',
  },
  {
    element: '[data-onboarding="main-nav"]',
    title: 'Menú de Navegación',
    description: 'Usa la barra de navegación para moverte entre las diferentes secciones de la plataforma.',
    side: 'bottom',
  },
  {
    element: '[data-onboarding="user-menu"]',
    title: 'Tu Perfil',
    description: 'Accede a la configuración de tu perfil y cierra sesión desde aquí.',
    side: 'bottom',
  },
]
```

### 5. Cleanup: Remove Unused Components

After the changes, the following components are **no longer used anywhere** and should be deleted:

- `frontend/src/components/home/QuickAccessCards.vue`
- `frontend/src/components/home/QuickAccessCard.vue`
- `frontend/src/components/home/AnniversarySection.vue`

### 6. Unit Tests

Add a test file `frontend/src/__tests__/components/home/HomeHeroCarousel.spec.ts`:

- Verify the component renders 3 slides
- Verify each slide contains its headline, description, CTA label, and correct router-link `to` attribute
- Verify the `data-onboarding="hero-carousel"` attribute is present on the wrapper in `HomePage.vue`

Update existing test (if any) for `HomePage.vue` to remove references to `QuickAccessCards` and `AnniversarySection`.

## Files to Modify

| File | Action |
|------|--------|
| `frontend/src/components/home/HomeHeroCarousel.vue` | **Create** — New hero carousel component |
| `frontend/src/views/HomePage.vue` | **Modify** — Replace QuickAccessCards + AnniversarySection with HomeHeroCarousel |
| `frontend/src/onboarding/tours/welcome.tour.ts` | **Modify** — Remove quick-access step, update hero step selector |
| `frontend/src/components/home/QuickAccessCards.vue` | **Delete** |
| `frontend/src/components/home/QuickAccessCard.vue` | **Delete** |
| `frontend/src/components/home/AnniversarySection.vue` | **Delete** |
| `frontend/src/__tests__/components/home/HomeHeroCarousel.spec.ts` | **Create** — Unit tests |

## Acceptance Criteria

- [ ] Home page shows a full-width hero carousel with at least 3 slides
- [ ] Each slide has a background image, headline, description, and CTA button linking to the correct route
- [ ] Carousel auto-plays with ~6s interval, is circular, and has navigation indicators/arrows
- [ ] Quick Access Cards bar is completely removed from the home page
- [ ] AnniversarySection is removed (content absorbed into slide 1)
- [ ] Driver.js welcome tour works correctly with 3 steps (hero carousel, main nav, user menu)
- [ ] The `quick-access-cards` onboarding step is removed from the tour
- [ ] The hero carousel area has a `data-onboarding` attribute for the tour
- [ ] Unused components (`QuickAccessCards.vue`, `QuickAccessCard.vue`, `AnniversarySection.vue`) are deleted
- [ ] Unit tests pass for the new component
- [ ] No regressions in existing functionality (routing, onboarding auto-trigger, responsive layout)

## Non-Functional Requirements

- **Performance**: Images should use static imports (handled by Vite asset pipeline) for proper hashing and caching. Consider lazy-loading off-screen slide images if performance is impacted.
- **Accessibility**: Each slide image must have meaningful `alt` text. CTA buttons must be focusable and keyboard-navigable.
- **Responsiveness**: Carousel must look good on mobile (single column, readable text) through desktop. Adjust text sizes with responsive Tailwind classes (`text-2xl md:text-4xl`).
- **Design consistency**: Use the same gradient overlay pattern and color scheme (amber/yellow) as `AnniversaryHero.vue` to maintain visual cohesion with ABUVI brand.
