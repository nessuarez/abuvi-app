# Mock 50th Anniversary Page — Enriched User Story

## Objective

Implement the `/anniversary` page as a visually rich, self-contained **mock** (static data, no backend calls) celebrating ABUVI's 50th anniversary (1976–2026). The page is for demonstration purposes only: forms submit locally with a toast confirmation, uploads are simulated, and gallery images use placeholder content. The existing route and `AnniversaryPage.vue` stub will be fully replaced.

---

## Context

- **Founded**: 1976. **50th anniversary year**: 2026.
- **Route**: `/anniversary` (already registered in `router/index.ts`, `requiresAuth: true`).
- **Current state**: `AnniversaryPage.vue` is an empty stub.
- **No backend needed**: All data is static/mock. No API endpoints, no composables, no Pinia store.
- **Layout**: Page renders inside the existing `AuthenticatedLayout` (header + footer already present).
- **Language**: All user-facing text in **Spanish**. Code in English.

---

## Sections

### 1. Hero / Landing Section

- Full-width banner with a gold-and-dark branded background (gradient: `from-yellow-900 via-amber-800 to-yellow-700` or similar warm palette).
- Centered "50" large numeral with the subtitle "ABUVI · 1976–2026".
- Tagline: *"Medio siglo de aventuras, amistad y naturaleza."*
- CTA button: scrolls to the upload section (`#subir-recuerdo`).
- Decorative badge image using existing `@/assets/images/50-aniversario-badge.png`.

### 2. Timeline Section

- Horizontal (desktop) / vertical (mobile) timeline with 5–8 hardcoded milestones.
- Each milestone: `year`, `title`, `description` (all static strings defined in the component).
- Example milestones (use realistic values):
  - 1976 — Primer campamento en Miraflores de la Sierra.
  - 1982 — Primera edición con más de 50 familias.
  - 1990 — Fundación de la Junta Directiva formal.
  - 2000 — 25º Aniversario: campamento de verano histórico.
  - 2010 — Primer campamento de invierno.
  - 2020 — Pandemia: campamento virtual.
  - 2026 — 50º Aniversario: celebración especial.
- Styled with PrimeVue `Timeline` component (`primevue/timeline`).

### 3. Upload / Contribute Section (`#subir-recuerdo`)

- Heading: *"Comparte tu recuerdo"*
- A form with:

  | Field | Type | Validation |
  |---|---|---|
  | Nombre | `InputText` (required) | Not empty |
  | Tipo de contenido | `Dropdown` options: Foto, Vídeo, Audio, Historia escrita | Required |
  | Año aproximado | `InputNumber` (optional) | 1976–2026 range |
  | Descripción / mensaje | `Textarea` (optional, max 500 chars) | — |
  | Archivo | `FileUpload` (PrimeVue, mode="basic") | UI only, no real upload |

- Submit button: *"Enviar recuerdo"*
- Submit button is disabled at this moment. These feature would be implemented in the future when the backend is ready.
- No API call is made. The form clears after submission.

### 4. Photo Gallery Section

- Heading: *"Galería de recuerdos"*
- 6–9 placeholder cards in a responsive grid (1 col mobile / 3 col tablet / 4 col desktop).
- Each card: placeholder image (`https://picsum.photos/seed/abuvi{n}/400/300`), a mock year label, and a mock author name.
- Use PrimeVue `Image` component with preview enabled.
- No lazy-loading or API fetching required.
- Clicking an image opens the preview modal with the larger version of the placeholder.
- Below the gallery, a note: *"¿Tienes más recuerdos para compartir? ¡Usa el formulario arriba!"*
- Disclaimer text: *"Todas las imágenes son de ejemplo y no representan recuerdos reales. En el futuro mejoraremos esta sección."*

### 5. Contact Form Section

- Heading: *"Contacta con los organizadores si quieres colaborar en este 50º aniversario"*
- Form fields:

  | Field | Type | Validation |
  |---|---|---|
  | Nombre | `InputText` | Required |
  | Correo electrónico | `InputText` type email | Required, valid email |
  | Mensaje | `Textarea` | Required, max 1000 chars |

- Submit button: *"Enviar mensaje"*
- On submit: show `Toast` success *"Mensaje enviado. ¡Gracias!"* — no API call.

---

## File Structure

```
frontend/src/
├── views/
│   └── AnniversaryPage.vue            ← REPLACE stub with full implementation
└── components/
    └── anniversary/
        ├── AnniversaryHero.vue        ← Section 1
        ├── AnniversaryTimeline.vue    ← Section 2
        ├── AnniversaryUploadForm.vue  ← Section 3
        ├── AnniversaryGallery.vue     ← Section 4
        └── AnniversaryContactForm.vue ← Section 5
```

No new composables, stores, or types files are needed (all data is static inline).

---

## Implementation Details

### AnniversaryPage.vue

- Orchestrates the five child components in order.
- Provides `ToastService` is already registered globally; use `useToast()` in form components.
- Each section is separated by a visual divider or background contrast change.
- Add a sticky internal navigation bar (anchors to each section) for desktop.

### Styling

- Accent palette: amber/gold (`amber-500`, `yellow-600`, `amber-900`).
- Sections alternate between white and `amber-50` backgrounds.
- All components use Tailwind utilities only (no `<style>` blocks).
- Hero section uses a full-width gradient div, not an image background.

### Form Validation Pattern

Use inline `reactive` state and a `validate()` function per the project's standard:

```typescript
const errors = ref<Record<string, string>>({})
const validate = (): boolean => {
  errors.value = {}
  if (!form.name.trim()) errors.value.name = 'El nombre es obligatorio'
  // ...
  return Object.keys(errors.value).length === 0
}
const handleSubmit = () => {
  if (!validate()) return
  toast.add({ severity: 'success', summary: 'Éxito', detail: '...', life: 4000 })
  // reset form
}
```

---

## Testing

### Scope

This is a static mock with no composables, no API calls, and no stores. Unit tests for composables are not applicable. **Component tests** are required for the two interactive forms.

### Tests Required

**File**: `frontend/src/components/anniversary/__tests__/AnniversaryUploadForm.test.ts`

| Test | Description |
|---|---|
| `should show validation error when name is empty` | Submit without name → error message shown |
| `should show validation error when content type not selected` | Submit without type → error message shown |
| `should call toast and reset form on valid submission` | Fill required fields → submit → toast spy called |

**File**: `frontend/src/components/anniversary/__tests__/AnniversaryContactForm.test.ts`

| Test | Description |
|---|---|
| `should show validation error when name is empty` | Submit without name → error message shown |
| `should show validation error when email is invalid` | Submit with "not-an-email" → error shown |
| `should show validation error when message is empty` | Submit without message → error shown |
| `should call toast and reset form on valid submission` | Fill all fields → submit → toast spy called |

### Framework

- Vitest + Vue Test Utils (`@vue/test-utils`).
- Mock `useToast` from `primevue/usetoast`.
- Mock PrimeVue components globally in test setup if needed.

---

## Acceptance Criteria

- [ ] `/anniversary` renders all 5 sections without errors.
- [ ] Hero renders with correct styling and scroll CTA.
- [ ] Timeline shows all hardcoded milestones in correct order.
- [ ] Upload form validates required fields (name, content type) and shows toast on success.
- [ ] Gallery renders 6+ placeholder cards in responsive grid.
- [ ] Contact form validates name, email format, and message; shows toast on success.
- [ ] Page is responsive: 1-column on mobile, multi-column on desktop.
- [ ] All user-facing text is in Spanish.
- [ ] All component/variable names are in English.
- [ ] `AnniversaryPage.vue` no longer shows the placeholder text.
- [ ] All form component unit tests pass.

---

## Non-Functional Requirements

- **Performance**: No external API calls. Page should load instantly.
- **Accessibility**: Semantic HTML (`<section>`, `<h2>`, `<article>`), `aria-label` on sections, alt text on images.
- **No backend changes**: Zero changes to the .NET backend.
- **No router changes**: Route `/anniversary` already exists and is correctly configured.
- **No new dependencies**: All required components (PrimeVue `Timeline`, `FileUpload`, `Image`, `Toast`) are already available.

---

## Out of Scope

- Real file upload to blob storage.
- Persisting form submissions to a database.
- Real email sending from the contact form.
- Admin review workflow for submitted memories.
- Integration with the `Memory` or `MediaItem` entities from the data model.
- Any backend endpoints.
