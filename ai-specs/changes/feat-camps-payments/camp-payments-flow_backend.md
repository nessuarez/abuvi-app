# Backend Implementation Plan: feat-camps-payments — Camp Payments Flow

## Overview

Implement a bank-transfer-based payment system for camp registrations. When a user creates a registration, two payment installments are auto-generated (50/50 split). Extras are only included in the second installment. Users see bank transfer instructions (IBAN, concept, amount) and upload proof of transfer. Admins review proofs and confirm/reject payments. Once both installments are confirmed, the registration transitions to `Confirmed`.

Each payment has a due date (e.g. 15 days before camp start) and a transfer concept that includes the family name (maybe shortened), family ID or code, and installment number (e.g. `PEREZLOPEZ-123-1`). Payment settings are stored in `AssociationSettings` as JSON.

This feature follows **Vertical Slice Architecture**, creating a new `Features/Payments/` slice since payments are a distinct domain concern with their own endpoints, service, repository, and models. The existing `PaymentsRepository` in `Features/Registrations/` will be moved to the new slice.

---

## Architecture Context

- **New feature slice**: `src/Abuvi.API/Features/Payments/`
- **Files to create**:
  - `PaymentsEndpoints.cs` — Minimal API endpoints
  - `PaymentsModels.cs` — Entity additions, DTOs, enums
  - `PaymentsService.cs` — Business logic
  - `IPaymentsService.cs` — Service interface
  - `PaymentsRepository.cs` — Data access (replaces existing)
  - `IPaymentsRepository.cs` — Repository interface (replaces existing)
  - `PaymentsValidators.cs` — FluentValidation rules
- **Files to modify**:
  - `Features/Registrations/RegistrationsModels.cs` — Add `PendingReview` to `PaymentStatus`, add new fields to `Payment` entity
  - `Features/Registrations/RegistrationsService.cs` — Call installment creation after registration creation
  - `Data/Configurations/PaymentConfiguration.cs` — Map new columns
  - `Features/BlobStorage/BlobStorageValidator.cs` — Add `"payment-proofs"` folder
  - `Program.cs` — Register new services, remove old `IPaymentsRepository` registration
- **Cross-cutting concerns**: `ApiResponse<T>`, `ValidationFilter<T>`, `NotFoundException`, `BusinessRuleException`, existing blob storage infrastructure
- **Tests to create**:
  - `tests/Abuvi.Tests/Features/Payments/PaymentsServiceTests.cs`
  - `tests/Abuvi.Tests/Features/Payments/PaymentsValidatorTests.cs`

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/feat-camps-payments-backend`
- **Implementation Steps**:
  1. Ensure on latest `dev`: `git checkout dev && git pull origin dev`
  2. Create branch: `git checkout -b feature/feat-camps-payments-backend`
  3. Verify: `git branch`

---

### Step 1: Update `PaymentStatus` Enum and `Payment` Entity

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Add `PendingReview` status and new fields to `Payment` entity

**1.1 Update `PaymentStatus` enum** (currently at line ~108):

```csharp
public enum PaymentStatus { Pending, PendingReview, Completed, Failed, Refunded }
```

**1.2 Add new properties to `Payment` entity** (currently at lines 83-95):

```csharp
public class Payment
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? ExternalReference { get; set; }
    // --- New fields ---
    public int InstallmentNumber { get; set; }
    public DateTime? DueDate { get; set; }
    public string? TransferConcept { get; set; }
    public string? ProofFileUrl { get; set; }
    public string? ProofFileName { get; set; }
    public DateTime? ProofUploadedAt { get; set; }
    public string? AdminNotes { get; set; }
    public Guid? ConfirmedByUserId { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    // --- End new fields ---
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Registration Registration { get; set; } = null!;
}
```

**1.3 Update `PaymentSummary` DTO** (currently at line ~222) to include new fields:

```csharp
public record PaymentSummary(
    Guid Id,
    int InstallmentNumber,
    decimal Amount,
    DateTime? DueDate,
    string Method,
    string Status,
    string? TransferConcept,
    string? ProofFileUrl,
    string? ProofFileName,
    DateTime? ProofUploadedAt,
    string? AdminNotes
);
```

**1.4 Update the `ToResponse` mapping** (lines ~246-307) to populate the new `PaymentSummary` fields from the `Payment` entity.

- **Implementation Notes**:
  - `PendingReview` is inserted between `Pending` and `Completed` in the enum. Since enums are stored as strings in the DB, insertion order does not matter.
  - `InstallmentNumber` defaults to 0 in C# but should always be explicitly set to 1 or 2 when creating payments.
  - `ConfirmedByUserId` is a nullable FK referencing `users.id` but does NOT need a formal navigation property — it's only used for audit purposes.

---

### Step 2: Update EF Core Configuration

- **File**: `src/Abuvi.API/Data/Configurations/PaymentConfiguration.cs`
- **Action**: Map new columns to the `payments` table

Add after existing property configurations:

```csharp
builder.Property(p => p.InstallmentNumber)
    .IsRequired()
    .HasDefaultValue(1)
    .HasColumnName("installment_number");

builder.Property(p => p.DueDate)
    .HasColumnName("due_date");

builder.Property(p => p.TransferConcept)
    .HasMaxLength(100)
    .HasColumnName("transfer_concept");

builder.Property(p => p.ProofFileUrl)
    .HasMaxLength(500)
    .HasColumnName("proof_file_url");

builder.Property(p => p.ProofFileName)
    .HasMaxLength(255)
    .HasColumnName("proof_file_name");

builder.Property(p => p.ProofUploadedAt)
    .HasColumnName("proof_uploaded_at");

builder.Property(p => p.AdminNotes)
    .HasColumnName("admin_notes");

builder.Property(p => p.ConfirmedByUserId)
    .HasColumnName("confirmed_by_user_id");

builder.Property(p => p.ConfirmedAt)
    .HasColumnName("confirmed_at");

// New indexes
builder.HasIndex(p => p.TransferConcept)
    .HasDatabaseName("IX_Payments_TransferConcept");
```

- **Implementation Notes**:
  - The existing `Status` column is `HasMaxLength(20)` — `"PendingReview"` is 13 chars, fits within 20.
  - `admin_notes` is text (no max length) — suitable for rejection reasons.
  - `confirmed_by_user_id` is a nullable UUID — no FK constraint configured (audit-only, no navigation property needed).

---

### Step 3: Create Database Migration

- **Action**: Generate EF Core migration for new columns
- **Command**:

  ```bash
  cd src/Abuvi.API
  dotnet ef migrations add AddPaymentInstallmentAndProofFields
  ```

- **Implementation Steps**:
  1. Run migration command
  2. Review generated migration — verify it adds all new columns with correct types and defaults
  3. Verify `installment_number` has `defaultValue: 1` (so existing rows get value 1)
  4. Apply migration: `dotnet ef database update`

- **Implementation Notes**:
  - Existing `Payment` rows (if any) will get `installment_number = 1` by default.
  - All new nullable columns default to `NULL`, which is correct for existing data.

---

### Step 4: Create Payment DTOs

- **File**: `src/Abuvi.API/Features/Payments/PaymentsModels.cs`
- **Action**: Define request/response DTOs as records

```csharp
using Abuvi.API.Features.Registrations;

namespace Abuvi.API.Features.Payments;

// --- User-facing response ---
public record PaymentResponse(
    Guid Id,
    Guid RegistrationId,
    int InstallmentNumber,
    decimal Amount,
    DateTime? DueDate,
    PaymentMethod Method,
    PaymentStatus Status,
    string? TransferConcept,
    string? ProofFileUrl,
    string? ProofFileName,
    DateTime? ProofUploadedAt,
    string? AdminNotes,
    DateTime CreatedAt
);

// --- Admin-facing response (extends with context) ---
public record AdminPaymentResponse(
    Guid Id,
    Guid RegistrationId,
    string FamilyUnitName,
    string CampEditionName,
    int InstallmentNumber,
    decimal Amount,
    DateTime? DueDate,
    PaymentStatus Status,
    string? TransferConcept,
    string? ProofFileUrl,
    string? ProofFileName,
    DateTime? ProofUploadedAt,
    string? AdminNotes,
    string? ConfirmedByUserName,
    DateTime? ConfirmedAt,
    DateTime CreatedAt
);

// --- Requests ---
public record ConfirmPaymentRequest(string? Notes);

public record RejectPaymentRequest(string Notes);

public record PaymentFilterRequest(
    PaymentStatus? Status = null,
    Guid? CampEditionId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20
);

// --- Payment Settings (stored in AssociationSettings as JSON) ---
public record PaymentSettingsResponse(
    string Iban,
    string BankName,
    string AccountHolder,
    int SecondInstallmentDaysBefore,
    string TransferConceptPrefix
);

public record PaymentSettingsRequest(
    string Iban,
    string BankName,
    string AccountHolder,
    int SecondInstallmentDaysBefore,
    string TransferConceptPrefix
);

// --- Internal DTO for JSON serialization in AssociationSettings ---
public class PaymentSettingsJson
{
    public string Iban { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public int SecondInstallmentDaysBefore { get; set; } = 15;
    public string TransferConceptPrefix { get; set; } = "CAMP";
}
```

- **Implementation Notes**:
  - `PaymentSettingsJson` is a mutable class (not record) because `System.Text.Json` requires a parameterless constructor for deserialization from `AssociationSettings.SettingValue`.
  - `PaymentFilterRequest` uses default parameter values so it works as query string binding in Minimal APIs.

---

### Step 5: Create FluentValidation Validators

- **File**: `src/Abuvi.API/Features/Payments/PaymentsValidators.cs`
- **Action**: Create validators for request DTOs

```csharp
using FluentValidation;

namespace Abuvi.API.Features.Payments;

public class RejectPaymentRequestValidator : AbstractValidator<RejectPaymentRequest>
{
    public RejectPaymentRequestValidator()
    {
        RuleFor(x => x.Notes)
            .NotEmpty().WithMessage("Las notas de rechazo son obligatorias.")
            .MinimumLength(10).WithMessage("Las notas de rechazo deben tener al menos 10 caracteres.");
    }
}

public class PaymentSettingsRequestValidator : AbstractValidator<PaymentSettingsRequest>
{
    public PaymentSettingsRequestValidator()
    {
        RuleFor(x => x.Iban)
            .NotEmpty().WithMessage("El IBAN es obligatorio.")
            .Matches(@"^ES\d{22}$").WithMessage("El IBAN debe tener el formato ES seguido de 22 dígitos.");

        RuleFor(x => x.BankName)
            .NotEmpty().WithMessage("El nombre del banco es obligatorio.")
            .MaximumLength(200);

        RuleFor(x => x.AccountHolder)
            .NotEmpty().WithMessage("El titular de la cuenta es obligatorio.")
            .MaximumLength(200);

        RuleFor(x => x.SecondInstallmentDaysBefore)
            .InclusiveBetween(1, 90)
            .WithMessage("Los días de antelación deben estar entre 1 y 90.");

        RuleFor(x => x.TransferConceptPrefix)
            .NotEmpty().WithMessage("El prefijo del concepto es obligatorio.")
            .MaximumLength(20)
            .Matches(@"^[A-Z0-9\-]+$").WithMessage("El prefijo solo puede contener letras mayúsculas, números y guiones.");
    }
}

public class PaymentFilterRequestValidator : AbstractValidator<PaymentFilterRequest>
{
    public PaymentFilterRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
```

- **Implementation Notes**:
  - IBAN validation: `ES` + 22 digits. This is a simplified Spanish IBAN format check. The IBAN is stored without spaces; formatting is frontend-only.
  - Error messages in Spanish (user-facing) per project convention.
  - `ConfirmPaymentRequest` has no validator since `Notes` is optional.
  - Validators are auto-registered via `AddValidatorsFromAssemblyContaining<Program>()` already in `Program.cs`.

---

### Step 6: Create Payment Repository

- **File**: `src/Abuvi.API/Features/Payments/IPaymentsRepository.cs`
- **Action**: Define repository interface

```csharp
using Abuvi.API.Features.Registrations;

namespace Abuvi.API.Features.Payments;

public interface IPaymentsRepository
{
    Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken ct);
    Task<Payment?> GetByIdWithRegistrationAsync(Guid paymentId, CancellationToken ct);
    Task<List<Payment>> GetByRegistrationIdAsync(Guid registrationId, CancellationToken ct);
    Task AddAsync(Payment payment, CancellationToken ct);
    Task AddRangeAsync(List<Payment> payments, CancellationToken ct);
    Task UpdateAsync(Payment payment, CancellationToken ct);
    Task<decimal> GetTotalCompletedAsync(Guid registrationId, CancellationToken ct);
    Task<List<Payment>> GetPendingReviewAsync(CancellationToken ct);
    Task<(List<Payment> Items, int TotalCount)> GetFilteredAsync(PaymentFilterRequest filter, CancellationToken ct);
}
```

- **File**: `src/Abuvi.API/Features/Payments/PaymentsRepository.cs`
- **Action**: Implement repository

```csharp
using Abuvi.API.Data;
using Abuvi.API.Features.Registrations;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Payments;

public class PaymentsRepository(AbuviDbContext db) : IPaymentsRepository
{
    public async Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken ct)
        => await db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, ct);

    public async Task<Payment?> GetByIdWithRegistrationAsync(Guid paymentId, CancellationToken ct)
        => await db.Payments
            .Include(p => p.Registration)
                .ThenInclude(r => r.CampEdition)
            .Include(p => p.Registration)
                .ThenInclude(r => r.FamilyUnit)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct);

    public async Task<List<Payment>> GetByRegistrationIdAsync(Guid registrationId, CancellationToken ct)
        => await db.Payments
            .Where(p => p.RegistrationId == registrationId)
            .OrderBy(p => p.InstallmentNumber)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task AddAsync(Payment payment, CancellationToken ct)
    {
        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(List<Payment> payments, CancellationToken ct)
    {
        db.Payments.AddRange(payments);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Payment payment, CancellationToken ct)
    {
        payment.UpdatedAt = DateTime.UtcNow;
        db.Payments.Update(payment);
        await db.SaveChangesAsync(ct);
    }

    public async Task<decimal> GetTotalCompletedAsync(Guid registrationId, CancellationToken ct)
        => await db.Payments
            .Where(p => p.RegistrationId == registrationId && p.Status == PaymentStatus.Completed)
            .SumAsync(p => p.Amount, ct);

    public async Task<List<Payment>> GetPendingReviewAsync(CancellationToken ct)
        => await db.Payments
            .Include(p => p.Registration)
                .ThenInclude(r => r.FamilyUnit)
            .Include(p => p.Registration)
                .ThenInclude(r => r.CampEdition)
            .Where(p => p.Status == PaymentStatus.PendingReview)
            .OrderBy(p => p.ProofUploadedAt)
            .ToListAsync(ct);

    public async Task<(List<Payment> Items, int TotalCount)> GetFilteredAsync(
        PaymentFilterRequest filter, CancellationToken ct)
    {
        var query = db.Payments
            .Include(p => p.Registration)
                .ThenInclude(r => r.FamilyUnit)
            .Include(p => p.Registration)
                .ThenInclude(r => r.CampEdition)
            .AsQueryable();

        if (filter.Status.HasValue)
            query = query.Where(p => p.Status == filter.Status.Value);

        if (filter.CampEditionId.HasValue)
            query = query.Where(p => p.Registration.CampEditionId == filter.CampEditionId.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(p => p.CreatedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(p => p.CreatedAt <= filter.ToDate.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
```

- **Implementation Notes**:
  - `GetByIdWithRegistrationAsync` includes `Registration.CampEdition` and `Registration.FamilyUnit` for admin context (family name, edition name).
  - `GetPendingReviewAsync` orders by `ProofUploadedAt` so oldest proofs are reviewed first (FIFO).
  - `GetFilteredAsync` returns a tuple `(Items, TotalCount)` for pagination. The endpoints layer wraps this into a paginated response.
  - `GetByRegistrationIdAsync` uses `AsNoTracking()` for read-only queries.

---

### Step 7: Create Payment Service

- **File**: `src/Abuvi.API/Features/Payments/IPaymentsService.cs`

```csharp
using Abuvi.API.Features.Registrations;
using Microsoft.AspNetCore.Http;

namespace Abuvi.API.Features.Payments;

public interface IPaymentsService
{
    Task<List<PaymentResponse>> CreateInstallmentsAsync(Guid registrationId, CancellationToken ct);
    Task<PaymentResponse> UploadProofAsync(Guid paymentId, Guid userId, IFormFile file, CancellationToken ct);
    Task<PaymentResponse> RemoveProofAsync(Guid paymentId, Guid userId, CancellationToken ct);
    Task<PaymentResponse> ConfirmPaymentAsync(Guid paymentId, Guid adminUserId, string? notes, CancellationToken ct);
    Task<PaymentResponse> RejectPaymentAsync(Guid paymentId, Guid adminUserId, string notes, CancellationToken ct);
    Task<List<PaymentResponse>> GetByRegistrationAsync(Guid registrationId, Guid userId, string? userRole, CancellationToken ct);
    Task<PaymentResponse> GetByIdAsync(Guid paymentId, Guid userId, string? userRole, CancellationToken ct);
    Task<List<AdminPaymentResponse>> GetPendingReviewAsync(CancellationToken ct);
    Task<(List<AdminPaymentResponse> Items, int TotalCount)> GetAllPaymentsAsync(PaymentFilterRequest filter, CancellationToken ct);
    Task<PaymentSettingsResponse> GetPaymentSettingsAsync(CancellationToken ct);
    Task<PaymentSettingsResponse> UpdatePaymentSettingsAsync(PaymentSettingsRequest request, Guid adminUserId, CancellationToken ct);
}
```

- **File**: `src/Abuvi.API/Features/Payments/PaymentsService.cs`
- **Action**: Implement all payment business logic

**Constructor dependencies**:

```csharp
public class PaymentsService(
    IPaymentsRepository paymentsRepo,
    IRegistrationsRepository registrationsRepo,
    IAssociationSettingsRepository settingsRepo,
    IBlobStorageService blobStorageService,
    ILogger<PaymentsService> logger) : IPaymentsService
```

**Key methods — implementation details**:

#### `CreateInstallmentsAsync(Guid registrationId, CancellationToken ct)`

1. Load registration with details via `registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)`.
2. Throw `NotFoundException` if not found.
3. Load payment settings via `settingsRepo.GetByKeyAsync("payment_settings", ct)`.
4. Deserialize settings JSON to `PaymentSettingsJson`. If not found, use defaults (prefix = "CAMP", daysBefore = 15).
5. Calculate amounts:
   - `installment1Amount = Math.Ceiling(registration.TotalAmount / 2m)`
   - `installment2Amount = registration.TotalAmount - installment1Amount`
6. Generate transfer concepts:
   - Get family last name from `registration.FamilyUnit` representative (or family unit name).
   - Format: `"{Prefix}-{CampEdition.StartDate.Year}-{FamilyLastName}-{InstallmentNumber}"`.
   - Normalize: uppercase, remove accents/special chars, truncate to 100 chars.
7. Calculate due dates:
   - Installment 1: `DateTime.UtcNow`
   - Installment 2: `registration.CampEdition.StartDate - settings.SecondInstallmentDaysBefore days`
8. Create two `Payment` entities:

   ```csharp
   new Payment
   {
       RegistrationId = registrationId,
       Amount = installment1Amount,
       PaymentDate = DateTime.UtcNow,
       Method = PaymentMethod.Transfer,
       Status = PaymentStatus.Pending,
       InstallmentNumber = 1,
       DueDate = DateTime.UtcNow,
       TransferConcept = concept1
   }
   ```

9. Save via `paymentsRepo.AddRangeAsync(payments, ct)`.
10. Log: `logger.LogInformation("Created {Count} installments for registration {RegistrationId}", 2, registrationId)`.
11. Return mapped `List<PaymentResponse>`.

#### `UploadProofAsync(Guid paymentId, Guid userId, IFormFile file, CancellationToken ct)`

1. Load payment with registration: `paymentsRepo.GetByIdWithRegistrationAsync(paymentId, ct)`.
2. Throw `NotFoundException` if not found.
3. **Authorization**: Verify `payment.Registration.RegisteredByUserId == userId`. Throw `BusinessRuleException` if not.
4. **Status check**: Only `Pending` status allows proof upload. Throw `BusinessRuleException` if status is not `Pending`.
5. Upload file via blob storage:

   ```csharp
   var uploadRequest = new UploadBlobRequest
   {
       File = file,
       Folder = "payment-proofs",
       ContextId = paymentId,
       GenerateThumbnail = false
   };
   var result = await blobStorageService.UploadAsync(uploadRequest, ct);
   ```

6. Update payment fields:
   - `ProofFileUrl = result.FileUrl`
   - `ProofFileName = file.FileName`
   - `ProofUploadedAt = DateTime.UtcNow`
   - `Status = PaymentStatus.PendingReview`
7. Save via `paymentsRepo.UpdateAsync(payment, ct)`.
8. Log state change.
9. Return mapped `PaymentResponse`.

#### `RemoveProofAsync(Guid paymentId, Guid userId, CancellationToken ct)`

1. Load payment with registration.
2. Authorize user (must be registration owner).
3. Status must be `Pending` or `PendingReview`. Throw `BusinessRuleException` otherwise.
4. If `ProofFileUrl` is not null, delete blob via `blobStorageService.DeleteAsync([proofFileKey], ct)`.
   - Extract key from URL (strip base URL prefix).
5. Clear proof fields: `ProofFileUrl = null`, `ProofFileName = null`, `ProofUploadedAt = null`.
6. Set `Status = PaymentStatus.Pending` (in case it was `PendingReview`).
7. Save and return.

#### `ConfirmPaymentAsync(Guid paymentId, Guid adminUserId, string? notes, CancellationToken ct)`

1. Load payment with registration.
2. Throw `NotFoundException` if not found.
3. **Status check**: Must be `PendingReview`. Throw `BusinessRuleException` if not.
4. Update:
   - `Status = PaymentStatus.Completed`
   - `ConfirmedByUserId = adminUserId`
   - `ConfirmedAt = DateTime.UtcNow`
   - `AdminNotes = notes`
5. Save via `paymentsRepo.UpdateAsync(payment, ct)`.
6. **Check if both installments are completed**: Load all payments for registration.
   - If all payments have `Status == Completed`, update `registration.Status = RegistrationStatus.Confirmed` via `registrationsRepo.UpdateAsync(registration, ct)`.
   - Log registration confirmation.
7. Return mapped `PaymentResponse`.

#### `RejectPaymentAsync(Guid paymentId, Guid adminUserId, string notes, CancellationToken ct)`

1. Load payment with registration.
2. **Status check**: Must be `PendingReview`. Throw `BusinessRuleException` if not.
3. Update:
   - `Status = PaymentStatus.Pending` (back to waiting)
   - `AdminNotes = notes` (rejection reason)
   - `ConfirmedByUserId = adminUserId` (who rejected)
   - `ConfirmedAt = DateTime.UtcNow`
   - Do NOT clear proof fields — keep for reference.
4. Save and return.
5. Log rejection.

#### `GetByRegistrationAsync(Guid registrationId, Guid userId, string? userRole, CancellationToken ct)`

1. Load registration via `registrationsRepo.GetByIdAsync(registrationId, ct)`.
2. If not admin/board: verify `registration.RegisteredByUserId == userId`.
3. Load payments via `paymentsRepo.GetByRegistrationIdAsync(registrationId, ct)`.
4. Map and return `List<PaymentResponse>`.

#### `GetByIdAsync(Guid paymentId, Guid userId, string? userRole, CancellationToken ct)`

1. Load payment with registration.
2. If not admin/board: verify ownership.
3. Map and return `PaymentResponse`.

#### `GetPendingReviewAsync(CancellationToken ct)`

1. Load via `paymentsRepo.GetPendingReviewAsync(ct)`.
2. Map to `List<AdminPaymentResponse>` including family name and edition name from navigation properties.

#### `GetAllPaymentsAsync(PaymentFilterRequest filter, CancellationToken ct)`

1. Load via `paymentsRepo.GetFilteredAsync(filter, ct)`.
2. Map items to `List<AdminPaymentResponse>`.
3. Return tuple with total count for pagination.

#### `GetPaymentSettingsAsync(CancellationToken ct)`

1. Load `settingsRepo.GetByKeyAsync("payment_settings", ct)`.
2. If not found, return defaults.
3. Deserialize JSON and map to `PaymentSettingsResponse`.

#### `UpdatePaymentSettingsAsync(PaymentSettingsRequest request, Guid adminUserId, CancellationToken ct)`

1. Load existing setting or create new.
2. Serialize `PaymentSettingsJson` to JSON.
3. Save via `settingsRepo.UpdateAsync` or `CreateAsync`.
4. Return mapped response.

- **Implementation Notes**:
  - `IBlobStorageService` — check actual interface name and method signatures. The upload method might be called `UploadFileAsync` rather than `UploadAsync`. Verify against `src/Abuvi.API/Features/BlobStorage/IBlobStorageService.cs`.
  - For deleting blobs, check the delete method signature — it may accept `string[]` keys.
  - The `FamilyUnit` may not have a direct "last name" field. Check the actual FamilyUnit model to determine how to derive the family name for transfer concepts. It might be the representative member's last name.
  - `Math.Ceiling` on `decimal` requires using `Math.Ceiling(totalAmount / 2m)` — verify this works correctly with `decimal` types.

---

### Step 8: Create Payment Endpoints

- **File**: `src/Abuvi.API/Features/Payments/PaymentsEndpoints.cs`
- **Action**: Register Minimal API endpoints

```csharp
using Abuvi.API.Common.Models;
using Abuvi.API.Common.Filters;
using System.Security.Claims;

namespace Abuvi.API.Features.Payments;

public static class PaymentsEndpoints
{
    public static void MapPaymentsEndpoints(this WebApplication app)
    {
        // User-facing endpoints (require auth)
        var payments = app.MapGroup("/api/payments")
            .WithTags("Payments")
            .RequireAuthorization();

        payments.MapGet("/{paymentId:guid}", GetPaymentById);

        payments.MapPost("/{paymentId:guid}/upload-proof", UploadProof)
            .DisableAntiforgery();  // Required for multipart form upload

        payments.MapDelete("/{paymentId:guid}/proof", RemoveProof);

        // Registration payments (under registrations group)
        var regPayments = app.MapGroup("/api/registrations")
            .WithTags("Payments")
            .RequireAuthorization();

        regPayments.MapGet("/{registrationId:guid}/payments", GetRegistrationPayments);

        // Admin endpoints
        var admin = app.MapGroup("/api/admin/payments")
            .WithTags("Payments Admin")
            .RequireAuthorization();

        admin.MapGet("/", GetAllPayments);
        admin.MapGet("/pending-review", GetPendingReview);
        admin.MapPost("/{paymentId:guid}/confirm", ConfirmPayment);
        admin.MapPost("/{paymentId:guid}/reject", RejectPayment)
            .AddEndpointFilter<ValidationFilter<RejectPaymentRequest>>();

        // Payment settings (public GET, admin PUT)
        var settings = app.MapGroup("/api/settings/payment")
            .WithTags("Payment Settings");

        settings.MapGet("/", GetPaymentSettings);
        settings.MapPut("/", UpdatePaymentSettings)
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<PaymentSettingsRequest>>();
    }
}
```

**Endpoint handler methods** (static methods in the same class):

Each handler follows the pattern from `RegistrationsEndpoints.cs`:

- Extract `userId` and `userRole` from `ClaimsPrincipal`
- Call service method
- Wrap result in `ApiResponse<T>.Ok(data)`
- Handle exceptions: `NotFoundException` → 404, `BusinessRuleException` → 422 or 409, `UnauthorizedAccessException` → 403

**Example handler for `UploadProof`**:

```csharp
private static async Task<IResult> UploadProof(
    Guid paymentId,
    IFormFile file,
    ClaimsPrincipal user,
    IPaymentsService service,
    CancellationToken ct)
{
    var userId = user.GetUserId()
        ?? throw new UnauthorizedAccessException("Usuario no autenticado");

    try
    {
        var result = await service.UploadProofAsync(paymentId, userId, file, ct);
        return TypedResults.Ok(ApiResponse<PaymentResponse>.Ok(result));
    }
    catch (NotFoundException ex)
    {
        return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
    catch (BusinessRuleException ex)
    {
        return TypedResults.UnprocessableEntity(ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE"));
    }
}
```

**Example handler for `ConfirmPayment`** (admin):

```csharp
private static async Task<IResult> ConfirmPayment(
    Guid paymentId,
    ConfirmPaymentRequest? request,
    ClaimsPrincipal user,
    IPaymentsService service,
    CancellationToken ct)
{
    var userId = user.GetUserId()
        ?? throw new UnauthorizedAccessException("Usuario no autenticado");
    var userRole = user.GetUserRole();

    if (userRole is not ("Admin" or "Board"))
        return TypedResults.Forbid();

    try
    {
        var result = await service.ConfirmPaymentAsync(paymentId, userId, request?.Notes, ct);
        return TypedResults.Ok(ApiResponse<PaymentResponse>.Ok(result));
    }
    catch (NotFoundException ex)
    {
        return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
    catch (BusinessRuleException ex)
    {
        return TypedResults.UnprocessableEntity(ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE"));
    }
}
```

- **Implementation Notes**:
  - `DisableAntiforgery()` is needed on `upload-proof` because it accepts `IFormFile`.
  - Admin endpoints check role inside the handler (not via policy) — consistent with `RegistrationsEndpoints.cs` pattern.
  - `GetPaymentSettings` does NOT require auth (public) — users need to see IBAN to make transfers.
  - `ConfirmPaymentRequest` is nullable since body is optional (notes are optional).

---

### Step 9: Register Services and Endpoints in `Program.cs`

- **File**: `src/Abuvi.API/Program.cs`
- **Action**: Register new services and endpoint mapping

**Service registration** — replace existing `IPaymentsRepository` line and add new services:

```csharp
// Payments feature (replaces old IPaymentsRepository registration)
builder.Services.AddScoped<IPaymentsRepository, PaymentsRepository>();
builder.Services.AddScoped<IPaymentsService, PaymentsService>();
```

Remove the old `PaymentsRepository` import from `Abuvi.API.Features.Registrations` and use the new one from `Abuvi.API.Features.Payments`.

**Endpoint registration** — add after `app.MapRegistrationsEndpoints()`:

```csharp
app.MapPaymentsEndpoints();
```

- **Implementation Notes**:
  - Delete the old `src/Abuvi.API/Features/Registrations/PaymentsRepository.cs` file after the new one is in place.
  - Ensure no other files reference `Abuvi.API.Features.Registrations.IPaymentsRepository` or `Abuvi.API.Features.Registrations.PaymentsRepository`.
  - The `RegistrationsService` currently does NOT inject `IPaymentsRepository` — it computes `amountPaid` from the loaded `Payments` navigation property. This pattern remains unchanged.

---

### Step 10: Update Blob Storage Validator

- **File**: `src/Abuvi.API/Features/BlobStorage/BlobStorageValidator.cs`
- **Action**: Add `"payment-proofs"` to allowed folders and configure its accepted types

**Update `AllowedFolders` array**:

```csharp
private static readonly string[] AllowedFolders =
    ["photos", "media-items", "camp-locations", "camp-photos", "payment-proofs"];
```

**Update `IsExtensionAllowed` method** (or equivalent) to allow both images AND documents for `payment-proofs`:

Check the current implementation. If it uses a switch/if on folder name to determine allowed extensions, add a case for `"payment-proofs"` that allows `.jpg`, `.jpeg`, `.png`, `.webp`, `.pdf`.

If the current implementation defaults non-`media-items` folders to image-only, then `payment-proofs` needs to be added alongside `media-items` as a folder that also accepts documents.

- **Implementation Notes**:
  - Also update the frontend `BlobFolder` type in `src/types/blob-storage.ts` to include `'payment-proofs'` (this is a frontend task but document it as a dependency).

---

### Step 11: Modify Registration Service to Create Installments

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`
- **Action**: Inject `IPaymentsService` and call `CreateInstallmentsAsync` after registration creation

**Add to constructor**:

```csharp
public class RegistrationsService(
    // ... existing dependencies ...
    IPaymentsService paymentsService,  // NEW
    ILogger<RegistrationsService> logger)
```

**Modify `CreateAsync`** — after the registration is saved and reloaded (line ~150-156):

```csharp
// After: var detailed = await registrationsRepo.GetByIdWithDetailsAsync(registration.Id, ct);

// Create payment installments
await paymentsService.CreateInstallmentsAsync(registration.Id, ct);

// Reload to include the new payments
detailed = await registrationsRepo.GetByIdWithDetailsAsync(registration.Id, ct);
var amountPaid = detailed!.Payments
    .Where(p => p.Status == PaymentStatus.Completed)
    .Sum(p => p.Amount);
return detailed.ToResponse(amountPaid);
```

- **Implementation Notes**:
  - The reload is necessary because `GetByIdWithDetailsAsync` includes payments via `.Include(r => r.Payments)`.
  - This adds one extra DB query but keeps the code clean and consistent.
  - The `IPaymentsService` must be imported from `Abuvi.API.Features.Payments`.
  - Check for circular dependency: `RegistrationsService` → `IPaymentsService` → `IRegistrationsRepository`. This should be fine since `PaymentsService` depends on `IRegistrationsRepository` (interface), not on `RegistrationsService`.

---

### Step 12: Write Unit Tests (TDD)

- **File**: `tests/Abuvi.Tests/Features/Payments/PaymentsServiceTests.cs`
- **Action**: Write comprehensive unit tests following TDD

**Test class setup**:

```csharp
using NSubstitute;
using FluentAssertions;
using Xunit;
using Abuvi.API.Features.Payments;
using Abuvi.API.Features.Registrations;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.BlobStorage;
using Microsoft.Extensions.Logging;

public class PaymentsServiceTests
{
    private readonly IPaymentsRepository _paymentsRepo = Substitute.For<IPaymentsRepository>();
    private readonly IRegistrationsRepository _registrationsRepo = Substitute.For<IRegistrationsRepository>();
    private readonly IAssociationSettingsRepository _settingsRepo = Substitute.For<IAssociationSettingsRepository>();
    private readonly IBlobStorageService _blobStorageService = Substitute.For<IBlobStorageService>();
    private readonly ILogger<PaymentsService> _logger = Substitute.For<ILogger<PaymentsService>>();
    private readonly PaymentsService _sut;

    public PaymentsServiceTests()
    {
        _sut = new PaymentsService(
            _paymentsRepo, _registrationsRepo, _settingsRepo, _blobStorageService, _logger);
    }
}
```

**Test cases to implement** (following `MethodName_StateUnderTest_ExpectedBehavior`):

#### Installment Creation

1. `CreateInstallmentsAsync_ValidRegistration_CreatesTwoPayments`
   - Arrange: Registration with TotalAmount = 200, CampEdition with StartDate, PaymentSettings with prefix "CAMP"
   - Assert: `paymentsRepo.AddRangeAsync` called with list of 2 payments

2. `CreateInstallmentsAsync_ValidRegistration_SplitsAmountEvenly`
   - Arrange: TotalAmount = 200
   - Assert: Payment 1 amount = 100, Payment 2 amount = 100

3. `CreateInstallmentsAsync_OddAmount_RoundsFirstInstallmentUp`
   - Arrange: TotalAmount = 201
   - Assert: Payment 1 amount = 101, Payment 2 amount = 100

4. `CreateInstallmentsAsync_ValidRegistration_GeneratesTransferConcepts`
   - Assert: Concepts match format `"CAMP-{Year}-{FamilyName}-1"` and `"...-2"`

5. `CreateInstallmentsAsync_ValidRegistration_SetsCorrectDueDates`
   - Assert: Installment 1 DueDate = now, Installment 2 DueDate = StartDate - N days

6. `CreateInstallmentsAsync_RegistrationNotFound_ThrowsNotFoundException`
   - Arrange: `registrationsRepo.GetByIdWithDetailsAsync` returns null
   - Assert: Throws `NotFoundException`

7. `CreateInstallmentsAsync_NoPaymentSettings_UsesDefaults`
   - Arrange: `settingsRepo.GetByKeyAsync("payment_settings", ...)` returns null
   - Assert: Uses prefix "CAMP", daysBefore = 15

#### Proof Upload

1. `UploadProofAsync_PendingPayment_UpdatesProofFieldsAndStatus`
   - Arrange: Payment with Status = Pending, valid file, matching userId
   - Assert: Status → PendingReview, ProofFileUrl set, ProofUploadedAt set

2. `UploadProofAsync_CompletedPayment_ThrowsBusinessRuleException`
   - Arrange: Payment with Status = Completed
   - Assert: Throws BusinessRuleException

3. `UploadProofAsync_WrongUser_ThrowsBusinessRuleException`
    - Arrange: Payment owned by user A, called with user B
    - Assert: Throws BusinessRuleException

4. `UploadProofAsync_PaymentNotFound_ThrowsNotFoundException`

#### Remove Proof

1. `RemoveProofAsync_PendingReviewPayment_ClearsProofAndResetsStatus`
    - Assert: ProofFileUrl = null, Status → Pending, blob deleted

2. `RemoveProofAsync_CompletedPayment_ThrowsBusinessRuleException`

#### Confirm Payment

1. `ConfirmPaymentAsync_PendingReviewPayment_MarksCompleted`
    - Assert: Status → Completed, ConfirmedByUserId set, ConfirmedAt set

2. `ConfirmPaymentAsync_PendingWithoutProof_ThrowsBusinessRuleException`
    - Arrange: Status = Pending (no proof uploaded)
    - Assert: Throws BusinessRuleException

3. `ConfirmPaymentAsync_BothInstallmentsCompleted_ConfirmsRegistration`
    - Arrange: Confirm second installment, first already Completed
    - Assert: `registrationsRepo.UpdateAsync` called with Status = Confirmed

4. `ConfirmPaymentAsync_OnlyOneInstallmentCompleted_RegistrationStaysPending`
    - Assert: Registration status NOT changed

#### Reject Payment

1. `RejectPaymentAsync_PendingReviewPayment_ResetsToPending`
    - Assert: Status → Pending, AdminNotes set with rejection reason

2. `RejectPaymentAsync_NotPendingReview_ThrowsBusinessRuleException`

#### Payment Settings

1. `GetPaymentSettingsAsync_SettingsExist_ReturnsDeserialized`
2. `GetPaymentSettingsAsync_NoSettings_ReturnsDefaults`
3. `UpdatePaymentSettingsAsync_ValidRequest_SavesAndReturns`

---

- **File**: `tests/Abuvi.Tests/Features/Payments/PaymentsValidatorTests.cs`
- **Action**: Test validators

```
RejectPaymentRequestValidator:
- Notes_Empty_Fails
- Notes_TooShort_Fails
- Notes_ValidLength_Passes

PaymentSettingsRequestValidator:
- Iban_InvalidFormat_Fails
- Iban_ValidSpanish_Passes
- BankName_Empty_Fails
- SecondInstallmentDaysBefore_OutOfRange_Fails
- ValidRequest_Passes

PaymentFilterRequestValidator:
- Page_LessThan1_Fails
- PageSize_GreaterThan100_Fails
- ValidFilter_Passes
```

---

### Step 13: Update Technical Documentation

- **Action**: Update documentation to reflect changes
- **Implementation Steps**:
  1. **`ai-specs/specs/data-model.md`**: Document new `Payment` fields (`InstallmentNumber`, `DueDate`, `TransferConcept`, `ProofFileUrl`, `ProofFileName`, `ProofUploadedAt`, `AdminNotes`, `ConfirmedByUserId`, `ConfirmedAt`), updated `PaymentStatus` enum with `PendingReview`.
  2. **`ai-specs/specs/api-endpoints.md`**: Document all new payment endpoints (GET/POST registration payments, upload proof, confirm, reject, admin listing, settings).
  3. Verify OpenAPI/Swagger auto-generates correctly for new endpoints.

---

## Implementation Order

1. **Step 0**: Create feature branch `feature/feat-camps-payments-backend`
2. **Step 1**: Update `PaymentStatus` enum and `Payment` entity (RegistrationsModels.cs)
3. **Step 2**: Update EF Core configuration (PaymentConfiguration.cs)
4. **Step 3**: Create database migration
5. **Step 4**: Create Payment DTOs (PaymentsModels.cs)
6. **Step 5**: Create validators (PaymentsValidators.cs)
7. **Step 12**: Write unit tests FIRST (TDD — Red phase)
8. **Step 6**: Create repository (IPaymentsRepository.cs, PaymentsRepository.cs)
9. **Step 7**: Create service (IPaymentsService.cs, PaymentsService.cs) — make tests pass (Green phase)
10. **Step 8**: Create endpoints (PaymentsEndpoints.cs)
11. **Step 9**: Register services in Program.cs, delete old PaymentsRepository
12. **Step 10**: Update BlobStorage validator
13. **Step 11**: Modify RegistrationsService to create installments
14. **Step 13**: Update documentation
15. Refactor if needed (Refactor phase)

**Note on TDD**: Steps 7 (tests) comes before Steps 6-9 (implementation). Write failing tests first, then implement to make them pass. The order above reflects TDD's Red-Green-Refactor cycle.

---

## Testing Checklist

- [ ] All 22+ PaymentsService test cases pass
- [ ] All 10+ validator test cases pass
- [ ] 90%+ code coverage on `PaymentsService`, `PaymentsValidators`
- [ ] No N+1 queries in repository (verify with EF Core query logging)
- [ ] `PaymentStatus.PendingReview` stored correctly as string in DB
- [ ] Migration applies and rolls back cleanly
- [ ] Existing tests still pass (no regressions from `PaymentStatus` enum change)
- [ ] Existing registration flow still works (payments auto-created)
- [ ] Blob upload works for `payment-proofs` folder (images + PDF)

---

## Error Response Format

| Scenario | HTTP Status | Error Code |
|----------|-------------|------------|
| Payment not found | 404 | `NOT_FOUND` |
| Not authorized (not owner) | 403 | Forbid |
| Wrong payment status for operation | 422 | `BUSINESS_RULE` |
| Validation errors | 400 | `VALIDATION_ERROR` |
| Duplicate/conflict | 409 | `CONFLICT` |
| Server error | 500 | `INTERNAL_ERROR` |

All responses wrapped in `ApiResponse<T>`.

---

## Dependencies

- **No new NuGet packages required** — all functionality uses existing dependencies (EF Core, FluentValidation, blob storage).
- **EF Core Migration**:

  ```bash
  cd src/Abuvi.API
  dotnet ef migrations add AddPaymentInstallmentAndProofFields
  dotnet ef database update
  ```

---

## Notes

- **Business rule: IBAN stored without spaces**. The IBAN regex validates `ES` + 22 digits. Frontend formats with spaces for display.
- **Transfer concept uniqueness**: Not enforced at DB level (index but not unique constraint) because family names could repeat across editions. The combination of prefix + year + name + installment number should be practically unique.
- **Audit logging**: Uses structured logging via `ILogger`. Full audit trail entity (separate table) is NOT implemented in this iteration — relies on log files.
- **Optimistic concurrency**: `UpdatedAt` is updated on every save. If concurrent confirm/reject occurs, EF Core's change tracking handles it. For strict concurrency, consider adding a `[ConcurrencyCheck]` attribute to `UpdatedAt` in a future iteration.
- **RGPD/GDPR**: Payment proofs may contain personal banking information. The blob storage uses private ACL by default. Ensure proofs are only accessible via authenticated API endpoints, not directly via public URLs. Check if `BlobStorageRepository` uses `S3CannedACL.PublicRead` — if so, payment proofs need a different ACL or a signed URL approach.
- **Error messages in Spanish** (user-facing) per project convention. Logger messages in English.

---

## Next Steps After Implementation

1. **Frontend implementation**: Follow `camp-payments-flow_enriched.md` frontend section.
2. **Manual testing**: Create a registration, verify installments are created, upload proof, confirm as admin.
3. **Integration with existing registration views**: Ensure `RegistrationResponse` now includes payment data.
4. **RGPD review**: Verify proof files are not publicly accessible.
