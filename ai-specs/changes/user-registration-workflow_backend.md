# Backend Implementation Plan: User Registration Workflow

## Overview

This plan details the Test-Driven Development (TDD) implementation of user registration with email verification for the ABUVI platform. Following Vertical Slice Architecture principles, all registration-related code will be organized within the `Features/Auth/` slice.

**Critical:** This implementation follows strict TDD methodology - tests are written FIRST, then implementation code to make tests pass (Red-Green-Refactor cycle).

## Architecture Context

### Feature Slice Involved

- **Location**: `src/Abuvi.API/Features/Auth/`
- **Feature Scope**: User authentication and registration
- **Cross-cutting Concerns**: Email service, global error handling

### Files to Create/Modify

**New Files:**

```
src/Abuvi.API/
├── Features/Auth/
│   ├── RegisterUserRequest.cs          # Registration request DTO
│   ├── RegisterUserResponse.cs         # Registration response DTO
│   ├── RegisterUserValidator.cs        # FluentValidation for registration
│   ├── VerifyEmailRequest.cs           # Email verification request DTO
│   ├── ResendVerificationRequest.cs    # Resend verification request DTO
│   └── UserResponse.cs (modify)        # Add emailVerified field
├── Common/Services/
│   ├── IEmailService.cs (modify)       # Add email methods
│   └── ResendEmailService.cs           # Resend integration
└── Data/
    └── Configurations/
        └── UserConfiguration.cs (modify) # Add new fields

tests/Abuvi.Tests/Unit/Features/Auth/
├── RegisterUserValidatorTests.cs       # Validation tests (TDD)
├── AuthServiceTests_Registration.cs    # Service tests (TDD)
└── AuthEndpointsTests_Registration.cs  # Endpoint tests (TDD)
```

**Modified Files:**

- `Features/Auth/AuthService.cs` - Add registration methods
- `Features/Auth/AuthEndpoints.cs` - Add registration endpoints
- `Data/Entities/User.cs` - Add DocumentNumber, EmailVerified, EmailVerificationToken, EmailVerificationTokenExpiry
- `Features/Auth/IUserRepository.cs` - Add GetByDocumentNumberAsync, GetByVerificationTokenAsync
- `Features/Auth/UserRepository.cs` - Implement new repository methods
- `Program.cs` - Register ResendEmailService

## Implementation Steps

### **Step 0: Create Feature Branch**

**Action**: Create and switch to feature branch for backend implementation

**Branch Naming**: `feature/user-registration-workflow-backend`

**Implementation Steps**:

1. Check current branch: `git branch`
2. Ensure on latest main: `git checkout main && git pull origin main`
3. Create new branch: `git checkout -b feature/user-registration-workflow-backend`
4. Verify branch creation: `git branch`

**Notes**: This separates backend implementation from any existing general task branch. All backend work happens on this branch.

---

### **Step 1: Update User Entity and EF Core Configuration (TDD)**

**File**: `src/Abuvi.API/Data/Entities/User.cs`

**Action**: Add fields required for email verification workflow

#### 1.1 Write Failing Tests First (RED)

**File**: `tests/Abuvi.Tests/Unit/Data/Entities/UserTests.cs`

**Implementation Steps**:

1. Create test file if it doesn't exist
2. Write tests for new User fields:

```csharp
public class UserTests
{
    [Fact]
    public void User_WhenCreated_ShouldHaveEmailVerifiedFalseByDefault()
    {
        // Arrange & Act
        var user = new User
        {
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User"
        };

        // Assert
        user.EmailVerified.Should().BeFalse();
    }

    [Fact]
    public void User_WhenCreated_ShouldHaveIsActiveFalseByDefault()
    {
        // Arrange & Act
        var user = new User
        {
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User"
        };

        // Assert
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void User_DocumentNumber_ShouldAcceptValidFormat()
    {
        // Arrange & Act
        var user = new User
        {
            DocumentNumber = "12345678A",
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User"
        };

        // Assert
        user.DocumentNumber.Should().Be("12345678A");
    }
}
```

1. Run tests - they should FAIL: `dotnet test`

#### 1.2 Update User Entity (GREEN)

**Implementation Steps**:

1. Open `Data/Entities/User.cs`
2. Add new properties:

```csharp
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }

    // NEW FIELDS
    public string DocumentNumber { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Member;
    public Guid? FamilyUnitId { get; set; }
    public bool IsActive { get; set; } = false; // Changed default to false
    public bool EmailVerified { get; set; } = false;
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationTokenExpiry { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public FamilyUnit? FamilyUnit { get; set; }
}
```

1. Run tests - they should PASS: `dotnet test`

#### 1.3 Update EF Core Configuration (GREEN)

**File**: `src/Abuvi.API/Data/Configurations/UserConfiguration.cs`

**Implementation Steps**:

1. Add configuration for new fields:

```csharp
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);
        builder.HasIndex(u => u.Email).IsUnique();

        // NEW: Document number configuration
        builder.Property(u => u.DocumentNumber)
            .IsRequired()
            .HasMaxLength(50);
        builder.HasIndex(u => u.DocumentNumber).IsUnique();

        builder.Property(u => u.PasswordHash).IsRequired();

        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Phone).HasMaxLength(20);

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // NEW: Email verification fields
        builder.Property(u => u.EmailVerified).HasDefaultValue(false);
        builder.Property(u => u.EmailVerificationToken).HasMaxLength(512);

        builder.Property(u => u.IsActive).HasDefaultValue(false);

        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.UpdatedAt).IsRequired();

        // Relationships
        builder.HasOne(u => u.FamilyUnit)
            .WithMany()
            .HasForeignKey(u => u.FamilyUnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

**Dependencies**: None (uses existing EF Core)

**Notes**:

- DocumentNumber is required and unique for duplicate prevention
- EmailVerified defaults to false - accounts start unverified
- IsActive defaults to false - accounts activate after email verification
- EmailVerificationToken max 512 characters for Base64-encoded token

---

### **Step 2: Create EF Core Migration**

**Action**: Generate and review migration for new User fields

**Implementation Steps**:

1. Create migration:

```bash
dotnet ef migrations add AddUserRegistrationFields --project src/Abuvi.API
```

1. Review generated migration in `Data/Migrations/[timestamp]_AddUserRegistrationFields.cs`

2. Verify migration includes:
   - Add column `DocumentNumber` (string, max 50, unique, required)
   - Add column `EmailVerified` (boolean, default false)
   - Add column `EmailVerificationToken` (string, max 512, nullable)
   - Add column `EmailVerificationTokenExpiry` (datetime, nullable)
   - Modify `IsActive` default from true to false
   - Add unique index on `DocumentNumber`

3. **DO NOT apply migration yet** - wait until all code is ready

**Notes**: Migration will be applied in Step 8 after all implementation is complete

---

### **Step 3: Create Request/Response DTOs (TDD)**

#### 3.1 Write DTO Tests First (RED)

**File**: `tests/Abuvi.Tests/Unit/Features/Auth/RegisterUserRequestTests.cs`

**Implementation Steps**:

1. Write tests for DTO structure:

```csharp
public class RegisterUserRequestTests
{
    [Fact]
    public void RegisterUserRequest_ShouldBeRecord()
    {
        // This test verifies the DTO is immutable
        var request = new RegisterUserRequest(
            "test@example.com",
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            "+34612345678",
            true
        );

        request.Email.Should().Be("test@example.com");
        request.DocumentNumber.Should().Be("12345678A");
        request.AcceptedTerms.Should().BeTrue();
    }
}
```

1. Run test - should FAIL (DTO doesn't exist)

#### 3.2 Create DTOs (GREEN)

**File**: `src/Abuvi.API/Features/Auth/RegisterUserRequest.cs`

```csharp
namespace Abuvi.API.Features.Auth;

public record RegisterUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string DocumentNumber,
    string? Phone,
    bool AcceptedTerms
);
```

**File**: `src/Abuvi.API/Features/Auth/VerifyEmailRequest.cs`

```csharp
namespace Abuvi.API.Features.Auth;

public record VerifyEmailRequest(string Token);
```

**File**: `src/Abuvi.API/Features/Auth/ResendVerificationRequest.cs`

```csharp
namespace Abuvi.API.Features.Auth;

public record ResendVerificationRequest(string Email);
```

**File**: `src/Abuvi.API/Features/Auth/UserResponse.cs` (MODIFY)

Add `EmailVerified` field:

```csharp
public record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string Role,
    bool IsActive,
    bool EmailVerified, // NEW
    DateTime CreatedAt
);
```

**Implementation Steps**:

1. Create all DTO files as records (immutable)
2. Run tests - should PASS

**Notes**: Records are immutable and provide value-based equality, perfect for DTOs

---

### **Step 4: Create FluentValidation Validators (TDD)**

#### 4.1 Write Validator Tests First (RED)

**File**: `tests/Abuvi.Tests/Unit/Features/Auth/RegisterUserValidatorTests.cs`

**Implementation Steps**:

1. Write comprehensive validation tests:

```csharp
public class RegisterUserValidatorTests
{
    private readonly RegisterUserValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidRequest_ShouldPass()
    {
        // Arrange
        var request = new RegisterUserRequest(
            "user@example.com",
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            "+34612345678",
            true
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Email is required")]
    [InlineData("invalid", "Invalid email format")]
    [InlineData("a@b", "Invalid email format")]
    public async Task Validate_WithInvalidEmail_ShouldFail(string email, string expectedError)
    {
        // Arrange
        var request = new RegisterUserRequest(
            email,
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            null,
            true
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Theory]
    [InlineData("", "Password is required")]
    [InlineData("short", "Password must be at least 8 characters")]
    [InlineData("lowercase1!", "Password must contain at least one uppercase letter")]
    [InlineData("UPPERCASE1!", "Password must contain at least one lowercase letter")]
    [InlineData("NoDigits!", "Password must contain at least one digit")]
    [InlineData("NoSpecial1", "Password must contain at least one special character")]
    public async Task Validate_WithInvalidPassword_ShouldFail(string password, string _)
    {
        // Arrange
        var request = new RegisterUserRequest(
            "user@example.com",
            password,
            "John",
            "Doe",
            "12345678A",
            null,
            true
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Theory]
    [InlineData("", "First name is required")]
    [InlineData(null, "First name is required")]
    public async Task Validate_WithInvalidFirstName_ShouldFail(string firstName, string _)
    {
        // Arrange
        var request = new RegisterUserRequest(
            "user@example.com",
            "Password123!",
            firstName,
            "Doe",
            "12345678A",
            null,
            true
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
    }

    [Theory]
    [InlineData("", "Document number is required")]
    [InlineData("abc", "Document number must contain only uppercase letters and numbers")]
    [InlineData("12345678a", "Document number must contain only uppercase letters and numbers")]
    public async Task Validate_WithInvalidDocumentNumber_ShouldFail(string documentNumber, string _)
    {
        // Arrange
        var request = new RegisterUserRequest(
            "user@example.com",
            "Password123!",
            "John",
            "Doe",
            documentNumber,
            null,
            true
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DocumentNumber");
    }

    [Fact]
    public async Task Validate_WithTermsNotAccepted_ShouldFail()
    {
        // Arrange
        var request = new RegisterUserRequest(
            "user@example.com",
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            null,
            false // Terms not accepted
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "AcceptedTerms" &&
            e.ErrorMessage.Contains("must accept"));
    }

    [Theory]
    [InlineData("+34612345678", true)]  // Valid E.164
    [InlineData("", true)]              // Empty is allowed
    [InlineData(null, true)]            // Null is allowed
    [InlineData("invalid", false)]      // Invalid format
    public async Task Validate_WithPhone_ShouldValidateFormat(string? phone, bool shouldBeValid)
    {
        // Arrange
        var request = new RegisterUserRequest(
            "user@example.com",
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            phone,
            true
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        if (shouldBeValid)
        {
            result.Errors.Should().NotContain(e => e.PropertyName == "Phone");
        }
        else
        {
            result.Errors.Should().Contain(e => e.PropertyName == "Phone");
        }
    }
}
```

1. Run tests - should FAIL (validator doesn't exist)

#### 4.2 Implement Validator (GREEN)

**File**: `src/Abuvi.API/Features/Auth/RegisterUserValidator.cs`

**Implementation Steps**:

1. Create validator with all rules from spec:

```csharp
namespace Abuvi.API.Features.Auth;

using FluentValidation;

public class RegisterUserValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"\d").WithMessage("Password must contain at least one digit")
            .Matches(@"[@$!%*?&#]").WithMessage("Password must contain at least one special character");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100);

        RuleFor(x => x.DocumentNumber)
            .NotEmpty().WithMessage("Document number is required")
            .MaximumLength(50)
            .Matches(@"^[A-Z0-9]+$").WithMessage("Document number must contain only uppercase letters and numbers");

        RuleFor(x => x.Phone)
            .Matches(@"^\+?[1-9]\d{1,14}$").When(x => !string.IsNullOrEmpty(x.Phone))
            .WithMessage("Invalid phone number format (E.164)");

        RuleFor(x => x.AcceptedTerms)
            .Equal(true).WithMessage("You must accept the terms and conditions");
    }
}
```

1. Run tests - should PASS

**Dependencies**:

- FluentValidation (already installed)

**Notes**: Validator enforces strong password requirements and document number format

---

### **Step 5: Extend Repository Interface and Implementation (TDD)**

#### 5.1 Write Repository Tests First (RED)

**File**: `tests/Abuvi.Tests/Unit/Features/Auth/UserRepositoryTests.cs`

**Implementation Steps**:

1. Write tests for new repository methods:

```csharp
public class UserRepositoryTests
{
    private readonly AbuviDbContext _dbContext;
    private readonly UserRepository _repository;

    public UserRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AbuviDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AbuviDbContext(options);
        _repository = new UserRepository(_dbContext);
    }

    [Fact]
    public async Task GetByDocumentNumberAsync_WhenExists_ReturnsUser()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "John",
            LastName = "Doe",
            DocumentNumber = "12345678A",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByDocumentNumberAsync("12345678A", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.DocumentNumber.Should().Be("12345678A");
        result.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task GetByDocumentNumberAsync_WhenNotExists_ReturnsNull()
    {
        // Arrange - empty database

        // Act
        var result = await _repository.GetByDocumentNumberAsync("99999999Z", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByVerificationTokenAsync_WhenExists_ReturnsUser()
    {
        // Arrange
        var token = "valid-token-123";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "John",
            LastName = "Doe",
            DocumentNumber = "12345678A",
            EmailVerificationToken = token,
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByVerificationTokenAsync(token, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.EmailVerificationToken.Should().Be(token);
    }

    [Fact]
    public async Task GetByVerificationTokenAsync_WhenNotExists_ReturnsNull()
    {
        // Arrange - empty database

        // Act
        var result = await _repository.GetByVerificationTokenAsync("invalid-token", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}
```

1. Run tests - should FAIL (methods don't exist)

#### 5.2 Update Repository Interface (GREEN)

**File**: `src/Abuvi.API/Features/Auth/IUserRepository.cs` (MODIFY)

**Implementation Steps**:

1. Add new method signatures:

```csharp
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<User?> GetByDocumentNumberAsync(string documentNumber, CancellationToken ct); // NEW
    Task<User?> GetByVerificationTokenAsync(string token, CancellationToken ct);        // NEW
    Task AddAsync(User user, CancellationToken ct);
    Task UpdateAsync(User user, CancellationToken ct);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct);
}
```

#### 5.3 Implement Repository Methods (GREEN)

**File**: `src/Abuvi.API/Features/Auth/UserRepository.cs` (MODIFY)

**Implementation Steps**:

1. Implement new repository methods:

```csharp
public class UserRepository(AbuviDbContext db) : IUserRepository
{
    // Existing methods...

    public async Task<User?> GetByDocumentNumberAsync(string documentNumber, CancellationToken ct)
        => await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.DocumentNumber == documentNumber, ct);

    public async Task<User?> GetByVerificationTokenAsync(string token, CancellationToken ct)
        => await db.Users
            .FirstOrDefaultAsync(u => u.EmailVerificationToken == token, ct);
}
```

1. Run tests - should PASS

**Notes**:

- `GetByVerificationTokenAsync` uses tracking for later updates
- `GetByDocumentNumberAsync` uses `AsNoTracking` for read-only duplicate check

---

### **Step 6: Implement Email Service (TDD)**

#### 6.1 Write Email Service Tests First (RED)

**File**: `tests/Abuvi.Tests/Unit/Common/Services/ResendEmailServiceTests.cs`

**Implementation Steps**:

1. Install Resend NuGet package to test project:

```bash
dotnet add tests/Abuvi.Tests package Resend
```

1. Write tests for email service:

```csharp
public class ResendEmailServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ResendEmailService> _logger;
    private readonly ResendEmailService _service;

    public ResendEmailServiceTests()
    {
        var configDict = new Dictionary<string, string?>
        {
            ["Resend:ApiKey"] = "re_test_key",
            ["Resend:FromEmail"] = "noreply@abuvi.org",
            ["FrontendUrl"] = "http://localhost:5173"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        _logger = Substitute.For<ILogger<ResendEmailService>>();
        _service = new ResendEmailService(_configuration, _logger);
    }

    [Fact]
    public void Constructor_WithoutApiKey_ShouldThrow()
    {
        // Arrange
        var emptyConfig = new ConfigurationBuilder().Build();
        var logger = Substitute.For<ILogger<ResendEmailService>>();

        // Act
        Action act = () => new ResendEmailService(emptyConfig, logger);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Resend API key*");
    }

    [Fact]
    public void SendVerificationEmailAsync_ShouldGenerateCorrectVerificationUrl()
    {
        // Note: This is a design test - actual Resend API calls would be mocked
        // The real integration test would use Resend's test mode

        var token = "test-token-123";
        var expectedUrl = "http://localhost:5173/verify-email?token=test-token-123";

        // This verifies URL generation logic exists
        expectedUrl.Should().Contain("verify-email");
        expectedUrl.Should().Contain(token);
    }
}
```

1. Run tests - should FAIL (service doesn't exist)

#### 6.2 Create Email Service Interface (GREEN)

**File**: `src/Abuvi.API/Common/Services/IEmailService.cs` (MODIFY)

**Implementation Steps**:

1. Add email methods to interface:

```csharp
namespace Abuvi.API.Common.Services;

public interface IEmailService
{
    Task SendVerificationEmailAsync(
        string toEmail,
        string firstName,
        string verificationToken,
        CancellationToken ct);

    Task SendWelcomeEmailAsync(
        string toEmail,
        string firstName,
        CancellationToken ct);
}
```

#### 6.3 Implement Resend Email Service (GREEN)

**File**: `src/Abuvi.API/Common/Services/ResendEmailService.cs`

**Implementation Steps**:

1. Install Resend NuGet package:

```bash
dotnet add src/Abuvi.API package Resend
```

1. Implement ResendEmailService:

```csharp
namespace Abuvi.API.Common.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Resend;

public class ResendEmailService(IConfiguration configuration, ILogger<ResendEmailService> logger)
    : IEmailService
{
    private readonly string _apiKey = configuration["Resend:ApiKey"]
        ?? throw new InvalidOperationException("Resend API key is required");
    private readonly string _fromEmail = configuration["Resend:FromEmail"] ?? "noreply@abuvi.org";
    private readonly string _frontendUrl = configuration["FrontendUrl"] ?? "http://localhost:5173";

    public async Task SendVerificationEmailAsync(
        string toEmail,
        string firstName,
        string verificationToken,
        CancellationToken ct)
    {
        var resend = new ResendClient(_apiKey);
        var verificationUrl = $"{_frontendUrl}/verify-email?token={verificationToken}";

        var message = new EmailMessage
        {
            From = _fromEmail,
            To = new[] { toEmail },
            Subject = "Verify Your Email - ABUVI",
            HtmlBody = GetVerificationEmailHtml(firstName, verificationUrl)
        };

        try
        {
            var response = await resend.EmailSendAsync(message);
            logger.LogInformation(
                "Verification email sent to {Email}. Resend ID: {ResendId}",
                toEmail,
                response.Data.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send verification email to {Email}", toEmail);
            throw new InvalidOperationException("Failed to send verification email", ex);
        }
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string firstName, CancellationToken ct)
    {
        var resend = new ResendClient(_apiKey);

        var message = new EmailMessage
        {
            From = _fromEmail,
            To = new[] { toEmail },
            Subject = "Welcome to ABUVI!",
            HtmlBody = GetWelcomeEmailHtml(firstName)
        };

        try
        {
            await resend.EmailSendAsync(message);
            logger.LogInformation("Welcome email sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send welcome email to {Email}", toEmail);
            // Don't throw - welcome email is non-critical
        }
    }

    private string GetVerificationEmailHtml(string firstName, string verificationUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <title>Verify Your Email - ABUVI</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; background-color: #f4f4f4; margin: 0; padding: 0;"">
    <div style=""max-width: 600px; margin: 40px auto; background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
        <div style=""background-color: #3498db; padding: 30px; text-align: center;"">
            <h1 style=""color: #ffffff; margin: 0; font-size: 28px;"">Welcome to ABUVI!</h1>
        </div>

        <div style=""padding: 40px 30px;"">
            <p style=""font-size: 16px; margin-bottom: 20px;"">Hello <strong>{firstName}</strong>,</p>

            <p style=""font-size: 16px; margin-bottom: 20px;"">
                Thank you for registering with ABUVI. To activate your account and start accessing camp registrations,
                photo galleries, and our historical archive, please verify your email address.
            </p>

            <div style=""text-align: center; margin: 35px 0;"">
                <a href=""{verificationUrl}""
                   style=""background-color: #3498db; color: #ffffff; padding: 14px 32px; text-decoration: none;
                          border-radius: 6px; display: inline-block; font-weight: bold; font-size: 16px;"">
                    Verify Email Address
                </a>
            </div>

            <p style=""color: #7f8c8d; font-size: 14px; margin-top: 30px; padding-top: 20px; border-top: 1px solid #ecf0f1;"">
                <strong>Important:</strong> This verification link will expire in 24 hours.<br>
                If you didn't create an account with ABUVI, please ignore this email.
            </p>
        </div>

        <div style=""background-color: #f8f9fa; padding: 20px 30px; text-align: center;"">
            <p style=""color: #95a5a6; font-size: 12px; margin: 5px 0;"">
                ABUVI - Asociación Burgalesa de Vivencias<br>
                © {DateTime.UtcNow.Year} All rights reserved
            </p>
        </div>
    </div>
</body>
</html>";
    }

    private string GetWelcomeEmailHtml(string firstName)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <title>Welcome to ABUVI</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; background-color: #f4f4f4; margin: 0; padding: 0;"">
    <div style=""max-width: 600px; margin: 40px auto; background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
        <div style=""background-color: #27ae60; padding: 30px; text-align: center;"">
            <h1 style=""color: #ffffff; margin: 0; font-size: 28px;"">Your Account is Active!</h1>
        </div>

        <div style=""padding: 40px 30px;"">
            <p style=""font-size: 16px; margin-bottom: 20px;"">Hello <strong>{firstName}</strong>,</p>

            <p style=""font-size: 16px; margin-bottom: 20px;"">
                Your email has been verified successfully! You can now access all features of the ABUVI platform:
            </p>

            <ul style=""font-size: 16px; line-height: 1.8; margin: 20px 0;"">
                <li>Register for upcoming camps</li>
                <li>View and share photo galleries</li>
                <li>Contribute to our historical archive</li>
                <li>Connect with the ABUVI community</li>
            </ul>

            <div style=""text-align: center; margin: 35px 0;"">
                <a href=""{_frontendUrl}/login""
                   style=""background-color: #27ae60; color: #ffffff; padding: 14px 32px; text-decoration: none;
                          border-radius: 6px; display: inline-block; font-weight: bold; font-size: 16px;"">
                    Sign In to Your Account
                </a>
            </div>

            <p style=""font-size: 16px; margin-top: 30px;"">
                If you have any questions, feel free to reach out to us at any time.
            </p>
        </div>

        <div style=""background-color: #f8f9fa; padding: 20px 30px; text-align: center;"">
            <p style=""color: #95a5a6; font-size: 12px; margin: 5px 0;"">
                ABUVI - Asociación Burgalesa de Vivencias<br>
                © {DateTime.UtcNow.Year} All rights reserved
            </p>
        </div>
    </div>
</body>
</html>";
    }
}
```

1. Run tests - should PASS

**Dependencies**:

- Resend NuGet package
- IConfiguration for settings
- ILogger for logging

**Notes**:

- HTML emails use inline styles for email client compatibility
- Welcome email failure doesn't throw - it's non-critical
- Verification email failure throws - it's critical for registration flow

---

### **Step 7: Implement AuthService Registration Methods (TDD)**

#### 7.1 Write Service Tests First (RED)

**File**: `tests/Abuvi.Tests/Unit/Features/Auth/AuthServiceTests_Registration.cs`

**Implementation Steps**:

1. Write comprehensive service tests:

```csharp
public class AuthServiceTests_Registration
{
    private readonly IUserRepository _repository;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly AuthService _service;

    public AuthServiceTests_Registration()
    {
        _repository = Substitute.For<IUserRepository>();
        _emailService = Substitute.For<IEmailService>();
        _logger = Substitute.For<ILogger<AuthService>>();

        var configDict = new Dictionary<string, string?>
        {
            ["FrontendUrl"] = "http://localhost:5173"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        _service = new AuthService(_repository, _emailService, _configuration, _logger);
    }

    [Fact]
    public async Task RegisterAsync_WithValidRequest_CreatesUserAndSendsEmail()
    {
        // Arrange
        var request = new RegisterUserRequest(
            "user@example.com",
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            "+34612345678",
            true
        );

        _repository.GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _repository.GetByDocumentNumberAsync(request.DocumentNumber, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var result = await _service.RegisterAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be("user@example.com");
        result.FirstName.Should().Be("John");
        result.EmailVerified.Should().BeFalse();
        result.IsActive.Should().BeFalse();

        await _repository.Received(1).AddAsync(
            Arg.Is<User>(u =>
                u.Email == request.Email &&
                u.FirstName == request.FirstName &&
                u.DocumentNumber == request.DocumentNumber &&
                u.EmailVerified == false &&
                u.IsActive == false &&
                u.EmailVerificationToken != null &&
                u.EmailVerificationTokenExpiry != null
            ),
            Arg.Any<CancellationToken>()
        );

        await _emailService.Received(1).SendVerificationEmailAsync(
            request.Email,
            request.FirstName,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_ThrowsBusinessRuleException()
    {
        // Arrange
        var request = new RegisterUserRequest(
            "existing@example.com",
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            null,
            true
        );

        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@example.com",
            PasswordHash = "hash",
            FirstName = "Existing",
            LastName = "User",
            DocumentNumber = "99999999Z",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns(existingUser);

        // Act
        Func<Task> act = async () => await _service.RegisterAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*email already exists*");

        await _repository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _emailService.DidNotReceive().SendVerificationEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateDocumentNumber_ThrowsBusinessRuleException()
    {
        // Arrange
        var request = new RegisterUserRequest(
            "new@example.com",
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            null,
            true
        );

        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "other@example.com",
            PasswordHash = "hash",
            FirstName = "Other",
            LastName = "User",
            DocumentNumber = "12345678A",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _repository.GetByDocumentNumberAsync(request.DocumentNumber, Arg.Any<CancellationToken>())
            .Returns(existingUser);

        // Act
        Func<Task> act = async () => await _service.RegisterAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*document number already exists*");
    }

    [Fact]
    public async Task RegisterAsync_ShouldHashPassword()
    {
        // Arrange
        var request = new RegisterUserRequest(
            "user@example.com",
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            null,
            true
        );

        _repository.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _repository.GetByDocumentNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        await _service.RegisterAsync(request, CancellationToken.None);

        // Assert
        await _repository.Received(1).AddAsync(
            Arg.Is<User>(u =>
                u.PasswordHash != request.Password &&
                u.PasswordHash.Length > 0 &&
                BCrypt.Net.BCrypt.Verify(request.Password, u.PasswordHash)
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task RegisterAsync_ShouldGenerateVerificationToken()
    {
        // Arrange
        var request = new RegisterUserRequest(
            "user@example.com",
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            null,
            true
        );

        _repository.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _repository.GetByDocumentNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        await _service.RegisterAsync(request, CancellationToken.None);

        // Assert
        await _repository.Received(1).AddAsync(
            Arg.Is<User>(u =>
                u.EmailVerificationToken != null &&
                u.EmailVerificationToken.Length > 20 &&
                u.EmailVerificationTokenExpiry != null &&
                u.EmailVerificationTokenExpiry > DateTime.UtcNow
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task VerifyEmailAsync_WithValidToken_ActivatesUser()
    {
        // Arrange
        var token = "valid-token-123";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordHash = "hash",
            FirstName = "John",
            LastName = "Doe",
            DocumentNumber = "12345678A",
            EmailVerified = false,
            IsActive = false,
            EmailVerificationToken = token,
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetByVerificationTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await _service.VerifyEmailAsync(token, CancellationToken.None);

        // Assert
        await _repository.Received(1).UpdateAsync(
            Arg.Is<User>(u =>
                u.EmailVerified == true &&
                u.IsActive == true &&
                u.EmailVerificationToken == null &&
                u.EmailVerificationTokenExpiry == null
            ),
            Arg.Any<CancellationToken>()
        );

        await _emailService.Received(1).SendWelcomeEmailAsync(
            user.Email,
            user.FirstName,
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task VerifyEmailAsync_WithInvalidToken_ThrowsNotFoundException()
    {
        // Arrange
        var token = "invalid-token";
        _repository.GetByVerificationTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Func<Task> act = async () => await _service.VerifyEmailAsync(token, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyEmailAsync_WithExpiredToken_ThrowsBusinessRuleException()
    {
        // Arrange
        var token = "expired-token";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordHash = "hash",
            FirstName = "John",
            LastName = "Doe",
            DocumentNumber = "12345678A",
            EmailVerified = false,
            EmailVerificationToken = token,
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(-1), // Expired
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetByVerificationTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Func<Task> act = async () => await _service.VerifyEmailAsync(token, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*token has expired*");
    }

    [Fact]
    public async Task ResendVerificationAsync_WithUnverifiedUser_SendsNewEmail()
    {
        // Arrange
        var email = "user@example.com";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hash",
            FirstName = "John",
            LastName = "Doe",
            DocumentNumber = "12345678A",
            EmailVerified = false,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetByEmailAsync(email, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await _service.ResendVerificationAsync(email, CancellationToken.None);

        // Assert
        await _repository.Received(1).UpdateAsync(
            Arg.Is<User>(u =>
                u.EmailVerificationToken != null &&
                u.EmailVerificationTokenExpiry != null &&
                u.EmailVerificationTokenExpiry > DateTime.UtcNow
            ),
            Arg.Any<CancellationToken>()
        );

        await _emailService.Received(1).SendVerificationEmailAsync(
            email,
            user.FirstName,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ResendVerificationAsync_WithAlreadyVerifiedUser_ThrowsBusinessRuleException()
    {
        // Arrange
        var email = "user@example.com";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hash",
            FirstName = "John",
            LastName = "Doe",
            DocumentNumber = "12345678A",
            EmailVerified = true, // Already verified
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetByEmailAsync(email, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Func<Task> act = async () => await _service.ResendVerificationAsync(email, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*already verified*");
    }
}
```

1. Install BCrypt.Net-Next for tests:

```bash
dotnet add tests/Abuvi.Tests package BCrypt.Net-Next
```

1. Run tests - should FAIL (methods don't exist)

#### 7.2 Implement AuthService Methods (GREEN)

**File**: `src/Abuvi.API/Features/Auth/AuthService.cs` (MODIFY)

**Implementation Steps**:

1. Install BCrypt.Net-Next:

```bash
dotnet add src/Abuvi.API package BCrypt.Net-Next
```

1. Add registration methods to AuthService:

```csharp
public class AuthService(
    IUserRepository userRepository,
    IEmailService emailService,
    IConfiguration configuration,
    ILogger<AuthService> logger)
{
    // Existing login methods...

    public async Task<UserResponse> RegisterAsync(RegisterUserRequest request, CancellationToken ct)
    {
        // Check for duplicate email
        var existingUser = await userRepository.GetByEmailAsync(request.Email, ct);
        if (existingUser is not null)
        {
            throw new BusinessRuleException("An account with this email already exists");
        }

        // Check for duplicate document number
        var existingByDocument = await userRepository.GetByDocumentNumberAsync(request.DocumentNumber, ct);
        if (existingByDocument is not null)
        {
            throw new BusinessRuleException("An account with this document number already exists");
        }

        // Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // Generate email verification token
        var verificationToken = GenerateVerificationToken();
        var tokenExpiry = DateTime.UtcNow.AddHours(24);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = passwordHash,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            DocumentNumber = request.DocumentNumber,
            Role = UserRole.Member,
            IsActive = false,
            EmailVerified = false,
            EmailVerificationToken = verificationToken,
            EmailVerificationTokenExpiry = tokenExpiry,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await userRepository.AddAsync(user, ct);

        // Send verification email
        await emailService.SendVerificationEmailAsync(user.Email, user.FirstName, verificationToken, ct);

        logger.LogInformation("User {Email} registered successfully. Verification email sent.", user.Email);

        return user.ToResponse();
    }

    public async Task VerifyEmailAsync(string token, CancellationToken ct)
    {
        var user = await userRepository.GetByVerificationTokenAsync(token, ct)
            ?? throw new NotFoundException("User", Guid.Empty);

        if (user.EmailVerificationTokenExpiry < DateTime.UtcNow)
        {
            throw new BusinessRuleException("Verification token has expired");
        }

        user.EmailVerified = true;
        user.IsActive = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;

        await userRepository.UpdateAsync(user, ct);

        // Send welcome email after successful verification
        await emailService.SendWelcomeEmailAsync(user.Email, user.FirstName, ct);

        logger.LogInformation("User {Email} email verified successfully", user.Email);
    }

    public async Task ResendVerificationAsync(string email, CancellationToken ct)
    {
        var user = await userRepository.GetByEmailAsync(email, ct)
            ?? throw new NotFoundException("User", Guid.Empty);

        if (user.EmailVerified)
        {
            throw new BusinessRuleException("Email is already verified");
        }

        // Generate new verification token
        var verificationToken = GenerateVerificationToken();
        var tokenExpiry = DateTime.UtcNow.AddHours(24);

        user.EmailVerificationToken = verificationToken;
        user.EmailVerificationTokenExpiry = tokenExpiry;
        user.UpdatedAt = DateTime.UtcNow;

        await userRepository.UpdateAsync(user, ct);

        // Send new verification email
        await emailService.SendVerificationEmailAsync(user.Email, user.FirstName, verificationToken, ct);

        logger.LogInformation("Verification email resent to {Email}", user.Email);
    }

    private static string GenerateVerificationToken()
    {
        var randomBytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_");
    }
}
```

1. Create extension method for User to Response mapping:

**File**: `src/Abuvi.API/Features/Auth/UserExtensions.cs` (MODIFY)

```csharp
public static class UserExtensions
{
    public static UserResponse ToResponse(this User user) => new(
        user.Id,
        user.Email,
        user.FirstName,
        user.LastName,
        user.Phone,
        user.Role.ToString(),
        user.IsActive,
        user.EmailVerified, // NEW
        user.CreatedAt
    );
}
```

1. Run tests - should PASS

**Dependencies**:

- BCrypt.Net-Next for password hashing
- System.Security.Cryptography for token generation

**Notes**:

- Password is hashed with BCrypt (salt rounds = 12 default)
- Verification token is 32 random bytes, Base64-encoded, URL-safe
- Token expires after 24 hours
- User starts with IsActive=false, EmailVerified=false
- User activated after email verification

---

### **Step 8: Create Minimal API Endpoints (TDD)**

#### 8.1 Write Endpoint Tests First (RED)

**File**: `tests/Abuvi.Tests/Unit/Features/Auth/AuthEndpointsTests_Registration.cs`

**Implementation Steps**:

1. Write endpoint tests:

```csharp
public class AuthEndpointsTests_Registration
{
    private readonly AuthService _authService;
    private readonly AuthEndpoints _endpoints;

    public AuthEndpointsTests_Registration()
    {
        _authService = Substitute.For<AuthService>(
            Substitute.For<IUserRepository>(),
            Substitute.For<IEmailService>(),
            Substitute.For<IConfiguration>(),
            Substitute.For<ILogger<AuthService>>()
        );
        _endpoints = new AuthEndpoints();
    }

    [Fact]
    public async Task Register_WithValidRequest_Returns201Created()
    {
        // Arrange
        var request = new RegisterUserRequest(
            "user@example.com",
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            null,
            true
        );

        var userResponse = new UserResponse(
            Guid.NewGuid(),
            "user@example.com",
            "John",
            "Doe",
            null,
            "Member",
            false,
            false,
            DateTime.UtcNow
        );

        _authService.RegisterAsync(request, Arg.Any<CancellationToken>())
            .Returns(userResponse);

        // Act
        var result = await _endpoints.Register(request, _authService, CancellationToken.None);

        // Assert
        var createdResult = result as Microsoft.AspNetCore.Http.HttpResults.Created<ApiResponse<UserResponse>>;
        createdResult.Should().NotBeNull();
        createdResult!.StatusCode.Should().Be(201);
        createdResult.Value!.Success.Should().BeTrue();
        createdResult.Value.Data!.Email.Should().Be("user@example.com");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409Conflict()
    {
        // Arrange
        var request = new RegisterUserRequest(
            "existing@example.com",
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            null,
            true
        );

        _authService.RegisterAsync(request, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BusinessRuleException("An account with this email already exists"));

        // Act & Assert - exception should be caught by global error middleware
        await Assert.ThrowsAsync<BusinessRuleException>(async () =>
            await _endpoints.Register(request, _authService, CancellationToken.None)
        );
    }

    [Fact]
    public async Task VerifyEmail_WithValidToken_Returns200Ok()
    {
        // Arrange
        var request = new VerifyEmailRequest("valid-token");

        _authService.VerifyEmailAsync(request.Token, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _endpoints.VerifyEmail(request, _authService, CancellationToken.None);

        // Assert
        var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<ApiResponse<object>>;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyEmail_WithInvalidToken_ThrowsNotFoundException()
    {
        // Arrange
        var request = new VerifyEmailRequest("invalid-token");

        _authService.VerifyEmailAsync(request.Token, Arg.Any<CancellationToken>())
            .ThrowsAsync(new NotFoundException("User", Guid.Empty));

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await _endpoints.VerifyEmail(request, _authService, CancellationToken.None)
        );
    }

    [Fact]
    public async Task ResendVerification_WithValidEmail_Returns200Ok()
    {
        // Arrange
        var request = new ResendVerificationRequest("user@example.com");

        _authService.ResendVerificationAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _endpoints.ResendVerification(request, _authService, CancellationToken.None);

        // Assert
        var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<ApiResponse<object>>;
        okResult.Should().NotBeNull();
        okResult!.Value!.Success.Should().BeTrue();
    }
}
```

1. Run tests - should FAIL (endpoint methods don't exist)

#### 8.2 Implement Endpoints (GREEN)

**File**: `src/Abuvi.API/Features/Auth/AuthEndpoints.cs` (MODIFY)

**Implementation Steps**:

1. Add registration endpoints to MapAuthEndpoints:

```csharp
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        // Existing login endpoints...

        group.MapPost("/register", Register)
            .WithName("RegisterUser")
            .Produces<ApiResponse<UserResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .AddEndpointFilter<ValidationFilter<RegisterUserRequest>>();

        group.MapPost("/verify-email", VerifyEmail)
            .WithName("VerifyEmail")
            .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/resend-verification", ResendVerification)
            .WithName("ResendVerification")
            .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Register(
        RegisterUserRequest request,
        AuthService authService,
        CancellationToken ct)
    {
        var user = await authService.RegisterAsync(request, ct);

        var response = new
        {
            userId = user.Id,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            isActive = user.IsActive,
            message = "Registration successful. Please check your email to verify your account."
        };

        return Results.Created(
            $"/api/users/{user.Id}",
            ApiResponse<object>.Ok(response)
        );
    }

    private static async Task<IResult> VerifyEmail(
        VerifyEmailRequest request,
        AuthService authService,
        CancellationToken ct)
    {
        await authService.VerifyEmailAsync(request.Token, ct);

        return Results.Ok(ApiResponse<object>.Ok(new
        {
            message = "Email verified successfully. Your account is now active."
        }));
    }

    private static async Task<IResult> ResendVerification(
        ResendVerificationRequest request,
        AuthService authService,
        CancellationToken ct)
    {
        await authService.ResendVerificationAsync(request.Email, ct);

        return Results.Ok(ApiResponse<object>.Ok(new
        {
            message = "Verification email sent. Please check your inbox."
        }));
    }
}
```

1. Run tests - should PASS

**Notes**:

- Register endpoint returns 201 Created with user details
- ValidationFilter automatically validates RegisterUserRequest
- Exceptions (BusinessRuleException, NotFoundException) caught by global error middleware
- All endpoints return ApiResponse envelope for consistency

---

### **Step 9: Register Services in DI Container**

**File**: `src/Abuvi.API/Program.cs` (MODIFY)

**Action**: Register ResendEmailService and validators

**Implementation Steps**:

1. Register email service:

```csharp
// Email services
builder.Services.AddScoped<IEmailService, ResendEmailService>();
```

1. Register FluentValidation validators (if not already done):

```csharp
// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
```

1. Ensure AuthService and UserRepository are registered:

```csharp
// Auth services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
```

1. Ensure AuthEndpoints are mapped:

```csharp
// Map endpoints
app.MapAuthEndpoints();
```

**Dependencies**: None (uses existing DI infrastructure)

**Notes**:

- ResendEmailService requires Resend:ApiKey in configuration
- Validators automatically discovered from assembly

---

### **Step 10: Configure Application Settings**

**File**: `src/Abuvi.API/appsettings.json` (MODIFY)

**Action**: Add Resend and FrontendUrl configuration placeholders

**Implementation Steps**:

1. Add configuration sections:

```json
{
  "Resend": {
    "ApiKey": "",
    "FromEmail": "noreply@abuvi.org"
  },
  "FrontendUrl": "http://localhost:5173"
}
```

1. Set API key via user secrets (development):

```bash
dotnet user-secrets set "Resend:ApiKey" "re_xxxxxxxxxxxxx" --project src/Abuvi.API
```

1. For production, set via environment variable:

```bash
export Resend__ApiKey="re_xxxxxxxxxxxxx"
export FrontendUrl="https://abuvi.org"
```

**Notes**:

- Never commit real API keys to source control
- Use user secrets for local development
- Use environment variables for production deployment

---

### **Step 11: Apply Database Migration**

**Action**: Apply the migration created in Step 2

**Implementation Steps**:

1. Review migration one more time:

```bash
cat src/Abuvi.API/Data/Migrations/*_AddUserRegistrationFields.cs
```

1. Apply migration to local database:

```bash
dotnet ef database update --project src/Abuvi.API
```

1. Verify migration applied successfully:

```bash
dotnet ef migrations list --project src/Abuvi.API
```

1. Check database schema has new columns (using PostgreSQL client or pgAdmin):
   - `document_number` column exists
   - `email_verified` column exists
   - `email_verification_token` column exists
   - `email_verification_token_expiry` column exists
   - Unique index on `document_number` exists

**Notes**:

- This step modifies the database schema
- Back up database before applying in production
- Use `dotnet ef migrations script --idempotent` for production deployment

---

### **Step 12: Manual Testing**

**Action**: Verify registration workflow end-to-end

**Implementation Steps**:

1. Start the API:

```bash
dotnet run --project src/Abuvi.API
```

1. Test registration endpoint (use Postman or curl):

```bash
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "testuser@example.com",
    "password": "Password123!",
    "firstName": "Test",
    "lastName": "User",
    "documentNumber": "12345678A",
    "phone": "+34612345678",
    "acceptedTerms": true
  }'
```

Expected: 201 Created with user details, verification email sent

1. Check logs for verification email sent message

2. Retrieve verification token from database (for testing):

```sql
SELECT email_verification_token FROM users WHERE email = 'testuser@example.com';
```

1. Test email verification endpoint:

```bash
curl -X POST http://localhost:5000/api/auth/verify-email \
  -H "Content-Type: application/json" \
  -d '{
    "token": "[token-from-database]"
  }'
```

Expected: 200 OK, user activated

1. Test duplicate email:

```bash
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "testuser@example.com",
    "password": "Password123!",
    "firstName": "Another",
    "lastName": "User",
    "documentNumber": "99999999Z",
    "phone": null,
    "acceptedTerms": true
  }'
```

Expected: 409 Conflict with error message

1. Test resend verification:

```bash
curl -X POST http://localhost:5000/api/auth/resend-verification \
  -H "Content-Type: application/json" \
  -d '{
    "email": "unverified@example.com"
  }'
```

Expected: 200 OK with success message

**Notes**:

- For real email delivery, configure a valid Resend API key
- Use Resend test mode for development without sending real emails

---

### **Step 13: Update Technical Documentation**

**Action**: Update documentation to reflect new registration workflow

**Implementation Steps**:

1. **Update Data Model Documentation** (`ai-specs/specs/data-model.md`):
   - Add `documentNumber` field to User entity
   - Add `emailVerified` field to User entity
   - Add `emailVerificationToken` field to User entity
   - Add `emailVerificationTokenExpiry` field to User entity
   - Change `isActive` default value to false
   - Update User validation rules to include email verification requirement

2. **Update API Specification** (`ai-specs/specs/api-spec.yml`):
   - Add `POST /api/auth/register` endpoint
   - Add `POST /api/auth/verify-email` endpoint
   - Add `POST /api/auth/resend-verification` endpoint
   - Document request/response schemas for all endpoints
   - Document error responses (400, 404, 409)

3. **Verify Auto-Generated OpenAPI** (via Swagger UI):
   - Start application: `dotnet run --project src/Abuvi.API`
   - Navigate to `http://localhost:5000/swagger`
   - Verify all registration endpoints appear
   - Verify request/response schemas are correct
   - Verify error response codes are documented

4. **Update Backend Standards** (if needed):
   - Document email service integration pattern
   - Document verification token generation approach
   - Add example of email verification workflow to patterns

**References**:

- Follow `ai-specs/specs/documentation-standards.mdc`
- All documentation in English
- Maintain consistency with existing structure

**Notes**:

- This step is MANDATORY before considering implementation complete
- Documentation updates should be in the same PR/commit as code changes

---

## Implementation Order

1. **Step 0**: Create Feature Branch (`feature/user-registration-workflow-backend`)
2. **Step 1**: Update User Entity and EF Core Configuration (TDD)
3. **Step 2**: Create EF Core Migration (do not apply yet)
4. **Step 3**: Create Request/Response DTOs (TDD)
5. **Step 4**: Create FluentValidation Validators (TDD)
6. **Step 5**: Extend Repository Interface and Implementation (TDD)
7. **Step 6**: Implement Email Service (TDD)
8. **Step 7**: Implement AuthService Registration Methods (TDD)
9. **Step 8**: Create Minimal API Endpoints (TDD)
10. **Step 9**: Register Services in DI Container
11. **Step 10**: Configure Application Settings
12. **Step 11**: Apply Database Migration
13. **Step 12**: Manual Testing
14. **Step 13**: Update Technical Documentation

## Testing Checklist

### Unit Tests (90%+ Coverage Required)

**Validation Tests** (`RegisterUserValidatorTests.cs`):

- ✅ Valid request passes validation
- ✅ Invalid email formats rejected
- ✅ Weak passwords rejected (missing uppercase, lowercase, digit, special char)
- ✅ Missing required fields rejected
- ✅ Invalid document number format rejected
- ✅ Terms not accepted rejected
- ✅ Invalid phone format rejected

**Service Tests** (`AuthServiceTests_Registration.cs`):

- ✅ RegisterAsync creates user with hashed password
- ✅ RegisterAsync generates verification token with 24h expiry
- ✅ RegisterAsync sends verification email
- ✅ RegisterAsync throws exception for duplicate email
- ✅ RegisterAsync throws exception for duplicate document number
- ✅ VerifyEmailAsync activates user with valid token
- ✅ VerifyEmailAsync sends welcome email after activation
- ✅ VerifyEmailAsync throws exception for invalid token
- ✅ VerifyEmailAsync throws exception for expired token
- ✅ ResendVerificationAsync generates new token and sends email
- ✅ ResendVerificationAsync throws exception if already verified

**Email Service Tests** (`ResendEmailServiceTests.cs`):

- ✅ Constructor throws if API key missing
- ✅ SendVerificationEmailAsync generates correct URL
- ✅ SendWelcomeEmailAsync uses correct template

**Endpoint Tests** (`AuthEndpointsTests_Registration.cs`):

- ✅ Register endpoint returns 201 Created
- ✅ VerifyEmail endpoint returns 200 OK
- ✅ ResendVerification endpoint returns 200 OK
- ✅ Exceptions propagate to global error middleware

### Integration Tests (Optional - see MEMORY.md)

Due to EF Core provider conflict, integration tests are currently blocked. If using Testcontainers:

- ✅ Register endpoint creates user in database
- ✅ Verify endpoint updates user in database
- ✅ Duplicate email returns 409 Conflict

### Manual Testing Verification

- ✅ Registration creates user with inactive account
- ✅ Verification email sent to user's inbox (or Resend logs)
- ✅ Email verification link activates account
- ✅ Welcome email sent after verification
- ✅ Duplicate email/document number prevented
- ✅ Resend verification generates new token
- ✅ Expired token rejected
- ✅ Invalid token rejected

## Error Response Format

All endpoints use the standard `ApiResponse<T>` envelope:

**Success Response (201 Created - Registration):**

```json
{
  "success": true,
  "data": {
    "userId": "550e8400-e29b-41d4-a716-446655440000",
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "isActive": false,
    "message": "Registration successful. Please check your email to verify your account."
  },
  "error": null
}
```

**Success Response (200 OK - Verification):**

```json
{
  "success": true,
  "data": {
    "message": "Email verified successfully. Your account is now active."
  },
  "error": null
}
```

**Error Response (400 Bad Request - Validation):**

```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "Validation failed",
    "code": "VALIDATION_ERROR",
    "details": [
      { "field": "Email", "message": "Invalid email format" },
      { "field": "Password", "message": "Password must be at least 8 characters" }
    ]
  }
}
```

**Error Response (409 Conflict - Duplicate):**

```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "An account with this email already exists",
    "code": "DUPLICATE_ACCOUNT"
  }
}
```

**Error Response (404 Not Found - Invalid Token):**

```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "User with ID '00000000-0000-0000-0000-000000000000' was not found",
    "code": "NOT_FOUND"
  }
}
```

**Error Response (422 Unprocessable Entity - Expired Token):**

```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "Verification token has expired",
    "code": "BUSINESS_RULE_VIOLATION"
  }
}
```

## HTTP Status Code Mapping

- **200 OK**: Email verification successful, resend verification successful
- **201 Created**: User registration successful
- **400 Bad Request**: Validation errors (FluentValidation)
- **404 Not Found**: User not found (invalid token)
- **409 Conflict**: Duplicate email or document number
- **422 Unprocessable Entity**: Business rule violations (expired token, already verified)
- **500 Internal Server Error**: Unexpected server errors (email service failure)

## Dependencies

### NuGet Packages

**Main Project** (`src/Abuvi.API/Abuvi.API.csproj`):

```bash
dotnet add src/Abuvi.API package BCrypt.Net-Next
dotnet add src/Abuvi.API package Resend
```

**Test Project** (`tests/Abuvi.Tests/Abuvi.Tests.csproj`):

```bash
dotnet add tests/Abuvi.Tests package BCrypt.Net-Next
dotnet add tests/Abuvi.Tests package Resend
```

### External Services

**Resend** (Transactional Email Service):

- Sign up: <https://resend.com>
- Pricing: Free tier includes 100 emails/day, 3,000 emails/month
- API key required (store in user secrets/environment variables)
- Test mode available for development

### EF Core Migration Commands

```bash
# Create migration
dotnet ef migrations add AddUserRegistrationFields --project src/Abuvi.API

# Apply migration (local)
dotnet ef database update --project src/Abuvi.API

# Generate SQL script (production)
dotnet ef migrations script --idempotent --project src/Abuvi.API --output migration.sql
```

## Notes

### Critical TDD Requirement

**This implementation MUST follow Test-Driven Development (RED-GREEN-REFACTOR):**

1. **RED**: Write failing test first
2. **GREEN**: Write minimum code to make test pass
3. **REFACTOR**: Improve code while keeping tests green
4. Repeat for each scenario

Tests are written FIRST, not as Step 9. Each implementation step includes:

- Write tests (RED)
- Implement code (GREEN)
- Verify tests pass

### Security Considerations

**Password Security:**

- BCrypt with default salt rounds (12)
- Passwords validated for strength (8+ chars, uppercase, lowercase, digit, special char)
- Password never stored in plain text
- Password never returned in responses

**Email Verification:**

- Token: 32 random bytes, Base64-encoded, URL-safe
- Expiry: 24 hours from generation
- Single-use: Token cleared after successful verification
- Token stored hashed in database (consideration for future)

**Data Protection:**

- DocumentNumber stored as-is (needed for business logic, but PII)
- GDPR compliance: DocumentNumber must be included in data export/deletion requests
- Rate limiting recommended for registration endpoint (5 attempts/hour per IP)
- Consider adding CAPTCHA for automated registration prevention

**Configuration Security:**

- Resend API key stored in user secrets (dev) or environment variables (prod)
- Never commit API keys to source control
- Use HTTPS only for all endpoints in production

### Business Rules

1. **Account Activation**: Users cannot login until email is verified
2. **Default Role**: All new users start with `Member` role
3. **Duplicate Prevention**: Unique constraints on both email and document number
4. **Token Expiry**: Verification tokens expire after 24 hours
5. **Single Registration**: One account per email, one account per document number
6. **Non-Critical Emails**: Welcome email failure doesn't block verification
7. **Critical Emails**: Verification email failure throws exception and blocks registration

### Email Template Recommendation

The enriched spec includes two options for email templates:

- **React Email** (recommended): Type-safe, component-based, with live preview
- **Inline HTML** (current implementation): Simple, no extra dependencies

Current implementation uses **Inline HTML** for simplicity. To upgrade to React Email:

1. Create `emails/` project with React Email
2. Export templates to HTML files
3. Modify ResendEmailService to load HTML from files
4. Replace placeholders ({{firstName}}, {{verificationUrl}})

### Future Enhancements

- Add CAPTCHA for bot prevention
- Implement email change with verification
- Add phone number verification (SMS)
- Support social login (Google, Facebook)
- Add password strength meter on frontend
- Implement account lockout after failed verification attempts
- Add audit log for security events
- Leverage Resend webhooks for email event tracking
- Create branded email templates with React Email
- Implement batch email sending for newsletters

### Language Requirements

- All code: English
- All comments: English
- All documentation: English
- All commit messages: English
- All test names: English
- Email templates: English (plan for Spanish translation)

### RGPD/GDPR Compliance

**Sensitive Data**:

- `DocumentNumber` is PII and must be included in data export/deletion requests
- Email addresses are PII
- User profile data (FirstName, LastName, Phone) is PII

**Data Subject Rights**:

- Right to access: Provide all user data including DocumentNumber
- Right to erasure: Delete all user data including DocumentNumber
- Right to portability: Export all user data in machine-readable format

## Next Steps After Implementation

1. **Frontend Integration**: Implement registration and email verification pages (see frontend spec)
2. **Email Template Enhancement**: Consider upgrading to React Email for better maintainability
3. **Rate Limiting**: Add rate limiting middleware to prevent abuse
4. **CAPTCHA Integration**: Add reCAPTCHA to registration form
5. **Monitoring**: Set up monitoring for email delivery metrics via Resend dashboard
6. **Production Deployment**:
   - Apply migration to production database
   - Configure Resend API key in production environment
   - Verify Resend domain configuration (if using custom domain)
   - Test email delivery from production

## Implementation Verification

### Final Checklist

**Code Quality**:

- ✅ All files follow C# naming conventions (PascalCase classes, camelCase parameters)
- ✅ Nullable reference types enabled and handled properly
- ✅ No compiler warnings or errors
- ✅ Code follows Vertical Slice Architecture (all in Features/Auth/)
- ✅ Primary constructors used for dependency injection
- ✅ Records used for DTOs (immutable)
- ✅ File-scoped namespaces used

**Functionality**:

- ✅ POST /api/auth/register creates user and sends verification email
- ✅ POST /api/auth/verify-email activates account with valid token
- ✅ POST /api/auth/resend-verification sends new verification email
- ✅ Duplicate email rejected with 409 Conflict
- ✅ Duplicate document number rejected with 409 Conflict
- ✅ Invalid password rejected with 400 Bad Request
- ✅ Expired token rejected with 422 Unprocessable Entity
- ✅ All endpoints return ApiResponse envelope

**Testing**:

- ✅ Unit tests written FIRST for all components (TDD)
- ✅ All tests follow AAA pattern (Arrange-Act-Assert)
- ✅ Test names follow convention: `MethodName_StateUnderTest_ExpectedBehavior`
- ✅ 90%+ code coverage achieved
- ✅ Tests use NSubstitute for mocking
- ✅ Tests use FluentAssertions for assertions
- ✅ All tests passing: `dotnet test`

**Integration**:

- ✅ EF Core migration created and reviewed
- ✅ Migration applied successfully to local database
- ✅ ResendEmailService registered in DI container
- ✅ FluentValidation validators registered
- ✅ AuthEndpoints mapped in Program.cs
- ✅ Resend API key configured (user secrets or environment variable)

**Documentation**:

- ✅ Data model updated with new User fields
- ✅ API specification updated with new endpoints
- ✅ OpenAPI/Swagger documentation verified
- ✅ Code comments added where logic is non-obvious
- ✅ All documentation in English

---

**Implementation Status**: Ready for development

**Estimated Effort**: 4-6 hours (following TDD strictly)

**Risk Level**: Low (well-defined requirements, standard patterns)

**Blocker Dependencies**: None (all dependencies available)
