namespace Abuvi.Tests.Integration.Common.Services;

using Abuvi.API.Common.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Integration tests for ResendEmailService - actually sends emails to verify Resend integration
/// These tests are SKIPPED by default to avoid sending test emails every test run.
/// To run them, remove the Skip attribute and ensure Resend:ApiKey is configured.
/// </summary>
public class ResendEmailIntegrationTests
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailIntegrationTests()
    {
        // Build configuration from user-secrets
        var builder = new ConfigurationBuilder()
            .AddUserSecrets<ResendEmailIntegrationTests>();

        _configuration = builder.Build();

        // Create logger
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<ResendEmailService>();
    }

    [Fact(
     Skip = "Manual test - sends real email to personal address"
    )]
    public async Task SendVerificationEmail_ToRealAddress_SendsSuccessfully()
    {
        // Arrange
        var apiKey = _configuration["Resend:ApiKey"];

        // Skip test if API key not configured
        if (string.IsNullOrEmpty(apiKey))
        {
            Assert.Fail("Resend:ApiKey not configured in user-secrets. Run: dotnet user-secrets set \"Resend:ApiKey\" \"your-key\"");
        }

        // Setup FromEmail - use onboarding@resend.dev for testing without domain verification
        var fromEmail = _configuration["Resend:FromEmail"] ?? "onboarding@abuvi.org";
        var fromName = _configuration["Resend:FromName"] ?? "ABUVI Test";

        // Configure for testing
        var tempConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Resend:ApiKey"] = apiKey,
                ["Resend:FromEmail"] = fromEmail,
                ["Resend:FromName"] = fromName,
                ["FrontendUrl"] = "http://localhost:5173"
            })
            .Build();

        // Create real ResendClient wrapper
        var resendClient = new ResendClientWrapper(apiKey);
        var emailService = new ResendEmailService(tempConfig, _logger, resendClient);

        var testEmail = "chachosua@gmail.com";
        var testFirstName = "Nestor";
        var testToken = "test_verification_token_123456";

        // Act
        await emailService.SendVerificationEmailAsync(
            testEmail,
            testFirstName,
            testToken,
            CancellationToken.None);

        // Assert
        // If we get here without exception, the email was sent successfully
        Assert.True(true, $"Verification email sent to {testEmail}. Check your inbox!");
    }

    [Fact(
     Skip = "Manual test - sends real welcome email"
    )]
    public async Task SendWelcomeEmail_ToRealAddress_SendsSuccessfully()
    {
        // Arrange
        var apiKey = _configuration["Resend:ApiKey"];

        if (string.IsNullOrEmpty(apiKey))
        {
            Assert.Fail("Resend:ApiKey not configured");
        }

        var fromEmail = _configuration["Resend:FromEmail"] ?? "onboarding@resend.dev";
        var fromName = _configuration["Resend:FromName"] ?? "ABUVI Test";

        var tempConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Resend:ApiKey"] = apiKey,
                ["Resend:FromEmail"] = fromEmail,
                ["Resend:FromName"] = fromName,
                ["FrontendUrl"] = "http://localhost:5173"
            })
            .Build();

        var resendClient = new ResendClientWrapper(apiKey);
        var emailService = new ResendEmailService(tempConfig, _logger, resendClient);

        var testEmail = "chachosua@gmail.com";
        var testFirstName = "Nestor";

        // Act
        await emailService.SendWelcomeEmailAsync(
            testEmail,
            testFirstName,
            CancellationToken.None);

        // Assert
        Assert.True(true, $"Welcome email sent to {testEmail}. Check your inbox!");
    }

    [Fact(Skip = "Manual test - sends real password reset email")]
    public async Task SendPasswordResetEmail_ToRealAddress_SendsSuccessfully()
    {
        // Arrange
        var apiKey = _configuration["Resend:ApiKey"];

        if (string.IsNullOrEmpty(apiKey))
        {
            Assert.Fail("Resend:ApiKey not configured");
        }

        var fromEmail = _configuration["Resend:FromEmail"] ?? "onboarding@resend.dev";
        var fromName = _configuration["Resend:FromName"] ?? "ABUVI Test";

        var tempConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Resend:ApiKey"] = apiKey,
                ["Resend:FromEmail"] = fromEmail,
                ["Resend:FromName"] = fromName,
                ["FrontendUrl"] = "http://localhost:5173"
            })
            .Build();

        var resendClient = new ResendClientWrapper(apiKey);
        var emailService = new ResendEmailService(tempConfig, _logger, resendClient);

        var testEmail = "chachosua@gmail.com";
        var testFirstName = "Nestor";
        var testResetToken = "test_reset_token_789";

        // Act
        await emailService.SendPasswordResetEmailAsync(
            testEmail,
            testFirstName,
            testResetToken,
            CancellationToken.None);

        // Assert
        Assert.True(true, $"Password reset email sent to {testEmail}. Check your inbox!");
    }

    [Fact(Skip = "Manual test - tests current configuration and domain setup")]
    public async Task TestCurrentConfiguration_ShowsConfiguredDomain()
    {
        // Arrange
        var apiKey = _configuration["Resend:ApiKey"];
        var fromEmail = _configuration["Resend:FromEmail"] ?? "noreply@abuvi.org (DEFAULT - NOT VERIFIED!)";
        var fromName = _configuration["Resend:FromName"] ?? "ABUVI Camps (DEFAULT)";

        // Act & Assert
        Console.WriteLine("=== Current Resend Configuration ===");
        Console.WriteLine($"API Key: {(string.IsNullOrEmpty(apiKey) ? "NOT CONFIGURED" : "Configured (re_...)")}");
        Console.WriteLine($"From Email: {fromEmail}");
        Console.WriteLine($"From Name: {fromName}");
        Console.WriteLine();
        Console.WriteLine("=== Next Steps ===");
        Console.WriteLine("1. Go to https://resend.com/domains");
        Console.WriteLine("2. Verify which domain is set up");
        Console.WriteLine("3. Configure it:");
        Console.WriteLine("   dotnet user-secrets set \"Resend:FromEmail\" \"noreply@yourdomain.com\"");
        Console.WriteLine();
        Console.WriteLine("OR for testing without domain verification:");
        Console.WriteLine("   dotnet user-secrets set \"Resend:FromEmail\" \"onboarding@resend.dev\"");

        Assert.True(true);
    }
}
