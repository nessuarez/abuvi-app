using Abuvi.API.Features.Registrations;

namespace Abuvi.API.Features.Payments;

// --- Concept line records (serialized as JSON in Payment.ConceptLinesSerialized) ---

public record PaymentConceptLine(
    string PersonFullName,
    string AgeCategory,
    string AttendancePeriod,
    decimal IndividualAmount,
    decimal AmountInPayment,
    decimal Percentage
);

public record PaymentExtraConceptLine(
    string ExtraName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalAmount,
    string? UserInput,
    string PricingType
);

public record ManualPaymentConceptLine(
    string Description,
    decimal Amount
);

public record PaymentConceptLinesJson(
    List<PaymentConceptLine>? MemberLines,
    List<PaymentExtraConceptLine>? ExtraLines,
    ManualPaymentConceptLine? ManualLine = null
);

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
    DateTime CreatedAt,
    bool IsActionable,
    bool IsManual,
    List<PaymentConceptLine>? ConceptLines = null,
    List<PaymentExtraConceptLine>? ExtraConceptLines = null,
    ManualPaymentConceptLine? ManualConceptLine = null
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
    DateTime CreatedAt,
    bool IsActionable,
    bool IsManual,
    List<PaymentConceptLine>? ConceptLines = null,
    List<PaymentExtraConceptLine>? ExtraConceptLines = null,
    ManualPaymentConceptLine? ManualConceptLine = null
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

public record CreateManualPaymentRequest(
    decimal Amount,
    string Description,
    DateTime? DueDate = null,
    string? AdminNotes = null
);

public record UpdateManualPaymentRequest(
    decimal? Amount = null,
    string? Description = null,
    DateTime? DueDate = null,
    string? AdminNotes = null
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
