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
