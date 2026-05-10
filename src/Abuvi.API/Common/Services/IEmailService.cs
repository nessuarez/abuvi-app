namespace Abuvi.API.Common.Services;

/// <summary>
/// Service interface for sending emails via Resend
/// </summary>
public interface IEmailService
{
    // ========================================
    // Registration & Authentication
    // ========================================

    /// <summary>
    /// Sends an email verification email to the user
    /// </summary>
    Task SendVerificationEmailAsync(
        string toEmail,
        string firstName,
        string verificationToken,
        CancellationToken ct);

    /// <summary>
    /// Sends a welcome email to the user after successful verification
    /// </summary>
    Task SendWelcomeEmailAsync(
        string toEmail,
        string firstName,
        string lastName,
        CancellationToken ct);

    /// <summary>
    /// Sends a password reset email with a reset token
    /// </summary>
    Task SendPasswordResetEmailAsync(
        string toEmail,
        string firstName,
        string resetToken,
        CancellationToken ct);

    // ========================================
    // Camp Management
    // ========================================

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

    /// <summary>
    /// Sends a notification about camp updates or changes
    /// </summary>
    Task SendCampUpdateNotificationAsync(
        string toEmail,
        string firstName,
        string campName,
        string updateMessage,
        CancellationToken ct);

    // ========================================
    // Payments
    // ========================================

    /// <summary>
    /// Sends a payment receipt email after successful payment
    /// </summary>
    Task SendPaymentReceiptAsync(
        string toEmail,
        string firstName,
        decimal amount,
        string paymentReference,
        CancellationToken ct);

    // ========================================
    // Registration Status Notifications
    // ========================================

    /// <summary>Sends "Al corriente — plazo 1 confirmado" when board sets PartiallyPaid</summary>
    Task SendRegistrationPartiallyPaidAsync(
        RegistrationStatusEmailData data,
        CancellationToken ct);

    /// <summary>Sends "Todos los pagos recibidos" when last payment confirmed (auto → FullyPaid)</summary>
    Task SendAllPaymentsReceivedAsync(
        AllPaymentsReceivedEmailData data,
        CancellationToken ct);

    /// <summary>Sends "Pago recibido — plazo N de M" for intermediate payment confirmations</summary>
    Task SendPaymentReceivedAsync(
        PaymentReceivedEmailData data,
        CancellationToken ct);

    /// <summary>Sends "Inscripción totalmente confirmada" when board sets Confirmed (from FullyPaid)</summary>
    Task SendRegistrationFinallyConfirmedAsync(
        RegistrationStatusEmailData data,
        CancellationToken ct);

    /// <summary>Sends "Inscripción devuelta a Pendiente" when board reverts registration to Pending</summary>
    Task SendRegistrationRevertedToPendingAsync(
        RegistrationStatusEmailData data,
        CancellationToken ct);

    /// <summary>Sends "Hay cambios en tu inscripción" when board notifies user of Draft changes</summary>
    Task SendDraftChangesNotificationAsync(
        DraftChangesEmailData data,
        CancellationToken ct);

    /// <summary>Sends "Has confirmado los cambios" after user or board force-confirms Draft</summary>
    Task SendDraftChangesConfirmedAsync(
        DraftChangesConfirmedEmailData data,
        CancellationToken ct);

    // ========================================
    // Engagement & Feedback
    // ========================================

    /// <summary>
    /// Sends a feedback request email after camp completion
    /// </summary>
    Task SendFeedbackRequestAsync(
        string toEmail,
        string firstName,
        string campName,
        CancellationToken ct);

    /// <summary>
    /// Sends an event reminder email
    /// </summary>
    Task SendEventReminderAsync(
        string toEmail,
        string firstName,
        string eventName,
        DateTime eventDate,
        CancellationToken ct);
}

/// <summary>
/// Data transfer object for camp registration email notifications
/// </summary>
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

    // First installment payment info (null if payment settings not configured)
    public string? FirstInstallmentConcept { get; init; }
    public decimal? FirstInstallmentAmount { get; init; }
    public string? Iban { get; init; }
    public string? BankName { get; init; }
    public string? AccountHolder { get; init; }
}

/// <summary>
/// Member details for camp registration email notifications
/// </summary>
public record RegistrationMemberEmailData
{
    public required string FullName { get; init; }
    public required string AgeCategory { get; init; }
    public required int AgeAtCamp { get; init; }
    public required string AttendancePeriod { get; init; }
    public required decimal IndividualAmount { get; init; }
}

public record RegistrationStatusEmailData
{
    public required string ToEmail { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string CampName { get; init; }
    public required Guid RegistrationId { get; init; }
    public string? BoardNotes { get; init; }
}

public record DraftChangesEmailData
{
    public required string ToEmail { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string CampName { get; init; }
    public required Guid RegistrationId { get; init; }
    public string? BoardNotes { get; init; }
    public IReadOnlyList<string>? ChangeSummary { get; init; }
}

public record PaymentReceivedEmailData
{
    public required string ToEmail { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string CampName { get; init; }
    public required Guid RegistrationId { get; init; }
    public required int InstallmentNumber { get; init; }
    public required int TotalInstallments { get; init; }
    public required decimal Amount { get; init; }
}

public record AllPaymentsReceivedEmailData
{
    public required string ToEmail { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string CampName { get; init; }
    public required Guid RegistrationId { get; init; }
    public required decimal TotalAmount { get; init; }
}

public record DraftChangesConfirmedEmailData
{
    public required string ToEmail { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string CampName { get; init; }
    public required Guid RegistrationId { get; init; }
    public required string NewStatusEs { get; init; }
}
