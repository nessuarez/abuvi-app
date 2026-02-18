# Enriched User Story: Forgot Password Flow

**Task ID:** `feat-forgot-password`
**Priority:** High
**Type:** Feature (Full-stack)

---

## Root Cause Analysis

The "¿Olvidaste tu contraseña?" link at `frontend/src/components/auth/LoginForm.vue:105` is a static `<a href="#">` placeholder — no event handler, no router navigation. There is no `/forgot-password` or `/reset-password` route in the router.

**Partial infrastructure that already exists:**

- `IEmailService.SendPasswordResetEmailAsync()` is declared and implemented in `ResendEmailService.cs` — sends email with URL `{frontendUrl}/reset-password?token={resetToken}` and 1-hour expiry message.
- `GenerateVerificationToken()` private method in `AuthService.cs` can be reused (256-bit CSPRNG, Base64URL encoded).
- BCrypt password hashing via `IPasswordHasher` is already available.

**What is entirely missing:**

- User entity fields for password reset token
- DB migration
- Backend endpoints: `POST /api/auth/forgot-password` and `POST /api/auth/reset-password`
- Service methods: `ForgotPasswordAsync()` and `ResetPasswordAsync()`
- Repository method: `GetByPasswordResetTokenAsync()`
- Frontend routes, pages, and API calls for the two-step flow

---

## User Story

**As** a registered user who has forgotten their password,
**I want** to request a password reset via email and set a new password using a secure token link,
**so that** I can regain access to my account without contacting an administrator.

---

## Acceptance Criteria

### Step 1 — Request reset (Forgot Password)

1. Clicking "¿Olvidaste tu contraseña?" in `LoginForm.vue` navigates to `/forgot-password`.
2. The page shows a single field: email address, and a submit button "Enviar enlace de recuperación".
3. On submit, the frontend calls `POST /api/auth/forgot-password` with `{ email }`.
4. **Always returns HTTP 200** regardless of whether the email exists (prevent user enumeration).
5. If the email exists, the backend:
   - Generates a secure 256-bit random token (Base64URL encoded, same logic as `GenerateVerificationToken()`).
   - Stores `PasswordResetToken` and `PasswordResetTokenExpiry` (1 hour from now) on the user record.
   - Sends the password reset email via `IEmailService.SendPasswordResetEmailAsync()`.
   - Logs the request: `_logger.LogInformation("Password reset requested for {Email}", email)`.
6. The frontend shows a success message: _"Si tu correo está registrado, recibirás un enlace para restablecer tu contraseña."_
7. A "Volver al inicio de sesión" link navigates back to `/`.

### Step 2 — Reset password

1. Clicking the link in the email navigates to `/reset-password?token={token}`.
2. The page reads the `token` query parameter. If absent, shows error and redirects to `/`.
3. The page shows two fields: "Nueva contraseña" and "Confirmar contraseña", and a submit button "Restablecer Contraseña".
4. Client-side validation:
   - Both fields required.
   - Passwords match.
   - Minimum 8 characters.
5. On submit, the frontend calls `POST /api/auth/reset-password` with `{ token, newPassword }`.
6. The backend:
   - Finds the user by `PasswordResetToken`.
   - Returns HTTP 400 (`INVALID_OR_EXPIRED_TOKEN`) if token not found or expired.
   - Hashes the new password with `IPasswordHasher.HashPassword()`.
   - Updates `PasswordHash`, clears `PasswordResetToken` and `PasswordResetTokenExpiry`.
   - Token is **one-time use** — cleared immediately on use.
   - Logs: `_logger.LogInformation("Password reset completed for user {UserId}", user.Id)`.
   - Returns HTTP 200 on success.
7. On success, the frontend shows: _"Tu contraseña ha sido restablecida exitosamente."_ and a "Iniciar Sesión" link back to `/`.
8. On backend error (expired/invalid token), the frontend shows: _"El enlace de recuperación es inválido o ha expirado."_

---

## Backend Implementation

### 1. User Entity — New Fields

**File:** `src/Abuvi.API/Features/Users/UsersModels.cs`

Add to the `User` class:

```csharp
public string? PasswordResetToken { get; set; }
public DateTime? PasswordResetTokenExpiry { get; set; }
```

### 2. EF Core Configuration

**File:** `src/Abuvi.API/Data/Configurations/UserConfiguration.cs`

Add:

```csharp
builder.Property(u => u.PasswordResetToken)
    .HasMaxLength(512)
    .HasColumnName("password_reset_token");

builder.Property(u => u.PasswordResetTokenExpiry)
    .HasColumnName("password_reset_token_expiry");
```

### 3. Database Migration

```bash
dotnet ef migrations add AddPasswordResetTokenToUsers
dotnet ef database update
```

### 4. Repository — New Method

**File:** `src/Abuvi.API/Features/Users/IUsersRepository.cs` — add:

```csharp
Task<User?> GetByPasswordResetTokenAsync(string token, CancellationToken ct);
```

**File:** `src/Abuvi.API/Features/Users/UsersRepository.cs` — implement:

```csharp
public async Task<User?> GetByPasswordResetTokenAsync(string token, CancellationToken ct)
    => await _context.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == token, ct);
```

### 5. Request/Response Models

**File:** `src/Abuvi.API/Features/Auth/AuthModels.cs` — add:

```csharp
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string NewPassword);
```

### 6. Validators

**New files** in `src/Abuvi.API/Features/Auth/`:

`ForgotPasswordRequestValidator.cs`:

```csharp
public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio")
            .EmailAddress().WithMessage("Formato de correo electrónico inválido");
    }
}
```

`ResetPasswordRequestValidator.cs`:

```csharp
public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("El token es obligatorio");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("La nueva contraseña es obligatoria")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres");
    }
}
```

### 7. Service Interface

**File:** `src/Abuvi.API/Features/Auth/IAuthService.cs` — add:

```csharp
Task ForgotPasswordAsync(string email, CancellationToken ct);
Task ResetPasswordAsync(string token, string newPassword, CancellationToken ct);
```

### 8. Service Implementation

**File:** `src/Abuvi.API/Features/Auth/AuthService.cs` — add two methods:

```csharp
public virtual async Task ForgotPasswordAsync(string email, CancellationToken ct)
{
    var user = await _usersRepository.GetByEmailAsync(email, ct);

    if (user is null || !user.IsActive)
    {
        // Always log but never reveal whether user exists
        _logger.LogInformation("Password reset requested for {Email} (user not found or inactive)", email);
        return;
    }

    var resetToken = GenerateVerificationToken(); // reuse existing private method
    var tokenExpiry = DateTime.UtcNow.AddHours(1);

    user.PasswordResetToken = resetToken;
    user.PasswordResetTokenExpiry = tokenExpiry;
    user.UpdatedAt = DateTime.UtcNow;

    await _usersRepository.UpdateAsync(user, ct);
    await _emailService.SendPasswordResetEmailAsync(user.Email, user.FirstName, resetToken, ct);

    _logger.LogInformation("Password reset email sent to {Email}", email);
}

public virtual async Task ResetPasswordAsync(string token, string newPassword, CancellationToken ct)
{
    var user = await _usersRepository.GetByPasswordResetTokenAsync(token, ct)
        ?? throw new BusinessRuleException("El enlace de recuperación es inválido o ha expirado");

    if (user.PasswordResetTokenExpiry < DateTime.UtcNow)
        throw new BusinessRuleException("El enlace de recuperación es inválido o ha expirado");

    user.PasswordHash = _passwordHasher.HashPassword(newPassword);
    user.PasswordResetToken = null;
    user.PasswordResetTokenExpiry = null;
    user.UpdatedAt = DateTime.UtcNow;

    await _usersRepository.UpdateAsync(user, ct);

    _logger.LogInformation("Password reset completed for user {UserId}", user.Id);
}
```

### 9. Endpoints

**File:** `src/Abuvi.API/Features/Auth/AuthEndpoints.cs` — add inside `MapAuthEndpoints()`:

```csharp
group.MapPost("/forgot-password", ForgotPassword)
    .AddEndpointFilter<ValidationFilter<ForgotPasswordRequest>>()
    .WithName("ForgotPassword")
    .Produces<ApiResponse<object>>()
    .Produces(StatusCodes.Status400BadRequest);

group.MapPost("/reset-password", ResetPassword)
    .AddEndpointFilter<ValidationFilter<ResetPasswordRequest>>()
    .WithName("ResetPassword")
    .Produces<ApiResponse<object>>()
    .Produces(StatusCodes.Status400BadRequest);
```

Handler implementations:

```csharp
private static async Task<IResult> ForgotPassword(
    [FromBody] ForgotPasswordRequest request,
    IAuthService authService,
    CancellationToken cancellationToken)
{
    await authService.ForgotPasswordAsync(request.Email, cancellationToken);
    return Results.Ok(ApiResponse<object>.Ok(new { message = "Si tu correo está registrado, recibirás un enlace para restablecer tu contraseña." }));
}

private static async Task<IResult> ResetPassword(
    [FromBody] ResetPasswordRequest request,
    IAuthService authService,
    CancellationToken cancellationToken)
{
    try
    {
        await authService.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken);
        return Results.Ok(ApiResponse<object>.Ok(new { message = "Contraseña restablecida exitosamente." }));
    }
    catch (BusinessRuleException ex)
    {
        return Results.Json(
            ApiResponse<object>.Fail(ex.Message, "INVALID_OR_EXPIRED_TOKEN"),
            statusCode: 400
        );
    }
}
```

---

## Frontend Implementation

### 1. Auth API Service

**File:** `frontend/src/services/auth.service.ts` (or wherever API calls are made) — add:

```typescript
forgotPassword(email: string): Promise<ApiResponse<void>>
resetPassword(token: string, newPassword: string): Promise<ApiResponse<void>>
```

### 2. Router

**File:** `frontend/src/router/index.ts` — add two public routes (no `requiresAuth`):

```typescript
{
  path: '/forgot-password',
  name: 'forgot-password',
  component: () => import('@/views/ForgotPasswordPage.vue'),
  meta: { requiresAuth: false, title: 'ABUVI | Recuperar Contraseña' }
},
{
  path: '/reset-password',
  name: 'reset-password',
  component: () => import('@/views/ResetPasswordPage.vue'),
  meta: { requiresAuth: false, title: 'ABUVI | Nueva Contraseña' }
},
```

These routes must be **excluded from the authenticated redirect** in the route guard (users who are already logged in should still be able to access these if needed, though it is low priority).

### 3. LoginForm.vue — Fix the Link

**File:** `frontend/src/components/auth/LoginForm.vue:105`

Change:

```html
<a href="#" class="text-sm text-primary-600 hover:text-primary-700">
  ¿Olvidaste tu contraseña?
</a>
```

To:

```html
<RouterLink to="/forgot-password" class="text-sm text-primary-600 hover:text-primary-700">
  ¿Olvidaste tu contraseña?
</RouterLink>
```

### 4. ForgotPasswordPage.vue

**File:** `frontend/src/views/ForgotPasswordPage.vue`

- Single email input field (PrimeVue `InputText`)
- Submit button: "Enviar enlace de recuperación" (PrimeVue `Button`)
- Client validation: email required + valid format
- On success: show Message component with generic text: _"Si tu correo está registrado, recibirás un enlace para restablecer tu contraseña."_
- Link: "Volver al inicio de sesión" → navigates to `/`
- Use the same layout/styling as `AuthContainer.vue` (centered card)

### 5. ResetPasswordPage.vue

**File:** `frontend/src/views/ResetPasswordPage.vue`

- On `mounted`, read `token` from `route.query.token`. If absent → show error and display "Volver" link.
- Two fields: "Nueva contraseña" and "Confirmar contraseña" (PrimeVue `Password` with `toggle-mask`, no feedback meter)
- Submit button: "Restablecer Contraseña"
- Client validation: both required, match, min 8 chars
- On success: show success Message + link "Iniciar Sesión" → `/`
- On backend 400: show error Message: _"El enlace de recuperación es inválido o ha expirado."_

---

## Unit Tests

**File:** `tests/Abuvi.Tests/Features/Auth/`

Follow TDD (Red-Green-Refactor). Write failing tests first.

### ForgotPassword tests (`ForgotPasswordTests.cs`)

- `ForgotPassword_WhenUserNotFound_DoesNotThrow_AndDoesNotSendEmail`
- `ForgotPassword_WhenUserInactive_DoesNotSendEmail`
- `ForgotPassword_WhenUserExists_SavesResetToken_WithOneHourExpiry`
- `ForgotPassword_WhenUserExists_SendsPasswordResetEmail`
- `ForgotPassword_WhenUserExists_LogsInformation`

### ResetPassword tests (`ResetPasswordTests.cs`)

- `ResetPassword_WhenTokenNotFound_ThrowsBusinessRuleException`
- `ResetPassword_WhenTokenExpired_ThrowsBusinessRuleException`
- `ResetPassword_WhenTokenValid_UpdatesPasswordHash`
- `ResetPassword_WhenTokenValid_ClearsResetToken`
- `ResetPassword_WhenTokenValid_LogsCompletion`

---

## Non-Functional Requirements

| Requirement | Detail |
|---|---|
| **Security** | `ForgotPassword` always returns 200 (no user enumeration) |
| **Security** | Token is one-time use, cleared after `ResetPassword` succeeds |
| **Security** | Token expiry: 1 hour (matching email template copy) |
| **Security** | Password hashed with BCrypt (work factor 12) — same as registration |
| **Security** | Audit log on both `ForgotPasswordAsync` and `ResetPasswordAsync` |
| **Validation** | New password minimum 8 characters (server + client) |
| **DB** | EF Core migration required for `password_reset_token` and `password_reset_token_expiry` columns |
| **Internationalisation** | All user-facing messages and email content in Spanish |

---

## Files to Create / Modify

| Action | File |
|---|---|
| Modify | `frontend/src/components/auth/LoginForm.vue` |
| Create | `frontend/src/views/ForgotPasswordPage.vue` |
| Create | `frontend/src/views/ResetPasswordPage.vue` |
| Modify | `frontend/src/router/index.ts` |
| Modify | `frontend/src/services/auth.service.ts` (or equivalent) |
| Modify | `src/Abuvi.API/Features/Users/UsersModels.cs` |
| Modify | `src/Abuvi.API/Data/Configurations/UserConfiguration.cs` |
| Modify | `src/Abuvi.API/Features/Users/IUsersRepository.cs` |
| Modify | `src/Abuvi.API/Features/Users/UsersRepository.cs` |
| Modify | `src/Abuvi.API/Features/Auth/AuthModels.cs` |
| Modify | `src/Abuvi.API/Features/Auth/IAuthService.cs` |
| Modify | `src/Abuvi.API/Features/Auth/AuthService.cs` |
| Modify | `src/Abuvi.API/Features/Auth/AuthEndpoints.cs` |
| Create | `src/Abuvi.API/Features/Auth/ForgotPasswordRequestValidator.cs` |
| Create | `src/Abuvi.API/Features/Auth/ResetPasswordRequestValidator.cs` |
| Create | EF Core migration (`AddPasswordResetTokenToUsers`) |
| Create | `tests/Abuvi.Tests/Features/Auth/ForgotPasswordTests.cs` |
| Create | `tests/Abuvi.Tests/Features/Auth/ResetPasswordTests.cs` |

---

## Definition of Done

- [ ] "¿Olvidaste tu contraseña?" link navigates to `/forgot-password`
- [ ] Forgot password page sends email and shows generic success message
- [ ] Reset password page reads token from query string, validates it, and sets new password
- [ ] Backend always returns 200 for `forgot-password` (no user enumeration)
- [ ] Backend validates token expiry (1 hour) and rejects expired/invalid tokens with `INVALID_OR_EXPIRED_TOKEN`
- [ ] Token is cleared after successful password reset (one-time use)
- [ ] DB migration applied with `password_reset_token` and `password_reset_token_expiry` columns
- [ ] All unit tests pass (TDD approach followed)
- [ ] No regression on existing login / register / verify-email flows
