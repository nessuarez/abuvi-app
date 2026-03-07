# Camp Registration Email Notification — Enriched User Story

## Overview

Send an email notification to the family representative when a camp registration is created or cancelled, using the existing Resend integration. The current `SendCampRegistrationConfirmationAsync` method exists but is **not wired** into the registration flow and has an incomplete template. This story connects the email trigger, enriches the template with full registration details, and adds a cancellation notification.

## Business Context

When a family completes the camp registration wizard, they currently receive no email confirmation. This creates uncertainty — families don't know if the registration went through successfully. Similarly, if a registration is cancelled, there is no notification.

**Goal:** Families receive a clear, Spanish-language email with a full summary of their registration immediately after completing it, and a notification if it is cancelled.

## Current State Analysis

### What already exists

| Component | File | Status |
|-----------|------|--------|
| `IEmailService` interface | `src/Abuvi.API/Common/Services/IEmailService.cs` | Has `SendCampRegistrationConfirmationAsync` (limited params) |
| `ResendEmailService` implementation | `src/Abuvi.API/Common/Services/ResendEmailService.cs` | Template exists but in English, limited data |
| `IResendClient` wrapper | `src/Abuvi.API/Common/Services/IResendClient.cs` | Fully functional |
| `ResendClientWrapper` | `src/Abuvi.API/Common/Services/ResendClientWrapper.cs` | Wraps Resend SDK |
| Resend config in `appsettings.json` | `src/Abuvi.API/appsettings.json` | Configured (ApiKey, FromEmail, FromName) |
| DI registration in `Program.cs` | `src/Abuvi.API/Program.cs` (lines 193-211) | `IEmailService` → `ResendEmailService` registered |
| Unit tests | `src/Abuvi.Tests/Unit/Common/Services/ResendEmailServiceTests.cs` | Exists for other email types |
| Integration tests | `src/Abuvi.Tests/Integration/Common/Services/ResendEmailIntegrationTests.cs` | Exists for other email types |

### What is missing

1. **Trigger**: `RegistrationsService.CreateAsync()` (line ~144) does NOT call the email service after saving
2. **Trigger**: `RegistrationsService.CancelAsync()` does NOT send cancellation email
3. **Template quality**: Current template is English-only, shows only camp name + start date
4. **Data richness**: Current method signature only accepts `(toEmail, firstName, campName, campStartDate)` — no members, prices, extras, accommodation prefs

## Technical Requirements

### 1. New/Updated Interface Methods

**File:** `src/Abuvi.API/Common/Services/IEmailService.cs`

Replace the existing `SendCampRegistrationConfirmationAsync` with an enriched version and add a cancellation method:

```csharp
/// <summary>
/// Sends a camp registration confirmation email with full details
/// </summary>
Task SendCampRegistrationConfirmationAsync(
    CampRegistrationEmailData data,
    CancellationToken ct);

/// <summary>
/// Sends a camp registration cancellation notification
/// </summary>
Task SendCampRegistrationCancellationAsync(
    CampRegistrationEmailData data,
    CancellationToken ct);
```

### 2. New DTO for Email Data

**File:** `src/Abuvi.API/Common/Services/IEmailService.cs` (or a dedicated `EmailModels.cs`)

```csharp
public record CampRegistrationEmailData
{
    public required string ToEmail { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string CampName { get; init; }
    public required string CampLocation { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required int Year { get; init; }
    public required Guid RegistrationId { get; init; }
    public required decimal TotalAmount { get; init; }
    public required decimal BaseTotalAmount { get; init; }
    public required decimal ExtrasAmount { get; init; }
    public required IReadOnlyList<RegistrationMemberEmailData> Members { get; init; }
    public string? SpecialNeeds { get; init; }
    public string? CampatesPreference { get; init; }
}

public record RegistrationMemberEmailData
{
    public required string FullName { get; init; }
    public required string AgeCategory { get; init; }   // "Adulto", "Nino", "Bebe"
    public required int AgeAtCamp { get; init; }
    public required string AttendancePeriod { get; init; } // "Completo", "1a Semana", etc.
    public required decimal IndividualAmount { get; init; }
}
```

### 3. Updated Email Template (Spanish)

**File:** `src/Abuvi.API/Common/Services/ResendEmailService.cs`

Update `SendCampRegistrationConfirmationAsync` with a Spanish template including:

- Subject: `Inscripcion confirmada — Campamento {Year}`
- Header: camp name, location, dates
- Member list: name, age category, attendance period, individual price
- Pricing summary: base amount + extras = total
- Special needs and preferences (if provided)
- CTA button: "Ver mi inscripcion" linking to `/registrations/{id}`
- Footer: ABUVI branding

Add `SendCampRegistrationCancellationAsync` with:

- Subject: `Inscripcion cancelada — Campamento {Year}`
- Header: camp name, dates
- Brief message confirming cancellation
- CTA button: "Ver campamento" linking to `/camp`

### 4. Wire Up Email Trigger in Registration Service

**File:** `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`

**4.1. Inject `IEmailService`:**

Add to the primary constructor parameters:

```csharp
public class RegistrationsService(
    IRegistrationsRepository registrationsRepo,
    // ... existing params ...
    IEmailService emailService,               // ADD
    ILogger<RegistrationsService> logger)
```

**4.2. Send email after registration creation (line ~155, after reload):**

```csharp
// 12. Reload and return
var detailed = await registrationsRepo.GetByIdWithDetailsAsync(registration.Id, ct)
    ?? throw new NotFoundException("Inscripcion", registration.Id);

// 13. Send confirmation email (fire-and-forget, non-blocking)
await SendRegistrationConfirmationEmailAsync(detailed, edition, representative, ct);

return detailed.ToResponse(amountPaid: 0m);
```

**4.3. Send email after cancellation:**

In `CancelAsync()`, after updating the status, send a cancellation email.

**4.4. Private helper to build email data:**

```csharp
private async Task SendRegistrationConfirmationEmailAsync(
    Registration registration,
    CampEdition edition,
    User representative,
    CancellationToken ct)
{
    try
    {
        var emailData = BuildEmailData(registration, edition, representative);
        await emailService.SendCampRegistrationConfirmationAsync(emailData, ct);
    }
    catch (Exception ex)
    {
        // Log but do NOT fail the registration
        logger.LogError(ex,
            "Failed to send registration confirmation email for {RegistrationId}",
            registration.Id);
    }
}
```

**Critical:** Email sending must be **non-blocking**. If the email fails, the registration should still succeed. Wrap in try-catch and log the error.

### 5. Data Retrieval

The `detailed` registration object loaded via `GetByIdWithDetailsAsync` already includes:

- `Registration.Members` (with `FamilyMember` navigation)
- `Registration.CampEdition` (with `Camp` navigation)
- `Registration.RegisteredByUser`

No additional repository queries are needed — all data is available from the existing reload.

The representative user's email comes from `Registration.RegisteredByUser.Email`.

### 6. Files to Modify

| File | Action |
|------|--------|
| `src/Abuvi.API/Common/Services/IEmailService.cs` | Add `CampRegistrationEmailData` DTO, update method signatures |
| `src/Abuvi.API/Common/Services/ResendEmailService.cs` | Update confirmation template (Spanish, rich data), add cancellation template |
| `src/Abuvi.API/Features/Registrations/RegistrationsService.cs` | Inject `IEmailService`, trigger email after create/cancel |
| `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs` | Add tests for email trigger (verify called, verify non-blocking on failure) |
| `src/Abuvi.Tests/Unit/Common/Services/ResendEmailServiceTests.cs` | Add tests for new/updated email methods |

### 7. Unit Tests

#### 7.1. RegistrationsService Tests

**File:** `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs`

New test cases:

```
CreateAsync_WhenSuccessful_SendsConfirmationEmail
CreateAsync_WhenEmailFails_StillReturnsRegistration
CreateAsync_WhenEmailFails_LogsError
CancelAsync_WhenSuccessful_SendsCancellationEmail
CancelAsync_WhenEmailFails_StillCancelsRegistration
```

**Pattern:**

- Mock `IEmailService` with NSubstitute
- Verify `SendCampRegistrationConfirmationAsync` received the call with correct data
- For failure test: configure mock to throw, assert registration is still returned

#### 7.2. ResendEmailService Tests

**File:** `src/Abuvi.Tests/Unit/Common/Services/ResendEmailServiceTests.cs`

New test cases:

```
SendCampRegistrationConfirmationAsync_WithValidData_SendsEmail
SendCampRegistrationConfirmationAsync_IncludesAllMembersInBody
SendCampRegistrationConfirmationAsync_IncludesPricingSummary
SendCampRegistrationConfirmationAsync_SubjectContainsCampYear
SendCampRegistrationConfirmationAsync_WhenResendFails_ThrowsInvalidOperationException
SendCampRegistrationCancellationAsync_WithValidData_SendsEmail
SendCampRegistrationCancellationAsync_WhenResendFails_ThrowsInvalidOperationException
```

### 8. Non-Functional Requirements

#### Security

- Do NOT include sensitive data in emails (medical notes, allergies, document numbers)
- Sanitize user-provided content (notes, special needs) before embedding in HTML
- Only send to the registered user's verified email

#### Performance

- Email sending is synchronous but wrapped in non-blocking try-catch
- Future enhancement: move to background queue if volume grows
- No additional DB queries — reuse already-loaded data

#### RGPD Compliance

- Transactional emails do not require consent (legitimate interest for service delivery)
- No tracking pixels or marketing content
- Email contains only data the user submitted themselves

#### Error Handling

- Email failures must NOT block the registration/cancellation flow
- All failures logged with structured logging at `Error` level
- Include `RegistrationId` in all log entries for traceability

### 9. Acceptance Criteria

1. After completing the registration wizard, the family representative receives a confirmation email within 1 minute
2. The email is in Spanish and includes: camp name, dates, location, member list with prices, total amount, and a link to view the registration
3. If the email fails to send, the registration still completes successfully and the error is logged
4. After cancelling a registration, the representative receives a cancellation notification
5. All new code has unit tests following TDD (write tests first)
6. Existing email service tests continue to pass
7. No sensitive data (medical notes, document numbers) appears in the email

### 10. Implementation Steps (TDD)

1. **Write failing tests** for `RegistrationsService` — verify email is called after create/cancel
2. **Inject `IEmailService`** into `RegistrationsService` constructor
3. **Define `CampRegistrationEmailData`** DTO in the email service
4. **Update `IEmailService`** interface with new method signatures
5. **Write failing tests** for `ResendEmailService` — verify template content
6. **Implement updated `SendCampRegistrationConfirmationAsync`** with Spanish template and rich data
7. **Implement `SendCampRegistrationCancellationAsync`**
8. **Wire up triggers** in `RegistrationsService.CreateAsync()` and `CancelAsync()`
9. **Run all tests** — verify green
10. **Manual test** — create a registration and verify email arrives in Resend dashboard

### 11. Out of Scope

- Email to multiple family members (only representative for now)
- Email when registration members/extras are updated (future enhancement)
- Email localization (all emails in Spanish for now)
- Background email queue (synchronous is acceptable for current volume)
- Resend webhooks for delivery tracking

## Dependencies

- Resend API key must be configured (already in place via user-secrets)
- Resend domain verification for production (`abuvi.org`)

## Related Specs

- [Resend Integration](../merged/feat-resend-integration/resend-integration_enriched.md) — Base Resend setup
- [Camp Registration](../merged/feat-camps-registration/feat-camps-registration_enriched.md) — Registration flow
- [Backend Standards](../../specs/backend-standards.mdc) — Coding conventions

---

**Story Status:** Ready for development
**Priority:** High
**Estimated Effort:** 1-2 days
**Dependencies:** Existing Resend integration (already merged)
