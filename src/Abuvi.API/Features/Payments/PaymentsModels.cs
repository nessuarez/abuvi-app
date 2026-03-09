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
    int FirstInstallmentDaysBefore,
    int SecondInstallmentDaysBefore,
    int ExtrasInstallmentDaysFromCampStart,
    string TransferConceptPrefix
);

public record PaymentSettingsRequest(
    string Iban,
    string BankName,
    string AccountHolder,
    int FirstInstallmentDaysBefore,
    int SecondInstallmentDaysBefore,
    int ExtrasInstallmentDaysFromCampStart,
    string TransferConceptPrefix
);

// --- Internal DTO for JSON serialization in AssociationSettings ---
public class PaymentSettingsJson
{
    public string Iban { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public int FirstInstallmentDaysBefore { get; set; } = 30;
    public int SecondInstallmentDaysBefore { get; set; } = 15;
    public int ExtrasInstallmentDaysFromCampStart { get; set; } = 0;
    public string TransferConceptPrefix { get; set; } = "CAMP";
}
