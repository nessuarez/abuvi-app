# Layout - Main Page Structure (Frontend) - ENRICHED

## Overview

This document provides a comprehensive specification for implementing the main page layout for the ABUVI application frontend. **IMPORTANT: This is a private, members-only application.** The public landing page serves only as an authentication gateway with a welcome message and login/register form. Once authenticated, members access the full application features.

The visual design is inspired by the [ABUVI showcase](https://abuvi--nessuarez.replit.app) but adapted for a closed, member-only platform.

## User Story

**As a** registered ABUVI member
**I want** to authenticate and access the members-only application
**So that** I can manage my membership, register for camps, and participate in ABUVI activities.

## Acceptance Criteria

### Public Landing Page (Unauthenticated)

- [ ] Blurred background image displays full-screen
- [ ] Welcome message "Bienvenido a ABUVI" is centered vertically and horizontally
- [ ] Login/Register form is displayed below welcome message
- [ ] No navigation menu is visible (authentication gateway only)
- [ ] No footer is visible on landing page
- [ ] Layout is fully responsive (mobile, tablet, desktop)
- [ ] Form has proper validation and error handling
- [ ] Images have proper alt text for accessibility

### Authenticated Layout (Members Only)

- [ ] Header with navigation is visible after login
- [ ] Navigation menu is fully functional with active state indicators
- [ ] Footer displays all required sections and links
- [ ] All internal routes are protected (require authentication)
- [ ] Logout functionality is available
- [ ] User profile access is available in header

---

## Main Components

## PART 1: Public Landing Page (Unauthenticated Users)

### 1. Landing Page with Authentication

#### Structure

A full-screen landing page that serves as the authentication gateway. No navigation or footer - just a welcome message and login/register form centered on a blurred background.

#### Elements

- **Background Image** (Full-screen, blurred):
  - Image: Hero background from ABUVI showcase (nature/camping scene)
  - Implementation: CSS background-image with blur filter
  - Fixed position covering entire viewport
  - Overlay: Semi-transparent dark overlay for better text contrast

- **Welcome Container** (Centered vertically & horizontally):
  - **ABUVI Logo**: Displayed above welcome message
  - **Headline (H1)**: "Bienvenido a ABUVI"
  - **Subtitle** (optional): Brief tagline about the members-only platform
  - **Authentication Form**:
    - **Tabs**: Toggle between "Iniciar Sesión" and "Registrarse"
    - **Login Form**:
      - Email/Username field
      - Password field
      - "Recordarme" checkbox
      - "Olvidé mi contraseña" link
      - "Iniciar Sesión" button
    - **Register Form**:
      - Name field
      - Email field
      - Password field
      - Confirm password field
      - Terms & conditions checkbox
      - "Registrarse" button

#### Technical Requirements

- Component: `LandingPage.vue` or `AuthPage.vue`
- Route: `/` (root) or `/auth`
- Layout: Full viewport height (100vh), centered content
- Background: CSS `filter: blur(8px)` on background image
- Form Validation:
  - Client-side validation with error messages
  - Integration with backend authentication API
- Responsive:
  - Mobile: Full-width form with adequate padding
  - Desktop: Max-width form (400px-500px) centered
- State Management:
  - Toggle between login/register forms
  - Handle authentication state
  - Redirect to dashboard/home after successful login
- Security:
  - Use HTTPS only
  - Implement CSRF protection
  - Secure password requirements

#### Files to Create/Modify

- `src/views/LandingPage.vue` or `AuthPage.vue` (main landing page)
- `src/components/auth/LoginForm.vue` (login form component)
- `src/components/auth/RegisterForm.vue` (registration form component)
- `src/assets/images/landing-background.jpg` (blurred background image)
- `src/router/index.ts` (public route configuration)
- `src/stores/auth.ts` (authentication state management)

---

## PART 2: Authenticated Layout (Members Only)

### 2. Header / Navigation Bar

#### Structure

The header is visible only AFTER successful authentication. It provides navigation to all member-only sections of the application.

#### Elements

- **Logo Section**:
  - Image: ABUVI logo (svg/png format)
  - Text: "ABUVI" brand name
  - Link: Routes to home page (`/home` or `/dashboard`)
  - Position: Left-aligned

- **Navigation Menu**:
  - **Home**: Routes to `/home` (Dashboard/Home page)
  - **Camp**: Routes to `/camp` (Camp registration/info)
  - **Anniversary**: Routes to `/anniversary` (Anniversary celebration)
  - **My Profile**: Routes to `/profile` (Member profile and settings)
  - **Admin**: Routes to `/admin` (Admin panel)
    - Note: Visible only for admin users (role-based visibility)
    - Displayed as a button to differentiate from other links
  - Position: Right-aligned or center-aligned depending on viewport size

- **User Menu** (Right side):
  - User avatar/name
  - Dropdown menu:
    - Profile
    - Settings
    - Logout

#### Technical Requirements

- Component: Create a reusable `AppHeader.vue` or `NavigationBar.vue` component
- Authentication: Only rendered when user is authenticated
- State Management: Track active route to highlight current navigation item
- Role-Based Access: Show/hide admin link based on user role
- Responsive Behavior:
  - Desktop: Horizontal menu
  - Mobile/Tablet: Hamburger menu or compact navigation
- Styling:
  - Use CSS sticky positioning or fixed positioning
  - Ensure proper z-index to appear above other content
  - Add subtle shadow or border-bottom for visual separation

#### Files to Create/Modify

- `src/components/layout/AppHeader.vue` (new component)
- `src/components/layout/UserMenu.vue` (user dropdown menu)
- `src/assets/images/logo.svg` or `logo.png` (logo asset)
- `src/router/index.ts` (ensure all protected routes are defined)

---

### 3. Home/Dashboard Content Area (Authenticated)

The main content area for authenticated members. This replaces the public landing page once logged in.

#### Structure

A dashboard-style layout showcasing member-relevant sections and quick access to key features.

#### Section A: Quick Access Cards

##### Structure

A grid layout displaying quick access cards to main application features (for authenticated members only).

##### Card 1: Camp 2026

- **Icon/Image**: Tent or camp-related icon
- **Label**: "Campamento 2026"
- **Description**: "15 días inolvidables en plena naturaleza. Segunda quincena de agosto."
- **CTA**: "Ver detalles" button → Routes to `/camp`

##### Card 2: 50th Anniversary

- **Icon/Image**: Anniversary or celebration icon
- **Label**: "50 Aniversario"
- **Description**: "Celebrando medio siglo de historias. Participa en los eventos conmemorativos."
- **CTA**: "Participar" button → Routes to `/anniversary`

##### Card 3: My Profile

- **Icon/Image**: User profile icon
- **Label**: "Mi Perfil"
- **Description**: "Gestiona tu información personal y preferencias."
- **CTA**: "Ver perfil" button → Routes to `/profile`

##### Technical Requirements

- Component: `QuickAccessCards.vue` (container) and `QuickAccessCard.vue` (individual card)
- Layout: CSS Grid with 3 columns on desktop, 1-2 columns on tablet, 1 column on mobile
- Card Styling:
  - Consistent padding and spacing
  - Hover effects (e.g., elevation/shadow increase)
  - Icon at top, followed by label, description, and button
- Data Structure: Make cards data-driven with a configuration array
- Authentication: Only visible to authenticated users

##### Files to Create/Modify

- `src/components/home/QuickAccessCards.vue` (container component)
- `src/components/home/QuickAccessCard.vue` (card component)
- `src/assets/images/icons/` (icon assets for each card)

---

#### Section B: Anniversary Highlight Section (Authenticated)

##### Structure

A two-column layout showcasing the 50th anniversary celebration for members.

##### Left Column: Content

- **Eyebrow Text**: "Camino al 2026" (small text above headline)
- **Heading (H2)**: "Medio siglo de ABUVI"
- **Paragraph 1**: "Desde 1976, hemos sido mucho más que un campamento. Hemos sido una escuela de vida, un lugar donde generaciones han aprendido el valor de la amistad, el respeto por la naturaleza y la alegría de vivir sencillamente."
- **Paragraph 2**: "En 2026 cumplimos 50 años y queremos celebrarlo contigo. Estamos recopilando historias, fotos y recuerdos para crear el mayor archivo de nuestra historia."
- **CTA Button**: "Participar en el Aniversario" → Routes to `/anniversary`

##### Right Column: Images

- **Image 1**: "Grupo ABUVI" - Group photo representing the ABUVI community
- **Image 2**: "50 Aniversario Badge" - Anniversary badge/logo overlay
- Layout: Images can be stacked or overlapped for visual interest

##### Technical Requirements

- Component: `AnniversarySection.vue`
- Layout: Two-column grid on desktop, stacked on mobile
- Responsive Images: Use `srcset` or responsive image techniques
- Typography: Clear hierarchy with eyebrow text, heading, body paragraphs
- Spacing: Generous whitespace for readability
- Authentication: Only visible to authenticated users

##### Files to Create/Modify

- `src/components/home/AnniversarySection.vue`
- `src/assets/images/grupo-abuvi.jpg`
- `src/assets/images/50-aniversario-badge.png`

---

### 4. Footer (Authenticated Only)

#### Structure

Multi-column footer layout with comprehensive information and links. **Only visible to authenticated users.**

#### Columns

##### Column 1: Branding & Description

- **Heading (H3)**: "ABUVI"
- **Description**: "Amigos de la Buena Vida. Promoviendo la amistad, la naturaleza y la convivencia desde 1976."

##### Column 2: Enlaces (Links)

- **Heading (H4)**: "Enlaces"
- **Links** (unordered list):
  - Camp 2026 → `/camp`
  - 50 Aniversario → `/anniversary`
  - Mi Perfil → `/profile`
  - Contacto → `/contact`

##### Column 3: Legal

- **Heading (H4)**: "Legal"
- **Links** (unordered list):
  - Aviso Legal → `/legal/notice`
  - Política de Privacidad → `/legal/privacy`
  - Estatutos → `/legal/bylaws`
  - Transparencia → `/legal/transparency`

##### Column 4: Contacto

- **Heading (H4)**: "Contacto"
- **Social Media Icons** (linked):
  - Facebook icon → `#` (external link to ABUVI Facebook)
  - Instagram icon → `#` (external link to ABUVI Instagram)
  - Twitter/X icon → `#` (external link to ABUVI Twitter)
  - YouTube icon → `#` (external link to ABUVI YouTube)
- **Contact Information**:
  - Email: <info@abuvi.org>
  - Phone: +34 600 000 000

#### Footer Bottom

- **Copyright Notice**: "© 2026 Asociación ABUVI. Todos los derechos reservados."
- Style: Centered or left-aligned, smaller font, subtle color

#### Technical Requirements

- Component: `AppFooter.vue`
- Authentication: Only rendered when user is authenticated (not shown on landing page)
- Layout: 4-column grid on desktop, 2-column on tablet, 1-column on mobile
- Social Icons: Use icon library (e.g., FontAwesome, Lucide, or custom SVGs)
- Links: Ensure all links are accessible and have proper hover states
- Responsive: Columns stack gracefully on smaller screens

#### Files to Create/Modify

- `src/components/layout/AppFooter.vue`
- `src/assets/images/icons/social/` (social media icon assets)

---

## Layout Container

### Structure

The application uses TWO different layout structures based on authentication state:

1. **Public Layout** (Unauthenticated): Landing page with auth form only
2. **Authenticated Layout**: Full app with header, content, and footer

### Implementation

#### 1. Public/Landing Layout (Unauthenticated)

Create a `PublicLayout.vue` or use direct route rendering for the landing page:

- Full-screen background (blurred)
- Centered welcome message and auth form
- No header or footer

**Route**: `/` (root) or `/auth`

#### 2. Main/Authenticated Layout

Create a `MainLayout.vue` component for authenticated users that includes:

- `<AppHeader />` at the top
- `<router-view />` for page content in the middle
- `<AppFooter />` at the bottom

**Protected Routes**: `/home`, `/camp`, `/anniversary`, `/profile`, `/admin`, etc.

### Route Protection

All authenticated routes must be protected with route guards:

```typescript
// router/index.ts
router.beforeEach((to, from, next) => {
  const isAuthenticated = checkAuthStatus(); // Check auth state

  if (to.meta.requiresAuth && !isAuthenticated) {
    next('/'); // Redirect to landing/auth page
  } else if (to.path === '/' && isAuthenticated) {
    next('/home'); // Redirect authenticated users to home
  } else {
    next();
  }
});
```

### Files to Create/Modify

- `src/views/LandingPage.vue` (public landing page with auth)
- `src/layouts/MainLayout.vue` (authenticated layout component)
- `src/App.vue` (conditional layout rendering based on auth)
- `src/router/index.ts` (route guards and meta configuration)
- `src/stores/auth.ts` or `useAuth.ts` (authentication state)

---

## Design Specifications

### Color Scheme

Based on the showcase website, define a consistent color palette:

- **Primary Color**: (Define based on ABUVI branding - appears to be nature/outdoor theme)
- **Secondary Color**: (Accent color for CTAs and highlights)
- **Text Colors**:
  - Primary text: Dark gray or black
  - Secondary text: Medium gray
  - Inverse text: White (for dark backgrounds)
- **Background Colors**:
  - Main background: White or light gray
  - Section alternates: Light background variations
  - Footer: Darker background

### Typography

- **Headings**: Sans-serif font (e.g., Inter, Poppins, Montserrat)
  - H1: 3rem-4rem (48px-64px)
  - H2: 2rem-2.5rem (32px-40px)
  - H3: 1.5rem-1.75rem (24px-28px)
  - H4: 1.25rem (20px)
- **Body Text**: Sans-serif font
  - Regular: 1rem (16px)
  - Small: 0.875rem (14px)
- **Line Height**: 1.5-1.6 for body text, 1.2-1.3 for headings

### Spacing

Use a consistent spacing scale (e.g., 4px, 8px, 16px, 24px, 32px, 48px, 64px, 96px)

- Section padding: 64px-96px vertical
- Container max-width: 1200px-1400px
- Grid gaps: 24px-32px

### Responsive Breakpoints

- Mobile: < 640px
- Tablet: 640px - 1024px
- Desktop: > 1024px
- Large Desktop: > 1400px

---

## Technical Implementation Plan

### Technologies

- **Framework**: Vue 3 with Composition API
- **Styling**:
  - Option 1: TailwindCSS (utility-first, responsive design)
  - Option 2: SCSS/CSS Modules (component-scoped styles)
  - Option 3: PrimeVue styled components (if already in use)
- **Icons**: FontAwesome, Lucide Vue, or custom SVG icons
- **Router**: Vue Router (already configured)

### Component Architecture

#### Component Tree - Unauthenticated

```
App.vue (unauthenticated)
└── LandingPage.vue
    ├── Background (blurred)
    └── AuthContainer
        ├── Welcome Message
        └── AuthForms
            ├── LoginForm.vue
            └── RegisterForm.vue
```

#### Component Tree - Authenticated

```
App.vue (authenticated)
└── MainLayout.vue
    ├── AppHeader.vue
    │   └── UserMenu.vue
    ├── router-view
    │   └── HomePage.vue (dashboard/home)
    │       ├── QuickAccessCards.vue
    │       │   └── QuickAccessCard.vue (×3)
    │       └── AnniversarySection.vue
    └── AppFooter.vue
```

#### Shared/Reusable Components

- `Button.vue` - Reusable button component with variants (primary, secondary, ghost)
- `Container.vue` - Max-width container with responsive padding
- `Icon.vue` - Icon wrapper component
- `FormInput.vue` - Reusable form input component for auth forms

### File Structure

```
src/
├── App.vue (conditional rendering based on auth state)
├── layouts/
│   └── MainLayout.vue (authenticated layout)
├── components/
│   ├── layout/
│   │   ├── AppHeader.vue
│   │   ├── AppFooter.vue
│   │   └── UserMenu.vue
│   ├── auth/
│   │   ├── LoginForm.vue
│   │   ├── RegisterForm.vue
│   │   └── AuthContainer.vue
│   ├── home/
│   │   ├── QuickAccessCards.vue
│   │   ├── QuickAccessCard.vue
│   │   └── AnniversarySection.vue
│   └── ui/
│       ├── Button.vue
│       ├── Container.vue
│       ├── FormInput.vue
│       └── Icon.vue
├── views/
│   ├── LandingPage.vue (public auth page)
│   ├── HomePage.vue (authenticated home/dashboard)
│   ├── CampPage.vue
│   ├── AnniversaryPage.vue
│   ├── ProfilePage.vue
│   └── AdminPage.vue
├── stores/
│   └── auth.ts (Pinia auth store)
├── composables/
│   └── useAuth.ts (auth composable)
├── assets/
│   ├── images/
│   │   ├── logo.svg
│   │   ├── landing-background.jpg (blurred background)
│   │   ├── grupo-abuvi.jpg
│   │   ├── 50-aniversario-badge.png
│   │   └── icons/
│   │       ├── tent.svg
│   │       ├── user.svg
│   │       ├── celebration.svg
│   │       └── social/
│   │           ├── facebook.svg
│   │           ├── instagram.svg
│   │           ├── twitter.svg
│   │           └── youtube.svg
│   └── styles/
│       ├── variables.css (colors, spacing, typography)
│       └── global.css
└── router/
    └── index.ts (routes with auth guards)
```

---

## Implementation Steps

### Phase 1: Setup & Foundation

1. [ ] Create folder structure (layouts, views, components, stores)
2. [ ] Define design tokens (colors, typography, spacing) in CSS variables
3. [ ] Set up authentication store (Pinia) with auth state management
4. [ ] Configure router with public and protected routes
5. [ ] Add route guards for authentication
6. [ ] Add placeholder images (landing background, logo, icons) to assets

### Phase 2: Authentication & Landing Page

1. [ ] Create LandingPage.vue component
2. [ ] Add blurred full-screen background image
3. [ ] Create AuthContainer component with centered layout
4. [ ] Create LoginForm.vue component
   - Email/password fields
   - Form validation
   - Submit handler
5. [ ] Create RegisterForm.vue component
   - Registration fields
   - Password confirmation
   - Terms checkbox
   - Submit handler
6. [ ] Implement tab/toggle between login and register forms
7. [ ] Add "Forgot Password" link functionality
8. [ ] Ensure responsive design for mobile/tablet
9. [ ] Test form validation and error messages
10. [ ] Connect to backend authentication API

### Phase 3: Authenticated Layout - Header

1. [ ] Create MainLayout.vue (authenticated layout wrapper)
2. [ ] Create AppHeader component (only for authenticated users)
3. [ ] Implement navigation menu with router-links
   - Home, Camp, Anniversary, Profile, Admin (role-based)
4. [ ] Create UserMenu component (avatar, dropdown)
   - Profile link
   - Settings link
   - Logout functionality
5. [ ] Add active state styling for current route
6. [ ] Implement responsive behavior (hamburger menu for mobile)
7. [ ] Add logo and brand name with proper linking
8. [ ] Test role-based visibility (admin link)

### Phase 4: Authenticated Layout - Home/Dashboard

1. [ ] Create HomePage.vue (authenticated dashboard)
2. [ ] Create QuickAccessCards container component
3. [ ] Create QuickAccessCard component
4. [ ] Implement grid layout with responsiveness
5. [ ] Add three cards: Camp, Anniversary, Profile
6. [ ] Add icons, labels, descriptions, and CTAs to each card
7. [ ] Add hover effects and interactions
8. [ ] Make component data-driven with props

### Phase 5: Anniversary Section (Authenticated)

1. [ ] Create AnniversarySection component
2. [ ] Implement two-column layout
3. [ ] Add content (eyebrow, heading, paragraphs, CTA)
4. [ ] Add images with proper sizing
5. [ ] Ensure responsive stacking on mobile
6. [ ] Update route to `/anniversary`

### Phase 6: Footer (Authenticated)

1. [ ] Create AppFooter component (only for authenticated users)
2. [ ] Implement four-column layout
3. [ ] Add all links with proper routing (English routes)
   - Camp, Anniversary, Profile, Contact
   - Legal: Notice, Privacy, Bylaws, Transparency
4. [ ] Add social media icons with external links
5. [ ] Add contact information
6. [ ] Add copyright notice
7. [ ] Ensure responsive column stacking

### Phase 7: Route Protection & Conditional Rendering

1. [ ] Update App.vue to conditionally render layouts based on auth state
2. [ ] Implement route guards in router/index.ts
   - Redirect unauthenticated users to `/`
   - Redirect authenticated users from `/` to `/home`
3. [ ] Protect all internal routes (home, camp, anniversary, profile, admin)
4. [ ] Test authentication flow (login → redirect to home)
5. [ ] Test logout flow (logout → redirect to landing)
6. [ ] Test direct URL access for protected routes

### Phase 8: Integration & Polish

1. [ ] Integrate all components into MainLayout and HomePage
2. [ ] Test all navigation links
3. [ ] Test responsive behavior across breakpoints
4. [ ] Verify accessibility (keyboard navigation, screen readers, contrast ratios)
5. [ ] Optimize images (compress, use WebP/AVIF formats, blur effect)
6. [ ] Add loading states during authentication
7. [ ] Add smooth transitions between login/register forms
8. [ ] Add error handling for failed authentication

### Phase 9: Testing & Documentation

1. [ ] Write unit tests for auth components (LoginForm, RegisterForm)
2. [ ] Write unit tests for route guards
3. [ ] Test cross-browser compatibility
4. [ ] Test authentication persistence (refresh page)
5. [ ] Verify SEO metadata (page titles, meta descriptions)
6. [ ] Document component props and usage
7. [ ] Document authentication flow
8. [ ] Update project documentation with layout structure

---

## Non-Functional Requirements

### Performance

- **Page Load Time**: Target < 3 seconds on 3G connection
- **Image Optimization**: Use responsive images, lazy loading for below-fold content
- **Code Splitting**: Lazy-load route components
- **CSS**: Minimize unused CSS, consider critical CSS inline for above-fold content

### Accessibility (WCAG 2.1 AA)

- **Color Contrast**: Minimum 4.5:1 for normal text, 3:1 for large text
- **Keyboard Navigation**: All interactive elements must be keyboard accessible
- **Screen Readers**: Proper semantic HTML, ARIA labels where needed
- **Alt Text**: All images must have descriptive alt attributes
- **Focus Indicators**: Visible focus states for all interactive elements

### SEO

- **Semantic HTML**: Use proper heading hierarchy (H1 → H2 → H3)
- **Meta Tags**: Page title, description, Open Graph tags
- **Structured Data**: Consider schema.org markup for organization info
- **URLs**: Clean, descriptive URLs for all routes

### Browser Support

- **Modern Browsers**: Latest 2 versions of Chrome, Firefox, Safari, Edge
- **Mobile Browsers**: iOS Safari, Chrome Mobile
- **Fallbacks**: Graceful degradation for older browsers if necessary

### Responsive Design

- **Mobile-First**: Design and develop for mobile, enhance for larger screens
- **Touch Targets**: Minimum 44x44px for touch interactions
- **Viewport**: Properly configured viewport meta tag
- **Orientation**: Support both portrait and landscape orientations

---

## Acceptance Testing Checklist

### Landing Page (Unauthenticated)

#### Visual & Layout

- [ ] Blurred background image displays full-screen
- [ ] Welcome message "Bienvenido a ABUVI" is centered
- [ ] ABUVI logo is displayed above welcome message
- [ ] Login/Register form is centered vertically and horizontally
- [ ] No header navigation is visible
- [ ] No footer is visible
- [ ] Auth form has clean, professional styling

#### Functionality

- [ ] Login form validates email and password
- [ ] Register form validates all required fields
- [ ] Toggle between login and register forms works
- [ ] "Forgot Password" link is functional
- [ ] Form error messages display correctly
- [ ] Successful login redirects to `/home`
- [ ] Successful registration redirects to `/home` or email verification page
- [ ] Form submission shows loading state

#### Responsiveness

- [ ] Landing page layout works on mobile (< 640px)
- [ ] Landing page layout works on tablet (640px - 1024px)
- [ ] Landing page layout works on desktop (> 1024px)
- [ ] Form is properly sized on all screen sizes
- [ ] Background image scales appropriately
- [ ] Text is readable on all screen sizes

### Authenticated Layout

#### Visual & Layout

- [ ] Header is visible and properly styled on all authenticated pages
- [ ] Logo is visible and links to `/home`
- [ ] Navigation menu items are aligned and styled correctly
- [ ] User menu (avatar/dropdown) is visible in header
- [ ] Quick access cards display in grid layout and are evenly spaced
- [ ] Anniversary section has proper two-column layout (or stacked on mobile)
- [ ] Footer displays all four columns with content
- [ ] Footer social icons are visible and linked
- [ ] Copyright notice is displayed at bottom

#### Functionality

- [ ] All navigation links route to correct pages (English routes)
- [ ] Logo link routes to `/home`
- [ ] Quick access card buttons route to correct pages
- [ ] Footer links route to correct pages
- [ ] Legal links route to correct legal pages
- [ ] User menu dropdown opens/closes correctly
- [ ] Logout functionality works and redirects to `/`
- [ ] Admin link is only visible to admin users

#### Route Protection

- [ ] Unauthenticated users accessing `/home` are redirected to `/`
- [ ] Unauthenticated users accessing `/camp` are redirected to `/`
- [ ] Unauthenticated users accessing `/profile` are redirected to `/`
- [ ] Authenticated users accessing `/` are redirected to `/home`
- [ ] Authentication state persists on page refresh
- [ ] Protected routes require authentication

#### Responsiveness

- [ ] Authenticated layout works on mobile devices (< 640px)
- [ ] Authenticated layout works on tablets (640px - 1024px)
- [ ] Authenticated layout works on desktops (> 1024px)
- [ ] Images scale appropriately on all screen sizes
- [ ] Navigation menu works on mobile (hamburger menu)
- [ ] Footer columns stack correctly on smaller screens

### Accessibility (Both Layouts)

- [ ] Pages have proper document outline (H1 → H2 → H3 hierarchy)
- [ ] All images have alt text
- [ ] All links and buttons are keyboard accessible (Tab navigation)
- [ ] Focus states are visible on interactive elements
- [ ] Color contrast meets WCAG AA standards
- [ ] Screen reader can navigate the pages logically
- [ ] Form inputs have proper labels and ARIA attributes

### Performance (Both Layouts)

- [ ] Landing page loads in under 2 seconds
- [ ] Authenticated pages load in under 3 seconds on simulated 3G
- [ ] Images are optimized and use appropriate formats
- [ ] Background image blur effect performs well
- [ ] No console errors or warnings
- [ ] Layout does not shift during page load (CLS < 0.1)
- [ ] Authentication check is fast and doesn't block UI

---

## Dependencies

### NPM Packages

- `vue` (^3.x)
- `vue-router` (^4.x)
- Styling solution: `tailwindcss` OR `sass` (depending on choice)
- Icon library: `@fortawesome/vue-fontawesome` OR `lucide-vue-next` (depending on choice)

### Assets Needed

- ABUVI logo (SVG or PNG)
- Hero background image (high-resolution)
- Feature card icons (tent, community, celebration)
- Anniversary images (Grupo ABUVI, 50 Aniversario badge)
- Social media icons (Facebook, Instagram, Twitter, YouTube)

---

## Related Documentation

- [Base Standards](../../specs/base-standards.md) - General coding standards and best practices
- Vue Router Documentation: <https://router.vuejs.org/>
- PrimeVue Documentation (if used): <https://primevue.org/>
- TailwindCSS Documentation (if used): <https://tailwindcss.com/>

---

## Notes

### General

- **Members-Only Application**: This is NOT a public website. Access is restricted to registered members only.
- **No Public Membership Sign-up**: The application does not promote or allow public membership registration beyond the initial auth form.
- **Authentication Required**: All routes except `/` (landing/auth) require authentication.
- **Route Naming**: All routes use English nomenclature (e.g., `/camp`, `/profile`, `/anniversary`).

### Page Titles

**IMPORTANT**: Page titles must follow this structure:

- **Landing Page** (unauthenticated): `"ABUVI"` (just the brand name)
- **All Other Pages** (authenticated): `"ABUVI | [Section Name]"`

Examples:

- Landing: `<title>ABUVI</title>`
- Home: `<title>ABUVI | Home</title>`
- Camp: `<title>ABUVI | Camp</title>`
- Profile: `<title>ABUVI | Profile</title>`
- Anniversary: `<title>ABUVI | 50th Anniversary</title>`
- Admin: `<title>ABUVI | Admin</title>`
- Legal pages: `<title>ABUVI | Privacy Policy</title>`, etc.

Implement this using Vue Router meta tags:

```typescript
{
  path: '/home',
  component: HomePage,
  meta: {
    requiresAuth: true,
    title: 'ABUVI | Home'
  }
}
```

Then update the title in a router afterEach hook:

```typescript
router.afterEach((to) => {
  document.title = to.meta.title || 'ABUVI';
});
```

### Implementation Details

- Admin navigation link visibility should be controlled by user role
- Consider implementing smooth transitions between login/register forms
- Add loading animations during authentication API calls
- Consider implementing a "Remember Me" feature for persistent login
- Ensure secure password requirements (min length, complexity)
- Consider email verification flow for new registrations
- Social media links in footer should open in new tab (target="_blank")
- Legal links should route to actual legal pages (not `#`)

---

## Success Metrics

- All acceptance criteria are met
- Zero accessibility violations in automated testing (axe, Lighthouse)
- Lighthouse score > 90 for Performance, Accessibility, Best Practices, SEO
- User testing feedback is positive regarding navigation and visual appeal
- Page load time < 3 seconds on 3G connection
- Cross-browser testing passes on all target browsers
