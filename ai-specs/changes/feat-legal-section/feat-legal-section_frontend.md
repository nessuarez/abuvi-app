# Frontend Implementation Plan: feat-legal-section Legal Pages

## Overview

This plan covers the implementation of four public legal pages for the ABUVI website: **Aviso Legal**, **Política de Privacidad**, **Estatutos**, and **Transparencia**. These are mock-up pages — structure and UI are fully implemented, with placeholder/template content rather than final approved legal text.

The implementation follows Vue 3 Composition API with `<script setup lang="ts">`, PrimeVue components, and Tailwind CSS. Pages are public routes (no authentication required). When visited by authenticated users they render naturally inside `AuthenticatedLayout` via App.vue. When visited directly by unauthenticated users they render standalone, with the `LegalPageLayout` providing its own minimal navigation back to home.

**No changes to App.vue or AuthenticatedLayout are needed** — public routes without `requiresAuth` render via `<router-view v-else />` for unauthenticated users and via `AuthenticatedLayout` for authenticated users, both cases work correctly.

---

## Architecture Context

### New Files

```
frontend/src/
├── components/
│   └── legal/
│       ├── LegalPageLayout.vue          # Reusable layout for all legal pages
│       ├── TableOfContents.vue          # Auto-generated anchor TOC
│       └── __tests__/
│           ├── LegalPageLayout.test.ts
│           └── TableOfContents.test.ts
└── views/
    └── legal/
        ├── NoticeLegalPage.vue          # Aviso Legal (/legal/notice)
        ├── PrivacyPage.vue              # Política de Privacidad (/legal/privacy)
        ├── BylawsPage.vue               # Estatutos (/legal/bylaws)
        └── TransparencyPage.vue         # Transparencia (/legal/transparency)
```

### Modified Files

- `frontend/src/router/index.ts` — Add four public `/legal/*` routes

### Route Mapping

The footer already references these exact paths — implementation must match:

| Footer Link          | Route Path          | View Component         |
|----------------------|---------------------|------------------------|
| Aviso Legal          | `/legal/notice`     | `NoticeLegalPage.vue`  |
| Política de Privacidad | `/legal/privacy`  | `PrivacyPage.vue`      |
| Estatutos            | `/legal/bylaws`     | `BylawsPage.vue`       |
| Transparencia        | `/legal/transparency` | `TransparencyPage.vue` |

### State Management

No Pinia store or composable needed — legal pages are purely static content with no API calls.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to the feature branch before any code changes
- **Implementation Steps**:
  1. Ensure you're on `main`: `git checkout main`
  2. Pull latest: `git pull origin main`
  3. Create branch: `git checkout -b feature/feat-legal-section-frontend`
  4. Verify: `git branch`
- **Notes**: Never make changes on `main`. This must be done before any file modifications.

---

### Step 1: Create `LegalPageLayout.vue` Component

- **File**: `frontend/src/components/legal/LegalPageLayout.vue`
- **Action**: Create the shared layout wrapper for all legal pages
- **Component Signature**:
  ```typescript
  interface Props {
    title: string
    lastUpdated: string
    showToc?: boolean        // default: true
    showPrintButton?: boolean // default: true
  }
  ```
- **Implementation Steps**:
  1. Create `frontend/src/components/legal/` directory
  2. Create `LegalPageLayout.vue` with `<script setup lang="ts">`
  3. Layout structure:
     - **Top bar** (always visible, provides navigation even for unauthenticated access):
       - ABUVI text logo linking to `/` via `<router-link>`
       - "Volver al inicio" button with back arrow icon (`pi pi-arrow-left`)
     - **Header section**: `<h1>{{ title }}</h1>` + last updated date
     - **Content area**: Two-column on desktop (`lg:grid-cols-[280px_1fr]`) with TOC sidebar + main slot
     - **Actions bar**: Print/PDF button using `window.print()`
  4. Add `@media print` CSS via `<style>` block to hide nav, actions, and TOC when printing
  5. Use `slot` for the main document content
  6. Use `slot name="toc"` passed to `TableOfContents` via props for TOC generation
- **Dependencies**: `vue-router` (RouterLink), PrimeVue Button, PrimeVue Divider
- **Implementation Notes**:
  - The top bar is always shown regardless of auth state — this makes the component self-sufficient for unauthenticated direct access
  - Authenticated users will also see `AppHeader` above this, creating minor redundancy but acceptable for mock-up
  - Max content width: `max-w-4xl mx-auto` for optimal reading line length
  - Use `prose` Tailwind class or equivalent custom typography utilities for legal text

---

### Step 2: Create `TableOfContents.vue` Component

- **File**: `frontend/src/components/legal/TableOfContents.vue`
- **Action**: Static prop-driven TOC (entries passed as data, no DOM scanning for mock-up simplicity)
- **Component Signature**:
  ```typescript
  interface TocEntry {
    id: string     // anchor ID matching h2/h3 id attributes in content
    label: string  // Display text
    level: 1 | 2  // 1 = h2 (main section), 2 = h3 (subsection)
  }

  interface Props {
    entries: TocEntry[]
  }
  ```
- **Implementation Steps**:
  1. Accept `entries` prop array
  2. Render a `<nav aria-label="Tabla de contenidos">` with an `<ol>` list
  3. Each entry renders as `<a :href="'#' + entry.id">{{ entry.label }}</a>`
  4. Level 2 entries get left padding (`pl-4`) for visual hierarchy
  5. Sticky positioning on desktop: `lg:sticky lg:top-8`
- **Dependencies**: None (pure Tailwind + plain HTML)
- **Implementation Notes**:
  - Kept simple (static data) for the mock-up — no runtime DOM scanning
  - Each legal page declares its own `tocEntries` array matching the anchor IDs in its content

---

### Step 3: Write Unit Tests for `LegalPageLayout.vue` and `TableOfContents.vue`

- **Files**:
  - `frontend/src/components/legal/__tests__/LegalPageLayout.test.ts`
  - `frontend/src/components/legal/__tests__/TableOfContents.test.ts`
- **Action**: Write Vitest unit tests **before** implementing final component details (TDD)
- **Implementation Steps**:

  **LegalPageLayout.test.ts** — test cases:
  ```
  - should render the page title in h1
  - should render the last updated date
  - should render the "Volver al inicio" router link
  - should render the print button when showPrintButton is true (default)
  - should not render the print button when showPrintButton is false
  - should render slot content
  - should show TOC sidebar when showToc is true (default)
  - should hide TOC sidebar when showToc is false
  ```

  **TableOfContents.test.ts** — test cases:
  ```
  - should render nav with aria-label "Tabla de contenidos"
  - should render one link per entry
  - should set href to #entry.id for each entry
  - should display entry label text
  - should apply pl-4 class to level 2 entries
  - should render empty list when entries is empty
  ```

- **Dependencies**: `@vue/test-utils`, `vitest`, Vue Router mock (`createRouter`, `createMemoryHistory`)
- **Implementation Notes**: Mock `RouterLink` or use `createRouter` with `createMemoryHistory` for test environment

---

### Step 4: Add Legal Routes to Router

- **File**: `frontend/src/router/index.ts`
- **Action**: Add four public `/legal` child routes **without** `requiresAuth`
- **Implementation Steps**:
  1. Add a `/legal` parent route with children array (or flat routes — flat is simpler)
  2. Add four routes as flat public routes:
  ```typescript
  // Public legal routes
  {
    path: '/legal/notice',
    name: 'legal-notice',
    component: () => import('@/views/legal/NoticeLegalPage.vue'),
    meta: { title: 'ABUVI | Aviso Legal', requiresAuth: false }
  },
  {
    path: '/legal/privacy',
    name: 'legal-privacy',
    component: () => import('@/views/legal/PrivacyPage.vue'),
    meta: { title: 'ABUVI | Política de Privacidad', requiresAuth: false }
  },
  {
    path: '/legal/bylaws',
    name: 'legal-bylaws',
    component: () => import('@/views/legal/BylawsPage.vue'),
    meta: { title: 'ABUVI | Estatutos', requiresAuth: false }
  },
  {
    path: '/legal/transparency',
    name: 'legal-transparency',
    component: () => import('@/views/legal/TransparencyPage.vue'),
    meta: { title: 'ABUVI | Transparencia', requiresAuth: false }
  },
  ```
  3. No route guard changes needed — routes without `requiresAuth: true` already pass through the guard

- **Implementation Notes**:
  - Lazy-load all legal pages with dynamic imports for performance
  - Keep flat routes (not nested under a parent) to keep the guard logic simple
  - The `requiresAuth: false` is explicit for clarity but not strictly needed (guard only blocks when `requiresAuth: true`)

---

### Step 5: Create `NoticeLegalPage.vue` (Aviso Legal)

- **File**: `frontend/src/views/legal/NoticeLegalPage.vue`
- **Action**: Implement the Aviso Legal mock-up page
- **Implementation Steps**:
  1. Create `frontend/src/views/legal/` directory
  2. Create component using `<script setup lang="ts">`
  3. Define `tocEntries` array with these sections:
     - `{ id: 'identificacion', label: '1. Identificación del Titular', level: 1 }`
     - `{ id: 'objeto', label: '2. Objeto del Sitio Web', level: 1 }`
     - `{ id: 'propiedad-intelectual', label: '3. Propiedad Intelectual e Industrial', level: 1 }`
     - `{ id: 'responsabilidad', label: '4. Responsabilidad', level: 1 }`
     - `{ id: 'legislacion', label: '5. Legislación Aplicable y Jurisdicción', level: 1 }`
  4. Wrap content in `<LegalPageLayout title="Aviso Legal" last-updated="Febrero 2026" :show-toc="true">`
  5. Implement each section with placeholder content structured per the spec:
     - **Section 1** (`id="identificacion"`): Organization data table (Denominación: Asociación ABUVI, CIF: G-79013322, Domicilio: C/BUTRÓN 27, 28022 Madrid, Email: juntaabuvi@gmail.com, Hosting: NAMECHEAP INC.)
     - **Section 2** (`id="objeto"`): Placeholder paragraph about website purpose
     - **Section 3** (`id="propiedad-intelectual"`): Placeholder copyright notice
     - **Section 4** (`id="responsabilidad"`): Placeholder liability disclaimers
     - **Section 5** (`id="legislacion"`): Placeholder jurisdiction text
  6. Use `<dl>` definition list for organization identification data for semantic HTML
- **Dependencies**: `LegalPageLayout.vue`, `TableOfContents.vue`
- **Implementation Notes**:
  - Content is mock-up placeholder — add `<!-- TODO: Replace with officially approved content -->` comment at top
  - Each `<h2>` must have the matching `id` attribute from `tocEntries`
  - Use `text-gray-700 leading-relaxed` for paragraph text readability

---

### Step 6: Create `PrivacyPage.vue` (Política de Privacidad)

- **File**: `frontend/src/views/legal/PrivacyPage.vue`
- **Action**: Implement the Privacy Policy mock-up page (GDPR-compliant structure)
- **Implementation Steps**:
  1. Create component using `<script setup lang="ts">`
  2. Define `tocEntries` array:
     - `{ id: 'responsable', label: '1. Responsable del Tratamiento', level: 1 }`
     - `{ id: 'datos-recopilados', label: '2. Datos Personales que Recopilamos', level: 1 }`
     - `{ id: 'base-legal', label: '3. Base Legal y Finalidad', level: 1 }`
     - `{ id: 'conservacion', label: '4. Conservación de Datos', level: 1 }`
     - `{ id: 'destinatarios', label: '5. Destinatarios de los Datos', level: 1 }`
     - `{ id: 'derechos', label: '6. Derechos de los Usuarios (RGPD)', level: 1 }`
     - `{ id: 'seguridad', label: '7. Medidas de Seguridad', level: 1 }`
     - `{ id: 'cookies', label: '8. Política de Cookies', level: 1 }`
     - `{ id: 'contacto', label: '9. Contacto y Reclamaciones', level: 1 }`
  3. Wrap in `<LegalPageLayout title="Política de Privacidad" last-updated="Febrero 2026">`
  4. Implement all 9 GDPR-required sections with placeholder content:
     - **Section 6** (User Rights) is the most important: use `<ul>` list for the 7 GDPR rights
     - **Section 9** must include mention of AEPD (Agencia Española de Protección de Datos)
  5. Contact email for exercising rights: `juntaabuvi@gmail.com`
- **Implementation Notes**: Content is placeholder. Add a `<Message severity="warn">` PrimeVue component at top noting "Contenido pendiente de revisión legal" for the mock-up

---

### Step 7: Create `BylawsPage.vue` (Estatutos)

- **File**: `frontend/src/views/legal/BylawsPage.vue`
- **Action**: Implement the Estatutos mock-up page with expandable article structure
- **Implementation Steps**:
  1. Create component using `<script setup lang="ts">`
  2. Use PrimeVue `Accordion` component for collapsible sections — ideal for long statute documents
  3. Define accordion tabs for the 7 main chapters:
     - Capítulo I: Disposiciones Generales (Art. 1-5)
     - Capítulo II: Fines y Actividades (Art. 6-8)
     - Capítulo III: Socios (Art. 9-15)
     - Capítulo IV: Órganos de Gobierno (Art. 16-28)
     - Capítulo V: Régimen Económico (Art. 29-32)
     - Capítulo VI: Modificación de Estatutos (Art. 33)
     - Capítulo VII: Disolución (Art. 34-35)
  4. Wrap in `<LegalPageLayout title="Estatutos de la Asociación ABUVI" last-updated="[Pendiente de fecha oficial]" :show-toc="false">` (TOC disabled since Accordion provides navigation)
  5. Add header metadata: "Aprobados en Asamblea General: [Pendiente]"
  6. Each accordion tab contains placeholder article text
  7. Add `<Button label="Descargar PDF" icon="pi pi-download" outlined />` (non-functional in mock-up, disabled with tooltip "Próximamente")
- **Dependencies**: PrimeVue Accordion, PrimeVue AccordionTab, PrimeVue Button
- **Implementation Notes**:
  - The official statutes content must come from ABUVI leadership — all content here is placeholder
  - Use `<AccordionTab :pt="{ header: { class: 'font-semibold' } }">`

---

### Step 8: Create `TransparencyPage.vue` (Transparencia)

- **File**: `frontend/src/views/legal/TransparencyPage.vue`
- **Action**: Implement the Transparency page with governance and financial structure
- **Implementation Steps**:
  1. Create component using `<script setup lang="ts">`
  2. Define `tocEntries`:
     - `{ id: 'quienes-somos', label: 'Quiénes Somos', level: 1 }`
     - `{ id: 'estructura-gobierno', label: 'Estructura de Gobierno', level: 1 }`
     - `{ id: 'estructura-organizativa', label: 'Estructura Organizativa', level: 1 }`
     - `{ id: 'informacion-financiera', label: 'Información Financiera', level: 1 }`
     - `{ id: 'memoria-actividades', label: 'Memoria de Actividades', level: 1 }`
     - `{ id: 'documentos', label: 'Documentos y Memorias', level: 1 }`
     - `{ id: 'contacto', label: 'Contacto Transparencia', level: 1 }`
  3. Wrap in `<LegalPageLayout title="Transparencia" last-updated="Febrero 2026">`
  4. Section structure:
     - **Quiénes Somos**: Brief history paragraph ("ABUVI (Amigos de la Buena Vida) es una asociación sin ánimo de lucro fundada en 1976...")
     - **Estructura de Gobierno** (`id="estructura-gobierno"`): Board table using PrimeVue `DataTable` with placeholder member data (columns: Cargo, Nombre, Mandato)
     - **Estructura Organizativa** (`id="estructura-organizativa"`): Stats panel (3 cards: Nº de socios, Nº de voluntarios, Años de historia) using PrimeVue `Card`
     - **Información Financiera** (`id="informacion-financiera"`): Two placeholder subsections — Presupuesto 2026 + Cuentas Anuales 2025 with download buttons (disabled, tooltip "Próximamente")
     - **Memoria de Actividades** (`id="memoria-actividades"`): Placeholder summary with key metrics (campistas, eventos)
     - **Documentos** (`id="documentos"`): `DataTable` with downloadable document list (Informe Anual, Cuentas Anuales, Acta Asamblea) — all with disabled download buttons for the mock-up
     - **Contacto** (`id="contacto"`): Email for transparency inquiries (`transparencia@abuvi.org` placeholder)
- **Dependencies**: PrimeVue DataTable, Column, Card, Button
- **Implementation Notes**: Board member data is all placeholder — use `[Pendiente]` values

---

### Step 9: Write Cypress E2E Tests

- **File**: `frontend/cypress/e2e/legal-pages.cy.ts`
- **Action**: Write E2E tests for legal page navigation and basic content rendering
- **Implementation Steps**:
  1. Create `frontend/cypress/e2e/legal-pages.cy.ts`
  2. Test cases:
     ```
     - should navigate to /legal/notice and display heading "Aviso Legal"
     - should navigate to /legal/privacy and display heading "Política de Privacidad"
     - should navigate to /legal/bylaws and display heading "Estatutos"
     - should navigate to /legal/transparency and display heading "Transparencia"
     - should show "Volver al inicio" link on each legal page
     - should navigate back to landing page from "Volver al inicio" link
     - should display last updated date on Aviso Legal page
     - should display last updated date on Privacy page
     - should display table of contents on Aviso Legal page
     - should anchor navigation work (TOC link scrolls to section)
     ```
  3. Add data-testid attributes to key elements in the view components:
     - `data-testid="legal-page-title"` on `<h1>`
     - `data-testid="legal-last-updated"` on last updated paragraph
     - `data-testid="legal-back-link"` on "Volver al inicio" link
     - `data-testid="legal-toc"` on the TOC nav element
     - `data-testid="print-button"` on the print button
- **Implementation Notes**: Tests should run without authentication since these are public routes

---

### Step 10: Update Technical Documentation

- **Action**: Update documentation to reflect the new routes and components
- **Implementation Steps**:
  1. **Identify changes**: New public routes, new `views/legal/` directory, new `components/legal/` directory, `LegalPageLayout` pattern
  2. **Update** `ai-specs/specs/frontend-standards.mdc`:
     - Add `views/legal/` and `components/legal/` to the Project Structure section
     - Add `LegalPageLayout` as a documentation example of a content layout component
     - Note the pattern for public (no-auth) routes
  3. **Update** `ai-specs/specs/api-spec.yml` (if applicable): No API changes needed
  4. **Verify**: All updated docs accurately reflect implementation
- **References**: `ai-specs/specs/documentation-standards.mdc`
- **Notes**: This step is MANDATORY before considering the implementation complete.

---

## Implementation Order

1. **Step 0** — Create feature branch `feature/feat-legal-section-frontend`
2. **Step 1** — Create `LegalPageLayout.vue` (shared component, dependency for all views)
3. **Step 2** — Create `TableOfContents.vue` (dependency for LegalPageLayout)
4. **Step 3** — Write unit tests for both layout components (**TDD: tests before final implementation**)
5. **Step 4** — Add legal routes to `router/index.ts`
6. **Step 5** — Create `NoticeLegalPage.vue`
7. **Step 6** — Create `PrivacyPage.vue`
8. **Step 7** — Create `BylawsPage.vue`
9. **Step 8** — Create `TransparencyPage.vue`
10. **Step 9** — Write Cypress E2E tests
11. **Step 10** — Update technical documentation

---

## Testing Checklist

### Unit Tests (Vitest)
- [ ] `LegalPageLayout.vue` — title prop renders in h1
- [ ] `LegalPageLayout.vue` — lastUpdated prop renders
- [ ] `LegalPageLayout.vue` — slot content renders
- [ ] `LegalPageLayout.vue` — showToc prop controls TOC visibility
- [ ] `LegalPageLayout.vue` — showPrintButton prop controls print button
- [ ] `LegalPageLayout.vue` — "Volver al inicio" RouterLink present
- [ ] `TableOfContents.vue` — renders nav with correct aria-label
- [ ] `TableOfContents.vue` — renders link per entry with correct href
- [ ] `TableOfContents.vue` — level 2 entries have indentation class
- [ ] `TableOfContents.vue` — handles empty entries array

### Cypress E2E Tests
- [ ] All four `/legal/*` routes load without 404
- [ ] Pages accessible without authentication
- [ ] "Volver al inicio" link navigates correctly
- [ ] TOC anchors scroll to correct sections
- [ ] Each page displays title and last updated date
- [ ] Print button is visible

### Manual Verification
- [ ] Footer links in AppFooter navigate to correct pages (test when authenticated)
- [ ] Pages render correctly on mobile (375px)
- [ ] Pages render correctly on tablet (768px)
- [ ] Pages render correctly on desktop (1440px)
- [ ] `window.print()` triggers browser print dialog
- [ ] Accordion in BylawsPage opens/closes correctly

---

## Error Handling Patterns

These pages have no API calls. Error handling only applies to:
- **Router 404**: If a user navigates to an undefined `/legal/*` path — the existing router handles this (falls through)
- **Print failure**: `window.print()` is native and cannot fail silently; no extra handling needed

---

## UI/UX Considerations

### PrimeVue Components Used

| Page | PrimeVue Components |
|------|---------------------|
| LegalPageLayout | `Button` |
| NoticeLegalPage | (none beyond layout) |
| PrivacyPage | `Message` (warning banner for mock-up notice) |
| BylawsPage | `Accordion`, `AccordionTab`, `Button` |
| TransparencyPage | `DataTable`, `Column`, `Card`, `Button` |

### Typography

- Section headings (`h2`): `text-2xl font-bold text-gray-900 mt-10 mb-4`
- Article headings (`h3`): `text-lg font-semibold text-gray-800 mt-6 mb-2`
- Body text: `text-gray-700 leading-relaxed` with `text-base`
- Max content width: `max-w-4xl` for optimal line length (~75 chars)

### Layout

- Desktop: Two-column grid — `lg:grid-cols-[280px_1fr]` for TOC + content
- Tablet/Mobile: Single column, TOC hidden or collapsed
- TOC: `lg:sticky lg:top-8 lg:self-start` for scroll-following behavior

### Print Styles (`<style>` block in LegalPageLayout)

```css
@media print {
  .legal-top-bar,
  .legal-actions,
  .legal-toc {
    display: none;
  }
  .legal-content {
    max-width: 100%;
    padding: 0;
  }
  a[href]::after {
    content: " (" attr(href) ")";
  }
}
```

### Accessibility

- `<h1>` for page title (one per page)
- `<h2>` for main sections, `<h3>` for sub-sections (proper hierarchy)
- TOC `<nav>` has `aria-label="Tabla de contenidos"`
- Print button has `aria-label="Imprimir o descargar como PDF"`
- "Volver al inicio" link has descriptive text (no icon-only)
- All external links have `rel="noopener noreferrer"`

---

## Dependencies

### No New NPM Packages Required

All functionality uses:
- Vue Router (already installed) — for RouterLink and navigation
- PrimeVue (already installed) — Accordion, DataTable, Card, Button, Message
- Tailwind CSS (already installed) — all styling

### Existing PrimeVue Components Used

- `Button` — print action, download buttons
- `Accordion` + `AccordionTab` — bylaws collapsible sections
- `DataTable` + `Column` — board members table, documents list
- `Card` — transparency stats display
- `Message` — mock-up notice banner on Privacy page

---

## Notes

- **Mock-up content**: All legal text is placeholder. Add `<!-- TODO: Replace with officially approved legal content -->` comments in each view
- **Language**: UI labels in Spanish, code/variables/comments in English
- **Routes match footer exactly**: `/legal/notice`, `/legal/privacy`, `/legal/bylaws`, `/legal/transparency` — do not change these
- **No auth required**: These are public pages — never add `requiresAuth: true` to these routes
- **TypeScript strict**: No `any` types, all props typed with interfaces
- **Data-testid attributes**: Required on key elements for Cypress E2E stability
- **TDD**: Write unit tests (Step 3) BEFORE finalizing implementation details of LegalPageLayout and TableOfContents

---

## Next Steps After Implementation

- ABUVI leadership to provide official content for each legal page (Aviso Legal, Estatutos)
- Legal advisor review for Política de Privacidad (GDPR compliance)
- PDF documents to be provided by organization for download links in BylawsPage and TransparencyPage
- Board member and financial data to be provided for TransparencyPage
- Consider cookie consent banner integration (Phase 2 from spec)

---

## Implementation Verification

### Code Quality

- [ ] All components use `<script setup lang="ts">` — no Options API
- [ ] All props typed with TypeScript interfaces (no `any`)
- [ ] No inline styles — Tailwind utilities only (except print `@media` in style block)
- [ ] All code comments in English
- [ ] All user-facing text in Spanish

### Functionality

- [ ] All four legal pages render at correct routes
- [ ] Footer links in AppFooter navigate to correct pages
- [ ] TOC anchors work (click → scroll to section)
- [ ] Print button opens browser print dialog
- [ ] "Volver al inicio" navigates back to home (authenticated: `/home`, unauthenticated: `/`)
- [ ] Accordion in BylawsPage is functional
- [ ] Pages accessible without login

### Testing

- [ ] Unit tests pass for `LegalPageLayout` (all scenarios)
- [ ] Unit tests pass for `TableOfContents` (all scenarios)
- [ ] Cypress E2E tests pass for all four pages
- [ ] No TypeScript errors (`npx vue-tsc --noEmit`)
- [ ] ESLint passes (`npm run lint`)

### Integration

- [ ] No router conflicts with existing routes
- [ ] Route guard does not block legal pages
- [ ] Lazy-loaded pages verified in network tab (separate chunks)

### Documentation

- [ ] `frontend-standards.mdc` updated with new project structure entries
- [ ] Documentation changes reviewed for accuracy
