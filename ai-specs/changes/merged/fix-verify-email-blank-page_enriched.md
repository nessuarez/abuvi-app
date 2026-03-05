# Bug Fix: Email Verification Link Shows Blank Page

## Problem

When a user clicks the email verification link (e.g., `https://app-dev.abuvi.org/verify-email?token=...`), they see a **blank page**. The verification flow is completely broken in the deployed application.

## Root Cause

The **deployed frontend** (`frontend/`) does not have a `/verify-email` route or component. The route and component (`VerifyEmailPage.vue`) only exist in the legacy `src/Abuvi.Web/` directory, which is **not what gets deployed** to production/dev.

- `frontend/src/router/index.ts` — No `/verify-email` route defined
- `frontend/src/pages/auth/` — No `VerifyEmailPage.vue` exists
- Since there's no catch-all/404 route in the router either, Vue Router renders nothing — resulting in a blank page

The backend endpoint `POST /api/auth/verify-email` works correctly. The issue is purely frontend.

## Related Context

- The `frontend/` RegisterForm already handles **resend verification** inline (see `frontend/src/components/auth/RegisterForm.vue:109-149`)
- The backend generates a verification URL using `{FrontendUrl}/verify-email?token={token}` (in `ResendEmailService.SendVerificationEmailAsync`)
- Tokens expire after 24 hours
- On successful verification, the backend sets `EmailVerified=true`, `IsActive=true`, clears the token, and sends a welcome email

## Affected Files

### Files to Create

| File | Purpose |
|---|---|
| `frontend/src/views/VerifyEmailPage.vue` | New page component for email verification |

### Files to Modify

| File | Change |
|---|---|
| `frontend/src/router/index.ts` | Add `/verify-email` route (public, `requiresAuth: false`) |

### Reference Files (existing implementations to mirror)

| File | Notes |
|---|---|
| `src/Abuvi.Web/src/pages/auth/VerifyEmailPage.vue` | Original implementation — adapt to `frontend/` patterns |
| `src/Abuvi.Web/src/composables/useAuth.ts` | Has `verifyEmail()` — need equivalent in `frontend/` |
| `frontend/src/utils/api.ts` | Axios instance to use for API calls |
| `frontend/src/components/auth/RegisterForm.vue` | Reference for resend verification pattern already in `frontend/` |

## Implementation Steps

### 1. Create `VerifyEmailPage.vue` in `frontend/src/views/`

- Read `token` from `route.query.token`
- Call `POST /api/auth/verify-email` with `{ token }` using the existing `api` axios instance
- Display three states:
  - **Verifying**: Spinner while the API call is in progress
  - **Success**: Confirmation message + "Ir al inicio" button (redirect to `/`)
  - **Error**: Error message with appropriate text based on error code:
    - `NOT_FOUND` → "Enlace de verificación inválido"
    - `VERIFICATION_FAILED` → "El enlace de verificación ha expirado. Por favor, solicita uno nuevo."
    - Default → "Ha ocurrido un error. Por favor, inténtalo de nuevo."
  - Error state should include a link/button to resend verification (either inline or redirect to landing)
- Use PrimeVue components consistent with the rest of `frontend/` (check existing views for patterns)
- All text in Spanish

### 2. Add route to `frontend/src/router/index.ts`

- Add `/verify-email` route in the public routes section (near `/forgot-password` and `/reset-password`)
- Set `meta: { requiresAuth: false, title: "ABUVI | Verificar Email" }`

### 3. (Optional) Add `/resend-verification` standalone page

- Currently resend is only available inline in `RegisterForm.vue`
- If a user's token expires and they visit the verification link, they need a way to request a new one
- Consider adding a simple page or redirecting to landing with a message

## Acceptance Criteria

- [ ] Clicking an email verification link navigates to the verification page (not blank)
- [ ] A valid, non-expired token successfully verifies the email and shows a success message
- [ ] An expired token shows an appropriate error message with option to resend
- [ ] An invalid/used token shows an appropriate error message
- [ ] A missing token (no `?token=` param) shows an error message
- [ ] The page is accessible without authentication (`requiresAuth: false`)
- [ ] After successful verification, user can navigate to login/landing
- [ ] UI text is in Spanish, consistent with the rest of the application
- [ ] Page styling matches the existing `frontend/` design patterns (PrimeVue + Tailwind)

## Non-Functional Requirements

- **Security**: Token is sent only via POST body, not exposed in logs. No additional security changes needed (backend already handles token validation).
- **Performance**: The page is lazy-loaded via dynamic import.
- **Error handling**: All API errors are caught and displayed to the user with meaningful messages.

## API Reference

### `POST /api/auth/verify-email`

**Request body:**

```json
{ "token": "string" }
```

**Success (200):**

```json
{ "data": { "message": "Email verified successfully" } }
```

**Error (404):** Token not found

```json
{ "error": { "code": "NOT_FOUND", "message": "..." } }
```

**Error (400):** Token expired

```json
{ "error": { "code": "VERIFICATION_FAILED", "message": "..." } }
```
