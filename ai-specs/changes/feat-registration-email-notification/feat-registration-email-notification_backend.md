# Backend Implementation Plan: feat-registration-email-notification — Camp Registration Email Notification

## Overview

Send Spanish-language email notifications to the family representative when a camp registration is created or cancelled. The existing `SendCampRegistrationConfirmationAsync` method in `ResendEmailService` exists but is **not wired** into the registration flow, is in English, and has a limited data signature. This plan enriches the email template, adds a cancellation notification, and wires both into `RegistrationsService`.

**Architecture:** Vertical Slice Architecture — changes span the `Common/Services` cross-cutting layer (email) and the `Registrations` feature slice.

## Architecture Context

### Feature Slices Involved

| Slice | Path |
|-------|------|
| Registrations | `src/Abuvi.API/Features/Registrations/` |
| Common Services | `src/Abuvi.API/Common/Services/` |

### Files to Create/Modify

| File | Action |
|------|--------|
| `src/Abuvi.API/Common/Services/IEmailService.cs` | Add `CampRegistrationEmailData` DTO, add `SendCampRegistrationCancellationAsync`, update `SendCampRegistrationConfirmationAsync` signature |
| `src/Abuvi.API/Common/Services/ResendEmailService.cs` | Rewrite confirmation template (Spanish, rich data), add cancellation template |
| `src/Abuvi.API/Features/Registrations/RegistrationsService.cs` | Inject `IEmailService`, trigger email after create/cancel |
| `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs` | Add email trigger tests |
| `src/Abuvi.Tests/Unit/Common/Services/ResendEmailServiceTests.cs` | Add/update tests for confirmation & cancellation templates |

### Cross-Cutting Concerns

- No new middleware or filters needed
- No schema changes — no EF Core migration
- No new NuGet packages

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to `feature/feat-registration-email-notification-backend` from `dev`
- **Implementation Steps**:
  1. `git checkout dev && git pull origin dev`
  2. `git checkout -b feature/feat-registration-email-notification-backend`
  3. `git branch` — verify active branch

---

### Step 1: Define `CampRegistrationEmailData` DTO

- **File**: `src/Abuvi.API/Common/Services/IEmailService.cs`
- **Action**: Add the email data record and member sub-record at the bottom of the file (before closing namespace if explicit, or at file end)
- **Implementation Steps**:

  1. Add `CampRegistrationEmailData` record:

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
         public required string AgeCategory { get; init; }   // "Adulto", "Niño", "Bebé"
         public required int AgeAtCamp { get; init; }
         public required string AttendancePeriod { get; init; } // "Completo", "1ª Semana", etc.
         public required decimal IndividualAmount { get; init; }
     }
     ```

  2. These records live in the same file as `IEmailService` for cohesion (they are only used by the email layer).

- **Notes**:
  - Use `required` + `init` pattern consistent with the project's DTO style
  - `AgeCategory` and `AttendancePeriod` are strings (Spanish display values), not enums — the mapping happens in the caller (RegistrationsService)
  - `CampLocation` is nullable in the Camp entity (`string? Location`), so the builder must handle `null` with a fallback like `"Sin ubicación"`

---

### Step 2: Update `IEmailService` Interface Methods

- **File**: `src/Abuvi.API/Common/Services/IEmailService.cs`
- **Action**: Replace the existing `SendCampRegistrationConfirmationAsync` signature and add `SendCampRegistrationCancellationAsync`
- **Implementation Steps**:

  1. **Replace** the existing method (lines 45-50):

     ```csharp
     // OLD:
     Task SendCampRegistrationConfirmationAsync(
         string toEmail, string firstName, string campName, DateTime campStartDate, CancellationToken ct);

     // NEW:
     /// <summary>
     /// Sends a camp registration confirmation email with full details
     /// </summary>
     Task SendCampRegistrationConfirmationAsync(
         CampRegistrationEmailData data,
         CancellationToken ct);
     ```

  2. **Add** new cancellation method in the Camp Management section:

     ```csharp
     /// <summary>
     /// Sends a camp registration cancellation notification
     /// </summary>
     Task SendCampRegistrationCancellationAsync(
         CampRegistrationEmailData data,
         CancellationToken ct);
     ```

- **Notes**:
  - This is a **breaking change** to the interface — `ResendEmailService` must be updated in the same commit (Step 3)
  - No other callers exist for the old `SendCampRegistrationConfirmationAsync` (it was never wired), so no other files break

---

### Step 3: Implement Updated `SendCampRegistrationConfirmationAsync` in `ResendEmailService`

- **File**: `src/Abuvi.API/Common/Services/ResendEmailService.cs`
- **Action**: Replace the existing confirmation method (lines 210-268) with the enriched Spanish template
- **Implementation Steps**:

  1. **Replace** the method signature to accept `CampRegistrationEmailData data` + `CancellationToken ct`

  2. **Build the Spanish HTML template** with these sections:
     - **Subject**: `$"Inscripción confirmada — Campamento {data.Year}"`
     - **Header**: Green checkmark, "¡Inscripción Confirmada!" title
     - **Greeting**: `Hola {data.RecipientFirstName},`
     - **Camp details block** (grey background box):
       - Campamento: `{data.CampName}`
       - Ubicación: `{data.CampLocation}`
       - Fechas: `{data.StartDate:d 'de' MMMM} — {data.EndDate:d 'de' MMMM 'de' yyyy}` (use Spanish CultureInfo `new CultureInfo("es-ES")`)
     - **Members table**: For each member in `data.Members`:
       - Name | Age category | Attendance period | Amount (€)
     - **Pricing summary**:
       - Base inscripción: `{data.BaseTotalAmount:N2} €`
       - Extras: `{data.ExtrasAmount:N2} €` (only if > 0)
       - **Total: `{data.TotalAmount:N2} €`**
     - **Special needs** (if not null/empty): "Necesidades especiales: {data.SpecialNeeds}"
     - **Campmates preference** (if not null/empty): "Preferencia de compañeros: {data.CampatesPreference}"
     - **CTA button**: "Ver mi inscripción" → `{_frontendUrl}/registrations/{data.RegistrationId}`
     - **Footer**: ABUVI branding, same style as existing emails

  3. **HTML sanitization**: Use `System.Net.WebUtility.HtmlEncode()` on user-provided strings:
     - `data.SpecialNeeds`
     - `data.CampatesPreference`
     - `data.RecipientFirstName`
     - Member `FullName`

  4. **Try/catch pattern**: Same as existing methods — log success with messageId, throw `InvalidOperationException` on failure

  5. **Date formatting**: Use `CultureInfo("es-ES")` for Spanish month names. Add `using System.Globalization;` if not already present.

- **Notes**:
  - Keep the same error handling pattern as `SendPasswordResetEmailAsync` and other existing methods
  - The `ct` parameter is not currently used by the Resend SDK call but pass it through for future compatibility
  - **Security**: Never include `GuardianDocumentNumber`, medical notes, or allergy info in emails

---

### Step 4: Implement `SendCampRegistrationCancellationAsync` in `ResendEmailService`

- **File**: `src/Abuvi.API/Common/Services/ResendEmailService.cs`
- **Action**: Add a new method for cancellation emails
- **Implementation Steps**:

  1. **Add method** after the confirmation method:

     ```csharp
     public async Task SendCampRegistrationCancellationAsync(
         CampRegistrationEmailData data,
         CancellationToken ct)
     ```

  2. **Build Spanish HTML template**:
     - **Subject**: `$"Inscripción cancelada — Campamento {data.Year}"`
     - **Header**: Red styling, "Inscripción Cancelada" title
     - **Greeting**: `Hola {data.RecipientFirstName},`
     - **Body**: "Tu inscripción en el campamento **{data.CampName}** ({StartDate} — {EndDate}) ha sido cancelada."
     - **Note**: "Si esto fue un error o deseas volver a inscribirte, puedes hacerlo desde nuestra web."
     - **CTA button**: "Ver campamento" → `{_frontendUrl}/camp`
     - **Footer**: ABUVI branding

  3. **Same try/catch + logging pattern** as confirmation method

- **Notes**:
  - Cancellation email is simpler — no member list or pricing breakdown needed
  - Same HTML sanitization for user-provided content

---

### Step 5: Wire Email Trigger in `RegistrationsService.CreateAsync()`

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`
- **Action**: Inject `IEmailService` and send confirmation email after registration creation
- **Implementation Steps**:

  1. **Add `IEmailService` to constructor** (primary constructor parameter):

     ```csharp
     public class RegistrationsService(
         IRegistrationsRepository registrationsRepo,
         IRegistrationExtrasRepository extrasRepo,
         IRegistrationAccommodationPreferencesRepository accommodationPrefsRepo,
         IFamilyUnitsRepository familyUnitsRepo,
         ICampEditionsRepository campEditionsRepo,
         ICampEditionAccommodationsRepository accommodationsRepo,
         RegistrationPricingService pricingService,
         IEmailService emailService,                    // ADD
         ILogger<RegistrationsService> logger)
     ```

  2. **Add `using Abuvi.API.Common.Services;`** at the top of the file

  3. **After line 153** (the `GetByIdWithDetailsAsync` reload), before `return`, add:

     ```csharp
     // 13. Send confirmation email (non-blocking on failure)
     try
     {
         var emailData = BuildRegistrationEmailData(detailed, edition);
         await emailService.SendCampRegistrationConfirmationAsync(emailData, ct);
     }
     catch (Exception ex)
     {
         logger.LogError(ex,
             "Failed to send registration confirmation email for {RegistrationId}",
             registration.Id);
     }
     ```

  4. **Add private helper method** `BuildRegistrationEmailData`:

     ```csharp
     private static CampRegistrationEmailData BuildRegistrationEmailData(
         Registration registration, CampEdition edition)
     {
         return new CampRegistrationEmailData
         {
             ToEmail = registration.RegisteredByUser.Email,
             RecipientFirstName = registration.RegisteredByUser.FirstName,
             CampName = edition.Camp.Name,
             CampLocation = edition.Camp.Location ?? "Sin ubicación",
             StartDate = DateOnly.FromDateTime(edition.StartDate),
             EndDate = DateOnly.FromDateTime(edition.EndDate),
             Year = edition.Year,
             RegistrationId = registration.Id,
             TotalAmount = registration.TotalAmount,
             BaseTotalAmount = registration.BaseTotalAmount,
             ExtrasAmount = registration.ExtrasAmount,
             SpecialNeeds = registration.SpecialNeeds,
             CampatesPreference = registration.CampatesPreference,
             Members = registration.Members.Select(m => new RegistrationMemberEmailData
             {
                 FullName = $"{m.FamilyMember.FirstName} {m.FamilyMember.LastName}",
                 AgeCategory = MapAgeCategory(m.AgeCategory),
                 AgeAtCamp = m.AgeAtCamp,
                 AttendancePeriod = MapAttendancePeriod(m.AttendancePeriod),
                 IndividualAmount = m.IndividualAmount
             }).ToList()
         };
     }

     private static string MapAgeCategory(AgeCategory category) => category switch
     {
         AgeCategory.Adult => "Adulto",
         AgeCategory.Child => "Niño",
         AgeCategory.Baby => "Bebé",
         _ => category.ToString()
     };

     private static string MapAttendancePeriod(AttendancePeriod period) => period switch
     {
         AttendancePeriod.Complete => "Completo",
         AttendancePeriod.FirstWeek => "1ª Semana",
         AttendancePeriod.SecondWeek => "2ª Semana",
         AttendancePeriod.WeekendVisit => "Visita fin de semana",
         _ => period.ToString()
     };
     ```

- **Critical Notes**:
  - The `detailed` object from `GetByIdWithDetailsAsync` already includes `RegisteredByUser`, `CampEdition.Camp`, and `Members` with `FamilyMember` — no extra DB queries needed
  - The `edition` variable is available from step 3 of `CreateAsync` (loaded earlier for status check)
  - **Email failure must NOT block registration** — the try-catch ensures the registration returns successfully even if email fails
  - Log at `Error` level with `RegistrationId` for traceability

---

### Step 6: Wire Email Trigger in `RegistrationsService.CancelAsync()`

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`
- **Action**: Send cancellation email after status update in `CancelAsync()`
- **Implementation Steps**:

  1. **After line 376** (`registrationsRepo.UpdateAsync`), **before** the log and return, add:

     ```csharp
     // 5. Send cancellation email (non-blocking on failure)
     try
     {
         var detailed = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
             ?? throw new NotFoundException("Inscripción", registrationId);
         var edition = detailed.CampEdition;
         var emailData = BuildRegistrationEmailData(detailed, edition);
         await emailService.SendCampRegistrationCancellationAsync(emailData, ct);
     }
     catch (Exception ex)
     {
         logger.LogError(ex,
             "Failed to send registration cancellation email for {RegistrationId}",
             registrationId);
     }
     ```

  2. **Note**: `CancelAsync` currently only loads the registration via `GetByIdAsync` (without details). For the cancellation email we need the detailed version (with `RegisteredByUser`, `CampEdition.Camp`). The `GetByIdWithDetailsAsync` call is necessary here.

- **Notes**:
  - Same non-blocking try-catch pattern as confirmation
  - The additional `GetByIdWithDetailsAsync` is only called once and only when cancelling — acceptable for current volume
  - The cancellation email uses the same `BuildRegistrationEmailData` helper, even though it doesn't display all data (the DTO is shared for simplicity)

---

### Step 7: Write Unit Tests for `RegistrationsService` Email Triggers

- **File**: `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs`
- **Action**: Add tests verifying email is called and non-blocking on failure
- **Implementation Steps**:

  1. **Update the SUT constructor** in the test class to include a mock `IEmailService`:

     ```csharp
     private readonly IEmailService _emailService = Substitute.For<IEmailService>();
     ```

     And pass it to the `RegistrationsService` constructor.

  2. **Add test cases**:

     ```
     CreateAsync_WhenSuccessful_SendsConfirmationEmail
     ```

     - Arrange: Set up all mocks for a successful registration creation
     - Act: Call `CreateAsync`
     - Assert: `_emailService.Received(1).SendCampRegistrationConfirmationAsync(Arg.Is<CampRegistrationEmailData>(d => d.ToEmail == expectedEmail && d.RegistrationId != Guid.Empty), Arg.Any<CancellationToken>())`

     ```
     CreateAsync_WhenEmailFails_StillReturnsRegistration
     ```

     - Arrange: Configure `_emailService.SendCampRegistrationConfirmationAsync(...)` to throw `InvalidOperationException`
     - Act: Call `CreateAsync`
     - Assert: Result is not null, registration was created successfully

     ```
     CreateAsync_WhenEmailFails_LogsError
     ```

     - Arrange: Configure email to throw
     - Act: Call `CreateAsync`
     - Assert: Logger received `LogError` call (use NSubstitute logger verification pattern)

     ```
     CancelAsync_WhenSuccessful_SendsCancellationEmail
     ```

     - Arrange: Set up mocks for successful cancellation
     - Act: Call `CancelAsync`
     - Assert: `_emailService.Received(1).SendCampRegistrationCancellationAsync(Arg.Any<CampRegistrationEmailData>(), Arg.Any<CancellationToken>())`

     ```
     CancelAsync_WhenEmailFails_StillCancelsRegistration
     ```

     - Arrange: Configure cancellation email to throw
     - Act: Call `CancelAsync`
     - Assert: Registration status is Cancelled, response message is returned

  3. **Follow existing test patterns**: Look at existing `CreateAsync` tests in the file for mock setup patterns (the tests already set up `registrationsRepo`, `familyUnitsRepo`, `campEditionsRepo`, etc.)

- **Notes**:
  - Use `Arg.Is<CampRegistrationEmailData>(...)` for targeted assertions on email data
  - Use `Arg.Any<CancellationToken>()` for the CT parameter
  - The existing test class uses NSubstitute — follow the same pattern

---

### Step 8: Write Unit Tests for `ResendEmailService` Email Templates

- **File**: `src/Abuvi.Tests/Unit/Common/Services/ResendEmailServiceTests.cs`
- **Action**: Add tests for the new/updated email methods
- **Implementation Steps**:

  1. **Create a helper method** to build test `CampRegistrationEmailData`:

     ```csharp
     private static CampRegistrationEmailData CreateTestRegistrationEmailData() => new()
     {
         ToEmail = "test@example.com",
         RecipientFirstName = "María",
         CampName = "Campamento Sierra",
         CampLocation = "Sierra de Gredos",
         StartDate = new DateOnly(2026, 7, 1),
         EndDate = new DateOnly(2026, 7, 15),
         Year = 2026,
         RegistrationId = Guid.NewGuid(),
         TotalAmount = 450.00m,
         BaseTotalAmount = 400.00m,
         ExtrasAmount = 50.00m,
         SpecialNeeds = "Vegetariano",
         CampatesPreference = "Familia López",
         Members =
         [
             new()
             {
                 FullName = "Juan Pérez",
                 AgeCategory = "Adulto",
                 AgeAtCamp = 35,
                 AttendancePeriod = "Completo",
                 IndividualAmount = 200.00m
             },
             new()
             {
                 FullName = "Ana Pérez",
                 AgeCategory = "Niño",
                 AgeAtCamp = 8,
                 AttendancePeriod = "Completo",
                 IndividualAmount = 200.00m
             }
         ]
     };
     ```

  2. **Add test cases** (update existing confirmation test + add new ones):

     ```
     SendCampRegistrationConfirmationAsync_WithValidData_SendsEmail
     ```

     - Verify `_resendClient.Received(1).SendEmailAsync(Arg.Any<EmailMessage>())`

     ```
     SendCampRegistrationConfirmationAsync_IncludesAllMembersInBody
     ```

     - Assert HTML body contains "Juan Pérez" and "Ana Pérez"

     ```
     SendCampRegistrationConfirmationAsync_IncludesPricingSummary
     ```

     - Assert HTML body contains "400,00", "50,00", "450,00"

     ```
     SendCampRegistrationConfirmationAsync_SubjectContainsCampYear
     ```

     - Assert subject contains "2026" and "Inscripción confirmada"

     ```
     SendCampRegistrationConfirmationAsync_SanitizesUserInput
     ```

     - Use `SpecialNeeds = "<script>alert('xss')</script>"` — verify HTML body does NOT contain raw `<script>` but contains encoded version

     ```
     SendCampRegistrationConfirmationAsync_WhenResendFails_ThrowsInvalidOperationException
     ```

     - Configure `_resendClient.SendEmailAsync(...)` to throw
     - Assert throws `InvalidOperationException`

     ```
     SendCampRegistrationCancellationAsync_WithValidData_SendsEmail
     ```

     - Verify email sent with correct subject

     ```
     SendCampRegistrationCancellationAsync_SubjectContainsCampYear
     ```

     - Assert subject contains "cancelada" and year

     ```
     SendCampRegistrationCancellationAsync_WhenResendFails_ThrowsInvalidOperationException
     ```

  3. **Update existing test** if there was one for the old `SendCampRegistrationConfirmationAsync` signature — remove/replace it

- **Notes**:
  - Follow existing test class patterns (arrange _configuration,_resendClient mocks in constructor)
  - Use `Arg.Is<EmailMessage>(m => m.HtmlBody!.Contains("..."))` for body assertions
  - FluentAssertions: `act.Should().ThrowAsync<InvalidOperationException>()`

---

### Step 9: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. **Review Changes**: Analyze all code changes
  2. **Update `ai-specs/specs/api-spec.yml`**: No endpoint changes, but document the email side-effect in the POST `/api/registrations` and POST `/api/registrations/{id}/cancel` endpoint descriptions
  3. **Update `ai-specs/specs/data-model.md`**: No schema changes needed — only mention the `CampRegistrationEmailData` DTO if the doc tracks DTOs
  4. **No changes needed to backend-standards**: No new patterns introduced
  5. **Verify**: All documentation in English, consistent with existing structure

---

## Implementation Order

1. **Step 0**: Create feature branch `feature/feat-registration-email-notification-backend`
2. **Step 1**: Define `CampRegistrationEmailData` and `RegistrationMemberEmailData` DTOs
3. **Step 2**: Update `IEmailService` interface (new signatures)
4. **Step 3**: Implement enriched Spanish confirmation template in `ResendEmailService`
5. **Step 4**: Implement cancellation template in `ResendEmailService`
6. **Step 5**: Wire email trigger in `RegistrationsService.CreateAsync()`
7. **Step 6**: Wire email trigger in `RegistrationsService.CancelAsync()`
8. **Step 7**: Write `RegistrationsService` email trigger tests
9. **Step 8**: Write `ResendEmailService` template tests
10. **Step 9**: Update technical documentation

## Testing Checklist

- [ ] All existing `ResendEmailServiceTests` still pass (old confirmation test updated)
- [ ] All existing `RegistrationsServiceTests` still pass (constructor updated with new mock)
- [ ] `CreateAsync_WhenSuccessful_SendsConfirmationEmail` — green
- [ ] `CreateAsync_WhenEmailFails_StillReturnsRegistration` — green
- [ ] `CreateAsync_WhenEmailFails_LogsError` — green
- [ ] `CancelAsync_WhenSuccessful_SendsCancellationEmail` — green
- [ ] `CancelAsync_WhenEmailFails_StillCancelsRegistration` — green
- [ ] `SendCampRegistrationConfirmationAsync_WithValidData_SendsEmail` — green
- [ ] `SendCampRegistrationConfirmationAsync_IncludesAllMembersInBody` — green
- [ ] `SendCampRegistrationConfirmationAsync_IncludesPricingSummary` — green
- [ ] `SendCampRegistrationConfirmationAsync_SubjectContainsCampYear` — green
- [ ] `SendCampRegistrationConfirmationAsync_SanitizesUserInput` — green
- [ ] `SendCampRegistrationConfirmationAsync_WhenResendFails_ThrowsInvalidOperationException` — green
- [ ] `SendCampRegistrationCancellationAsync_WithValidData_SendsEmail` — green
- [ ] `SendCampRegistrationCancellationAsync_WhenResendFails_ThrowsInvalidOperationException` — green
- [ ] `dotnet build` — no warnings
- [ ] `dotnet test` — all green

## Error Response Format

No new API error responses. Email failures are caught internally and logged — they do not propagate to the API response. The existing `ApiResponse<T>` envelope is unaffected.

| Scenario | HTTP Status | Behavior |
|----------|-------------|----------|
| Registration created, email sent | 201 | Normal response |
| Registration created, email failed | 201 | Normal response (error logged) |
| Registration cancelled, email sent | 200 | Normal response |
| Registration cancelled, email failed | 200 | Normal response (error logged) |

## Dependencies

- **No new NuGet packages** — Resend SDK already installed
- **No EF Core migrations** — no schema changes
- **Resend API key** — already configured via user-secrets
- **`System.Globalization`** — for `CultureInfo("es-ES")` in date formatting (part of .NET BCL)

## Notes

- **Language**: All email content in Spanish. Date formatting uses `es-ES` culture
- **Security**: HTML-encode all user-provided content (`SpecialNeeds`, `CampatesPreference`, names). Never include `GuardianDocumentNumber`, medical notes, or allergy info
- **RGPD**: Transactional emails — no consent needed. No tracking pixels. Only data the user submitted
- **Non-blocking**: Email failures are caught and logged — registration/cancellation always succeeds
- **No additional DB queries in CreateAsync**: The `detailed` reload and `edition` variable are already available
- **One additional DB query in CancelAsync**: `GetByIdWithDetailsAsync` is needed to load related data for the email — acceptable for current volume
- **Camp.Location is nullable**: Handle with fallback `"Sin ubicación"` in `BuildRegistrationEmailData`
- **CampEdition dates are `DateTime`**: Convert to `DateOnly` via `DateOnly.FromDateTime()` for the DTO

## Next Steps After Implementation

1. Manual test: Create a registration and verify confirmation email arrives in Resend dashboard
2. Manual test: Cancel a registration and verify cancellation email arrives
3. Verify emails render correctly in Gmail, Outlook, and mobile clients
4. Create PR targeting `dev` branch

## Implementation Verification

- [ ] **Code Quality**: No C# analyzer warnings, nullable reference types handled
- [ ] **Functionality**: Registration create/cancel still work correctly, emails sent
- [ ] **Non-blocking**: Email failure does not affect registration flow (verified by tests)
- [ ] **Security**: User-provided content is HTML-encoded, no sensitive data in emails
- [ ] **Testing**: All new and existing tests pass
- [ ] **Documentation**: Updated as needed
