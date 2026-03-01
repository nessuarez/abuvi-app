# Fix: Background image not showing in production (auth pages)

## Problem

The blurred background image on the login/auth pages renders correctly in local development but **does not appear in the deployed production build** (e.g., `https://app-dev.abuvi.org/`). Instead, a plain gray background is shown.

### Root Cause

The background image is referenced using an inline `style` attribute with a **raw source path**:

```html
style="background-image: url('/src/assets/images/landing-background.png');"
```

This path works in **Vite dev mode** because Vite serves files directly from the `/src` directory. However, in **production builds**, Vite processes and hashes assets into `dist/assets/` (e.g., `landing-background-abc123.png`). The `/src/assets/...` path does not exist in the built output, so the image fails to load silently.

## Affected Files

| File | Line(s) |
|------|---------|
| `frontend/src/views/LandingPage.vue` | 8-12 |
| `frontend/src/views/ForgotPasswordPage.vue` | 40-46 |
| `frontend/src/views/ResetPasswordPage.vue` | 81-88 |

All three files use the exact same broken pattern.

## Solution

Import the image in the `<script setup>` block so Vite can process and hash it correctly, then bind it dynamically in the template.

### Implementation Steps

**For each of the 3 affected files:**

1. **Import the image asset** in `<script setup>`:
   ```ts
   import landingBackground from '@/assets/images/landing-background.png'
   ```

2. **Replace the inline `style` attribute** with a dynamic `:style` binding:
   ```html
   <!-- Before -->
   <div class="absolute inset-0 bg-cover bg-center bg-no-repeat" style="
     background-image: url('/src/assets/images/landing-background.png');
     filter: blur(8px);
     transform: scale(1.1);
   " />

   <!-- After -->
   <div
     class="absolute inset-0 bg-cover bg-center bg-no-repeat"
     :style="{
       backgroundImage: `url(${landingBackground})`,
       filter: 'blur(8px)',
       transform: 'scale(1.1)'
     }"
   />
   ```

### Why this works

When using `import`, Vite resolves the asset path at build time and replaces it with the correct hashed URL in the production bundle. This is the standard Vite pattern for referencing static assets in Vue components.

## Acceptance Criteria

- [ ] Background image is visible (blurred) on the **Login page** in production
- [ ] Background image is visible (blurred) on the **Forgot Password page** in production
- [ ] Background image is visible (blurred) on the **Reset Password page** in production
- [ ] Background image continues to work correctly in local development
- [ ] No visual regressions (blur effect, dark overlay, card styling unchanged)

## Non-Functional Requirements

- **Performance**: No impact. The image is the same asset, just correctly referenced.
- **Bundle size**: No change. The image was already included in the build (just not reachable).

## Testing

1. Run `npm run build` in `frontend/`
2. Serve the `dist/` folder locally (e.g., `npx serve dist`)
3. Verify the background image appears on `/`, `/forgot-password`, and `/reset-password?token=test`
4. Verify local dev (`npm run dev`) still works correctly

## Documentation

No documentation updates required.
