# PWA Installability with a Square App Icon — Enriched User Story

## Summary

Allow users to install ABUVI as an app on Android (Chrome "Add to Home screen") and macOS (Safari "Add to Dock"). This requires two things that do not exist today: a **web app manifest** (`manifest.webmanifest`) declaring the app as installable, and a **square icon asset** — the current logo is a wide wordmark and cannot serve as an app icon as-is.

**As** a family member or board member using ABUVI on their phone or Mac,
**I want** to install the webapp with a proper square icon,
**so that** I can open it like a native app from my home screen / Dock instead of through a browser tab.

---

## Current State

- **Framework:** Vue 3.5 + Vite 7 (`frontend/`), no SSR. No PWA plugin (`vite-plugin-pwa` or similar) is installed — see `frontend/package.json`.
- **No manifest:** there is no `manifest.json` / `manifest.webmanifest` anywhere in the project, and [index.html](frontend/index.html) has no `<link rel="manifest">`.
- **No service worker** and no install-related meta tags (`theme-color`, `apple-touch-icon`, `apple-mobile-web-app-capable`) in `index.html`.
- **Current icon assets are not square and not app-icon-shaped:**
  - `frontend/src/assets/images/logo.svg` — a traced wordmark, `viewBox="0 0 11050 6670"` (~1.65:1 landscape ratio). It is the full "ABUVI" text lockup, not a separable mark/symbol — there is no isotipo inside it that can simply be cropped into a square. It is used as the favicon and in [AppHeader.vue](frontend/src/components/layout/AppHeader.vue#L55) / `AuthContainer.vue`.
  - `frontend/src/assets/images/logo-new.png` — 1280×800 PNG, also landscape, and currently unused (no import found in `src/`).
  - `frontend/public/` has no favicon, no icons directory, and only the unused default `vite.svg`.
- **Known placeholder status:** `frontend/src/assets/images/PLACEHOLDER_NOTES.md` explicitly flags `logo.svg` as *"Simple placeholder created. Replace with official ABUVI logo."* — the branding itself is not final. This ticket should not block on final branding (see Decision D1 below).
- **No prior spec exists** for PWA/install/manifest work in `ai-specs/specs/` or `ai-specs/changes/` — this is greenfield.

---

## Decisions Required

| # | Decision | Recommendation |
| --- | --- | --- |
| **D1** | The current logo is a full wordmark with no croppable square mark inside it, and it's a known placeholder. Do we block this ticket on the association delivering official square-icon artwork? | **No.** Ship an interim square icon now: pad the existing wordmark (or a simplified monogram, e.g. just the "A" glyph) onto a solid square canvas using the brand color as background. Track replacing it with an official isotipo as a follow-up design task — do not let branding delivery block installability. |
| **D2** | Use `vite-plugin-pwa` (generates manifest + icons + optional service worker, standard for Vite projects) vs. hand-writing a static `manifest.webmanifest` and meta tags only? | **`vite-plugin-pwa`.** It's the maintained, widely-used solution for Vite, generates the manifest from config (single source of truth), and its `@vite-pwa/assets-generator` companion produces every required icon size from one master image — avoids hand-exporting 6+ PNGs and keeping them in sync by hand. |
| **D3** | Include a service worker (offline caching), or manifest-only (installable, but nothing cached)? | **Minimal service worker, `registerType: 'autoUpdate'`, precaching only the built app shell** (JS/CSS/HTML — no API responses). This is the safer install path across Android Chrome versions and costs little, but must auto-update on every deploy so users never get stuck on a stale bundle (see Non-Functional Requirements). No offline data/API caching — that's a separate, larger feature and out of scope here. |

---

## Changes Breakdown

### 1. Produce the square icon asset

**Deliverable:** a single master square icon, ideally as SVG or a high-resolution (≥1024×1024) PNG, following D1 above (interim: wordmark/monogram padded onto a solid square background in the brand color).

- Store the source at `frontend/src/assets/images/icon-master.png` (or `.svg`).
- Design constraint for the **maskable** variant: the icon content must fit inside the inner ~80% "safe zone" (a centered circle at 40% radius) so Android's adaptive-icon mask doesn't clip it — see [maskable.app](https://maskable.app) for verification once produced.

### 2. Add `vite-plugin-pwa` and generate all required sizes

- `npm install -D vite-plugin-pwa @vite-pwa/assets-generator` in `frontend/`.
- Use the assets generator against the master icon to produce, into `frontend/public/`:
  - `favicon.ico` / `favicon-32x32.png` / `favicon-16x16.png`
  - `pwa-192x192.png`, `pwa-512x512.png` (manifest `icons`, purpose `any`)
  - `maskable-icon-512x512.png` (manifest `icons`, purpose `maskable`)
  - `apple-touch-icon-180x180.png` (iOS/macOS Safari)
- Configure `VitePWA()` in `frontend/vite.config.ts`:

```ts
VitePWA({
  registerType: 'autoUpdate',
  includeAssets: ['favicon.ico', 'apple-touch-icon-180x180.png'],
  manifest: {
    name: 'ABUVI',
    short_name: 'ABUVI',
    description: 'Plataforma de gestión de la Asociación ABUVI',
    lang: 'es',
    start_url: '/',
    scope: '/',
    display: 'standalone',
    background_color: '#ffffff', // match current brand background
    theme_color: '#ffffff',      // match AppHeader brand color
    icons: [
      { src: 'pwa-192x192.png', sizes: '192x192', type: 'image/png' },
      { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png' },
      { src: 'maskable-icon-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
    ],
  },
  workbox: {
    globPatterns: ['**/*.{js,css,html,svg,png,ico}'],
  },
})
```

Exact `background_color`/`theme_color` values must match ABUVI's existing brand color used in `AppHeader.vue`/Tailwind config — confirm during implementation rather than guessing.

### 3. Update `index.html`

- `VitePWA()` auto-injects the `<link rel="manifest">` and registers the service worker; still add manually, since the plugin doesn't manage these:
  - `<meta name="theme-color" content="#ffffff" />`
  - `<link rel="apple-touch-icon" href="/apple-touch-icon-180x180.png" />`
  - `<meta name="apple-mobile-web-app-capable" content="yes" />`
  - `<meta name="apple-mobile-web-app-title" content="ABUVI" />`
- Keep the existing favicon `<link>`, pointed at the new `favicon.svg`/`favicon.ico` instead of `logo.svg`.

### Files to modify

| File | Change |
| --- | --- |
| `frontend/package.json` | Add `vite-plugin-pwa`, `@vite-pwa/assets-generator` devDependencies |
| `frontend/vite.config.ts` | Add and configure `VitePWA()` plugin |
| `frontend/index.html` | Add `theme-color`, `apple-touch-icon`, `apple-mobile-web-app-*` meta tags |
| `frontend/src/assets/images/icon-master.png` (or `.svg`) | New — master square icon source (new file) |
| `frontend/public/*.png`, `favicon.ico` | New — generated icon set (new files, git-tracked, not gitignored) |
| `frontend/src/assets/images/PLACEHOLDER_NOTES.md` | Update to note the interim square icon and that it awaits official branding |
| `ai-specs/specs/frontend-standards.mdc` | Add a short "PWA / App Icons" section documenting the manifest, icon sizes, and how to regenerate them when the logo changes |

---

## Non-Functional Requirements

- **Update safety:** `registerType: 'autoUpdate'` must be verified to activate a new service worker (and thus a new deployed bundle) on next navigation without users being stuck on stale cached assets — test by deploying twice in a row and confirming the second deploy's content appears after a normal reload, not just a hard refresh.
- **HTTPS requirement:** installability (manifest + service worker) requires a secure context. Confirm the production URL is served over HTTPS (expected already, but verify — `beforeinstallprompt`/install won't fire otherwise).
- **No API/data caching:** the service worker precaches only the built app shell (JS/CSS/HTML/icons). It must not cache API responses (`/api/*`) — this would risk serving stale data (payments, registrations) while offline/online. Exclude via `workbox.navigateFallbackDenylist` or by not including API routes in `globPatterns` (they aren't build assets, so this should be the default, but verify).
- **Testing:** no meaningful unit-test coverage applies to static config/assets; verify manually:
  - Chrome Android: "Install app" / "Add to Home screen" prompt appears; installed icon is not clipped or stretched; app opens `display: standalone` (no browser chrome/URL bar).
  - macOS Safari 17+: "Add to Dock" available from the Share menu; Dock icon matches the manifest icon, not a browser-generated screenshot.
  - Confirm `theme_color` matches the header so the OS status bar / title bar doesn't look mismatched.
- **Documentation:** update `ai-specs/specs/frontend-standards.mdc` per the Files table above so future logo changes know to regenerate the PWA icon set.

---

## Acceptance Criteria

- [ ] `frontend/public/` contains a full generated icon set (favicon, 192, 512, maskable 512, apple-touch-icon 180) derived from one square master asset.
- [ ] `manifest.webmanifest` is served and linked from `index.html`, with correct `name`, `short_name`, `display: standalone`, `start_url`, `scope`, `background_color`, `theme_color`, and both `any` and `maskable` icon entries.
- [ ] `index.html` includes `theme-color`, `apple-touch-icon`, and `apple-mobile-web-app-*` meta tags.
- [ ] On Android Chrome, the app is installable and the home-screen icon is not clipped by the adaptive-icon mask.
- [ ] On macOS Safari (17+), "Add to Dock" is available and the Dock icon matches the app icon, not a page screenshot.
- [ ] Redeploying the app updates the installed instance's cached assets without requiring the user to uninstall/reinstall.
- [ ] `PLACEHOLDER_NOTES.md` and `frontend-standards.mdc` are updated to reflect the new icon asset and its interim (non-final-branding) status.

---

## Out of Scope

- Final/official ABUVI branding and logo redesign — tracked separately; this ticket ships an interim square icon per Decision D1.
- Offline support for app data/API responses (only the static app shell is precached).
- Push notifications and background sync.
- iOS/iPadOS "Add to Home Screen" splash screens (`apple-touch-startup-image`) — Safari on iOS is not a stated target in this request (only Android and macOS); can be added later at low cost once the icon set exists.
