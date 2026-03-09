# feat-welcome-email-bcc-junta

## User Story

**As a** member of the Junta (board),
**I want to** receive a BCC copy of the welcome email when a new user verifies their account,
**so that** the board is automatically notified of every new user registration without additional manual tracking.

Additionally, the email subject should include the user's full name (first name + last name) so that emails are not grouped/threaded together in Gmail's inbox.

## Problem

Currently, when a new user registers and verifies their email, a welcome email (`"¡Bienvenido a Abuvi!"`) is sent only to the user. The Junta has no visibility into new registrations unless they manually check the admin panel. Also, since all welcome emails share the same subject line, Gmail groups them into a single conversation thread, making it harder to track individual registrations.

## Proposed Solution

1. **Add BCC to the welcome email**: Send a BCC copy of the welcome email to `junta.abuvi@gmail.com`.
2. **Personalize the email subject**: Include the user's full name in the subject line (e.g., `"¡Bienvenido/a a Abuvi! — Juan García López"`) to prevent Gmail conversation threading.

## Technical Details

### Files to Modify

#### 1. `src/Abuvi.API/Common/Services/IEmailService.cs`

- **Update** `SendWelcomeEmailAsync` signature to accept `lastName` parameter:

  ```csharp
  Task SendWelcomeEmailAsync(
      string toEmail,
      string firstName,
      string lastName,
      CancellationToken ct);
  ```

#### 2. `src/Abuvi.API/Common/Services/ResendEmailService.cs`

- **Update** `SendWelcomeEmailAsync` method (lines 96-151):
  - Add `string lastName` parameter
  - Change subject from `"¡Bienvenido a Abuvi!"` to `$"¡Bienvenido/a a Abuvi! — {firstName} {lastName}"` (with proper HTML encoding for the subject)
  - Add BCC to the `EmailMessage`:

    ```csharp
    Bcc = "junta.abuvi@gmail.com"
    ```

  - The BCC email address should be read from configuration (`Resend:BoardBccEmail`) with fallback default `"junta.abuvi@gmail.com"`, following the existing pattern for `_fromEmail` and `_fromName`.

- **Add** a new field `_boardBccEmail` in the constructor:

  ```csharp
  _boardBccEmail = configuration["Resend:BoardBccEmail"] ?? "junta.abuvi@gmail.com";
  ```

#### 3. `src/Abuvi.API/Features/Auth/AuthService.cs`

- **Update** the call to `SendWelcomeEmailAsync` at line 179 to pass `user.LastName`:

  ```csharp
  await _emailService.SendWelcomeEmailAsync(user.Email, user.FirstName, user.LastName, ct);
  ```

#### 4. `src/Abuvi.Tests/Unit/Common/Services/ResendEmailServiceTests.cs`

- **Update** existing welcome email tests to pass the new `lastName` parameter
- **Add** new test: `SendWelcomeEmailAsync_IncludesBccToBoard` — Verify BCC is set to the configured board email
- **Add** new test: `SendWelcomeEmailAsync_SubjectIncludesFullName` — Verify subject contains first name and last name

#### 5. `src/Abuvi.Tests/Unit/Features/Auth/AuthServiceTests_Registration.cs`

- **Update** any mocks/verifications for `SendWelcomeEmailAsync` to include the `lastName` parameter

### Configuration

- New optional config key: `Resend:BoardBccEmail` (default: `junta.abuvi@gmail.com`)
- No infrastructure changes required — the Resend SDK `EmailMessage.Bcc` property already supports BCC natively

### Email Subject Format

- **Current**: `"¡Bienvenido a Abuvi!"`
- **New**: `"¡Bienvenido/a a Abuvi! — {firstName} {lastName}"`
- Example: `"¡Bienvenido/a a Abuvi! — María García López"`

### API Endpoint

No endpoint changes needed. The change is internal to the email service, triggered during the existing email verification flow:

- `POST /api/auth/verify-email?token={token}` → `AuthService.VerifyEmailAsync()` → `SendWelcomeEmailAsync()`

## Acceptance Criteria

- [ ] When a user verifies their email, the welcome email is sent with BCC to `junta.abuvi@gmail.com`
- [ ] The email subject includes the user's full name (first and last name)
- [ ] The BCC email address is configurable via `Resend:BoardBccEmail` configuration key
- [ ] Existing welcome email content and behavior remains unchanged for the primary recipient
- [ ] Unit tests cover the new BCC and subject line behavior
- [ ] All existing tests pass with the updated method signature

## Non-Functional Requirements

- **Security**: The BCC address is not exposed to the user; Resend API handles BCC natively at the SMTP level
- **Performance**: No impact — a single API call to Resend handles both To and BCC recipients
- **Reliability**: BCC failure should not block the welcome email from being sent to the user (Resend handles this atomically)

## Out of Scope

- Adding BCC to other email types (verification, password reset, camp registration, etc.)
- Admin UI to configure the BCC address (configuration-only for now)
- Tracking/logging of BCC delivery status
