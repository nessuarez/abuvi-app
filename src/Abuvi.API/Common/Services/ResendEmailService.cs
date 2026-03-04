namespace Abuvi.API.Common.Services;

using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Resend;

/// <summary>
/// Email service implementation using Resend API
/// </summary>
public class ResendEmailService : IEmailService
{
    private readonly IResendClient _resend;
    private readonly ILogger<ResendEmailService> _logger;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly string _frontendUrl;

    public ResendEmailService(
        IConfiguration configuration,
        ILogger<ResendEmailService> logger,
        IResendClient resendClient)
    {
        _logger = logger;

        var apiKey = configuration["Resend:ApiKey"]
            ?? throw new InvalidOperationException(
                "Resend API key not configured. Use: dotnet user-secrets set \"Resend:ApiKey\" \"your-api-key\"");

        _fromEmail = configuration["Resend:FromEmail"] ?? "noreply@abuvi.org";
        _fromName = configuration["Resend:FromName"] ?? "Abuvi Camps";
        _frontendUrl = configuration["FrontendUrl"] ?? "http://localhost:5173";

        _resend = resendClient;
    }

    // ========================================
    // Registration & Authentication
    // ========================================

    public async Task SendVerificationEmailAsync(
        string toEmail,
        string firstName,
        string verificationToken,
        CancellationToken ct)
    {
        var verificationUrl = $"{_frontendUrl}/verify-email?token={verificationToken}";

        var message = new EmailMessage
        {
            From = $"{_fromName} <{_fromEmail}>",
            To = toEmail,
            Subject = "Verifica tu correo electrónico - Campamentos Abuvi",
            HtmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2563eb;'>¡Bienvenido a Abuvi, {firstName}!</h2>
                        <p>Gracias por registrarte. Por favor verifica tu dirección de correo electrónico haciendo clic en el enlace de abajo:</p>
                        <p style='margin: 30px 0;'>
                            <a href=""{verificationUrl}""
                               style='background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                Verificar Correo
                            </a>
                        </p>
                        <p style='color: #666; font-size: 14px;'>Este enlace expirará en 24 horas.</p>
                        <p style='color: #666; font-size: 14px;'>Si no creaste esta cuenta, por favor ignora este correo.</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                        <p style='color: #999; font-size: 12px;'>
                            Saludos cordiales,<br>
                            El equipo de Abuvi
                        </p>
                    </div>
                </body>
                </html>
            "
        };

        try
        {
            var messageId = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Verification email sent to {Email}, Resend ID: {MessageId}",
                toEmail,
                messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email to {Email}", toEmail);
            throw new InvalidOperationException($"Failed to send verification email: {ex.Message}", ex);
        }
    }

    public async Task SendWelcomeEmailAsync(
        string toEmail,
        string firstName,
        CancellationToken ct)
    {
        var dashboardUrl = $"{_frontendUrl}/home";

        var message = new EmailMessage
        {
            From = $"{_fromName} <{_fromEmail}>",
            To = toEmail,
            Subject = "¡Bienvenido a Abuvi!",
            HtmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2563eb;'>¡Tu cuenta está activada, {firstName}! 🎉</h2>
                        <p>Tu correo electrónico ha sido verificado exitosamente. Ahora puedes acceder a todas las funciones de Abuvi.</p>
                        <h3 style='color: #1e40af; margin-top: 30px;'>Próximos pasos:</h3>
                        <ul style='line-height: 2;'>
                            <li>Completa tu perfil</li>
                            <li>Explora los próximos campamentos</li>
                            <li>Registra a los miembros de tu familia</li>
                            <li>Consulta el historial de aniversarios</li>
                        </ul>
                        <p style='margin: 30px 0;'>
                            <a href=""{dashboardUrl}""
                               style='background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                Ir al Inicio
                            </a>
                        </p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                        <p style='color: #999; font-size: 12px;'>
                            Saludos cordiales,<br>
                            El equipo de Abuvi
                        </p>
                    </div>
                </body>
                </html>
            "
        };

        try
        {
            var messageId = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Welcome email sent to {Email}, Resend ID: {MessageId}",
                toEmail,
                messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email to {Email}", toEmail);
            throw new InvalidOperationException($"Failed to send welcome email: {ex.Message}", ex);
        }
    }

    public async Task SendPasswordResetEmailAsync(
        string toEmail,
        string firstName,
        string resetToken,
        CancellationToken ct)
    {
        var resetUrl = $"{_frontendUrl}/reset-password?token={resetToken}";

        var message = new EmailMessage
        {
            From = $"{_fromName} <{_fromEmail}>",
            To = toEmail,
            Subject = "Restablece tu contraseña - Abuvi",
            HtmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2563eb;'>Restablecimiento de Contraseña</h2>
                        <p>Hola {firstName},</p>
                        <p>Recibimos una solicitud para restablecer tu contraseña. Haz clic en el botón de abajo para crear una nueva contraseña:</p>
                        <p style='margin: 30px 0;'>
                            <a href=""{resetUrl}""
                               style='background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                Restablecer Contraseña
                            </a>
                        </p>
                        <p style='color: #666; font-size: 14px;'>Este enlace expirará en 1 hora.</p>
                        <p style='color: #dc2626; font-size: 14px;'>
                            <strong>Aviso de seguridad:</strong> Si no solicitaste restablecer tu contraseña, por favor ignora este correo y tu contraseña permanecerá sin cambios.
                        </p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                        <p style='color: #999; font-size: 12px;'>
                            Saludos cordiales,<br>
                            El equipo de Abuvi
                        </p>
                    </div>
                </body>
                </html>
            "
        };

        try
        {
            var messageId = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Password reset email sent to {Email}, Resend ID: {MessageId}",
                toEmail,
                messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", toEmail);
            throw new InvalidOperationException($"Failed to send password reset email: {ex.Message}", ex);
        }
    }

    // ========================================
    // Camp Management
    // ========================================

    public async Task SendCampRegistrationConfirmationAsync(
        CampRegistrationEmailData data,
        CancellationToken ct)
    {
        var culture = new CultureInfo("es-ES");
        var registrationUrl = $"{_frontendUrl}/registrations/{data.RegistrationId}";
        var startDate = data.StartDate.ToString("d 'de' MMMM", culture);
        var endDate = data.EndDate.ToString("d 'de' MMMM 'de' yyyy", culture);
        var recipientName = WebUtility.HtmlEncode(data.RecipientFirstName);

        var membersHtml = new StringBuilder();
        foreach (var member in data.Members)
        {
            var name = WebUtility.HtmlEncode(member.FullName);
            membersHtml.Append($@"
                <tr>
                    <td style='padding: 8px; border-bottom: 1px solid #eee;'>{name}</td>
                    <td style='padding: 8px; border-bottom: 1px solid #eee;'>{member.AgeCategory} ({member.AgeAtCamp} años)</td>
                    <td style='padding: 8px; border-bottom: 1px solid #eee;'>{member.AttendancePeriod}</td>
                    <td style='padding: 8px; border-bottom: 1px solid #eee; text-align: right;'>{member.IndividualAmount.ToString("N2", culture)} €</td>
                </tr>");
        }

        var extrasHtml = data.ExtrasAmount > 0
            ? $"<p style='margin: 5px 0;'>Extras: <strong>{data.ExtrasAmount.ToString("N2", culture)} €</strong></p>"
            : "";

        var specialNeedsHtml = !string.IsNullOrWhiteSpace(data.SpecialNeeds)
            ? $"<p style='margin: 5px 0;'><strong>Necesidades especiales:</strong> {WebUtility.HtmlEncode(data.SpecialNeeds)}</p>"
            : "";

        var campatesHtml = !string.IsNullOrWhiteSpace(data.CampatesPreference)
            ? $"<p style='margin: 5px 0;'><strong>Preferencia de compañeros:</strong> {WebUtility.HtmlEncode(data.CampatesPreference)}</p>"
            : "";

        var message = new EmailMessage
        {
            From = $"{_fromName} <{_fromEmail}>",
            To = data.ToEmail,
            Subject = $"Inscripción confirmada — Campamento {data.Year}",
            HtmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #16a34a;'>¡Inscripción Confirmada! ✓</h2>
                        <p>Hola {recipientName},</p>
                        <p>Tu inscripción en el campamento <strong>{WebUtility.HtmlEncode(data.CampName)}</strong> ha sido registrada correctamente.</p>

                        <div style='background-color: #f3f4f6; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <p style='margin: 5px 0;'><strong>Campamento:</strong> {WebUtility.HtmlEncode(data.CampName)}</p>
                            <p style='margin: 5px 0;'><strong>Ubicación:</strong> {WebUtility.HtmlEncode(data.CampLocation)}</p>
                            <p style='margin: 5px 0;'><strong>Fechas:</strong> {startDate} — {endDate}</p>
                        </div>

                        <h3 style='color: #1e40af; margin-top: 25px;'>Participantes</h3>
                        <table style='width: 100%; border-collapse: collapse; margin: 10px 0;'>
                            <thead>
                                <tr style='background-color: #f3f4f6;'>
                                    <th style='padding: 8px; text-align: left;'>Nombre</th>
                                    <th style='padding: 8px; text-align: left;'>Categoría</th>
                                    <th style='padding: 8px; text-align: left;'>Periodo</th>
                                    <th style='padding: 8px; text-align: right;'>Importe</th>
                                </tr>
                            </thead>
                            <tbody>
                                {membersHtml}
                            </tbody>
                        </table>

                        <div style='background-color: #f0fdf4; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <p style='margin: 5px 0;'>Base inscripción: <strong>{data.BaseTotalAmount.ToString("N2", culture)} €</strong></p>
                            {extrasHtml}
                            <hr style='border: none; border-top: 1px solid #ccc; margin: 10px 0;' />
                            <p style='margin: 5px 0; font-size: 18px;'>Total: <strong>{data.TotalAmount.ToString("N2", culture)} €</strong></p>
                        </div>

                        {specialNeedsHtml}
                        {campatesHtml}

                        <p style='margin: 30px 0;'>
                            <a href=""{registrationUrl}""
                               style='background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                Ver mi inscripción
                            </a>
                        </p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                        <p style='color: #999; font-size: 12px;'>
                            Saludos cordiales,<br>
                            El equipo de Abuvi
                        </p>
                    </div>
                </body>
                </html>
            "
        };

        try
        {
            var messageId = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Camp registration confirmation sent to {Email} for camp {CampName}, Resend ID: {MessageId}",
                data.ToEmail,
                data.CampName,
                messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send camp registration confirmation to {Email}", data.ToEmail);
            throw new InvalidOperationException($"Failed to send camp registration confirmation: {ex.Message}", ex);
        }
    }

    public async Task SendCampRegistrationCancellationAsync(
        CampRegistrationEmailData data,
        CancellationToken ct)
    {
        var culture = new CultureInfo("es-ES");
        var campUrl = $"{_frontendUrl}/camp";
        var startDate = data.StartDate.ToString("d 'de' MMMM", culture);
        var endDate = data.EndDate.ToString("d 'de' MMMM 'de' yyyy", culture);
        var recipientName = WebUtility.HtmlEncode(data.RecipientFirstName);

        var message = new EmailMessage
        {
            From = $"{_fromName} <{_fromEmail}>",
            To = data.ToEmail,
            Subject = $"Inscripción cancelada — Campamento {data.Year}",
            HtmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #dc2626;'>Inscripción Cancelada</h2>
                        <p>Hola {recipientName},</p>
                        <p>Tu inscripción en el campamento <strong>{WebUtility.HtmlEncode(data.CampName)}</strong> ({startDate} — {endDate}) ha sido cancelada.</p>
                        <p>Si esto fue un error o deseas volver a inscribirte, puedes hacerlo desde nuestra web.</p>
                        <p style='margin: 30px 0;'>
                            <a href=""{campUrl}""
                               style='background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                Ver campamento
                            </a>
                        </p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                        <p style='color: #999; font-size: 12px;'>
                            Saludos cordiales,<br>
                            El equipo de Abuvi
                        </p>
                    </div>
                </body>
                </html>
            "
        };

        try
        {
            var messageId = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Camp registration cancellation sent to {Email} for camp {CampName}, Resend ID: {MessageId}",
                data.ToEmail,
                data.CampName,
                messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send camp registration cancellation to {Email}", data.ToEmail);
            throw new InvalidOperationException($"Failed to send camp registration cancellation: {ex.Message}", ex);
        }
    }

    public async Task SendCampUpdateNotificationAsync(
        string toEmail,
        string firstName,
        string campName,
        string updateMessage,
        CancellationToken ct)
    {
        var message = new EmailMessage
        {
            From = $"{_fromName} <{_fromEmail}>",
            To = toEmail,
            Subject = $"Update: {campName}",
            HtmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2563eb;'>Camp Update</h2>
                        <p>Hello {firstName},</p>
                        <p>We have an important update regarding <strong>{campName}</strong>:</p>
                        <div style='background-color: #fef3c7; border-left: 4px solid #f59e0b; padding: 15px; margin: 20px 0;'>
                            <p style='margin: 0;'>{updateMessage}</p>
                        </div>
                        <p>If you have any questions, please don't hesitate to contact us.</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                        <p style='color: #999; font-size: 12px;'>
                            Best regards,<br>
                            The Abuvi Team
                        </p>
                    </div>
                </body>
                </html>
            "
        };

        try
        {
            var messageId = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Camp update notification sent to {Email} for {CampName}, Resend ID: {MessageId}",
                toEmail,
                campName,
                messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send camp update notification to {Email}", toEmail);
            throw new InvalidOperationException($"Failed to send camp update notification: {ex.Message}", ex);
        }
    }

    // ========================================
    // Payments
    // ========================================

    public async Task SendPaymentReceiptAsync(
        string toEmail,
        string firstName,
        decimal amount,
        string paymentReference,
        CancellationToken ct)
    {
        var paymentsUrl = $"{_frontendUrl}/payments";
        var formattedAmount = amount.ToString("F2");

        var message = new EmailMessage
        {
            From = $"{_fromName} <{_fromEmail}>",
            To = toEmail,
            Subject = $"Payment Received - €{formattedAmount}",
            HtmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #16a34a;'>Payment Received ✓</h2>
                        <p>Hello {firstName},</p>
                        <p>We have successfully received your payment.</p>
                        <div style='background-color: #f3f4f6; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <p style='margin: 5px 0;'><strong>Amount:</strong> €{formattedAmount}</p>
                            <p style='margin: 5px 0;'><strong>Reference:</strong> {paymentReference}</p>
                            <p style='margin: 5px 0;'><strong>Date:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</p>
                        </div>
                        <p>Thank you for your payment!</p>
                        <p style='margin: 30px 0;'>
                            <a href=""{paymentsUrl}""
                               style='background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                View Payment History
                            </a>
                        </p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                        <p style='color: #999; font-size: 12px;'>
                            Best regards,<br>
                            The Abuvi Team
                        </p>
                    </div>
                </body>
                </html>
            "
        };

        try
        {
            var messageId = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Payment receipt sent to {Email}, Amount: {Amount}, Reference: {Reference}, Resend ID: {MessageId}",
                toEmail,
                amount,
                paymentReference,
                messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment receipt to {Email}", toEmail);
            throw new InvalidOperationException($"Failed to send payment receipt: {ex.Message}", ex);
        }
    }

    // ========================================
    // Engagement & Feedback
    // ========================================

    public async Task SendFeedbackRequestAsync(
        string toEmail,
        string firstName,
        string campName,
        CancellationToken ct)
    {
        var feedbackUrl = $"{_frontendUrl}/feedback";

        var message = new EmailMessage
        {
            From = $"{_fromName} <{_fromEmail}>",
            To = toEmail,
            Subject = "We value your feedback - Abuvi Camps",
            HtmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2563eb;'>How was your experience?</h2>
                        <p>Hello {firstName},</p>
                        <p>Thank you for attending <strong>{campName}</strong>! We hope you had a wonderful experience.</p>
                        <p>We'd love to hear your feedback to help us improve our camps for future participants.</p>
                        <p style='margin: 30px 0;'>
                            <a href=""{feedbackUrl}""
                               style='background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                Share Your Feedback
                            </a>
                        </p>
                        <p style='color: #666; font-size: 14px;'>Your feedback helps us create better experiences for everyone.</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                        <p style='color: #999; font-size: 12px;'>
                            Best regards,<br>
                            The Abuvi Team
                        </p>
                    </div>
                </body>
                </html>
            "
        };

        try
        {
            var messageId = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Feedback request sent to {Email} for camp {CampName}, Resend ID: {MessageId}",
                toEmail,
                campName,
                messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send feedback request to {Email}", toEmail);
            throw new InvalidOperationException($"Failed to send feedback request: {ex.Message}", ex);
        }
    }

    public async Task SendEventReminderAsync(
        string toEmail,
        string firstName,
        string eventName,
        DateTime eventDate,
        CancellationToken ct)
    {
        var formattedDate = eventDate.ToString("dddd, MMMM d, yyyy 'at' h:mm tt");

        var message = new EmailMessage
        {
            From = $"{_fromName} <{_fromEmail}>",
            To = toEmail,
            Subject = $"Reminder: {eventName} is coming up!",
            HtmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2563eb;'>Reminder: {eventName}</h2>
                        <p>Hello {firstName},</p>
                        <p>This is a friendly reminder about the upcoming event:</p>
                        <div style='background-color: #dbeafe; border-left: 4px solid #2563eb; padding: 15px; margin: 20px 0;'>
                            <p style='margin: 5px 0; font-size: 18px;'><strong>{eventName}</strong></p>
                            <p style='margin: 5px 0;'>📅 {formattedDate}</p>
                        </div>
                        <p>We look forward to seeing you there!</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                        <p style='color: #999; font-size: 12px;'>
                            Best regards,<br>
                            The Abuvi Team
                        </p>
                    </div>
                </body>
                </html>
            "
        };

        try
        {
            var messageId = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Event reminder sent to {Email} for event {EventName}, Resend ID: {MessageId}",
                toEmail,
                eventName,
                messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send event reminder to {Email}", toEmail);
            throw new InvalidOperationException($"Failed to send event reminder: {ex.Message}", ex);
        }
    }
}
