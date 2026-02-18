# Backend Implementation Plan: feat-forgot-password — Password Reset Flow

## Overview

Implements a two-step password reset flow:

1. **Forgot Password** — User requests a reset link; backend sends an email with a one-time token. Always returns 200 to prevent user enumeration.
2. **Reset Password** — User submits the token from the email along with a new password; backend validates the token, updates the password hash, and clears the token.

The feature lives entirely within the existing **Auth** vertical slice (`src/Abuvi.API/Features/Auth/`) and touches the **Users** slice only to extend the entity and repository.

Architecture: Vertical Slice Architecture, Minimal APIs, EF Core + PostgreSQL, FluentValidation, BCrypt, NSubstitute + xUnit tests.

---

## Architecture Context

### Feature slice: `src/Abuvi.API/Features/Auth/`

| File | Action |
|---|---|
| `AuthModels.cs` | Add `ForgotPasswordRequest` + `ResetPasswordRequest` records |
| `IAuthService.cs` | Add `ForgotPasswordAsync` + `ResetPasswordAsync` signatures |
| `AuthService.cs` | Implement both methods |
| `AuthEndpoints.cs` | Register two new endpoints |
| `ForgotPasswordRequestValidator.cs` | **New file** |
| `ResetPasswordRequestValidator.cs` | **New file** |

### Users slice (cross-slice touch):

| File | Action |
|---|---|
| `Features/Users/UsersModels.cs` | Add 2 new nullable fields to `User` entity |
| `Data/Configurations/UserConfiguration.cs` | Configure the 2 new columns |
| `Features/Users/IUsersRepository.cs` | Add `GetByPasswordResetTokenAsync` |
| `Features/Users/UsersRepository.cs` | Implement `GetByPasswordResetTokenAsync` |

### Other:

| File | Action |
|---|---|
| EF Core migration | `AddPasswordResetTokenToUsers` |
| `ai-specs/specs/data-model.md` | Add 2 new fields to User table |
| `ai-specs/specs/api-endpoints.md` | Document 2 new endpoints |

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to feature branch before any code changes.
- **Branch name**: `feature/feat-forgot-password-backend`
- **Steps**:
  1. `git checkout main`
  2. `git pull origin main`
  3. `git checkout -b feature/feat-forgot-password-backend`
  4. `git branch` — verify you are on the new branch

---

### Step 1: Extend User Entity with Password Reset Fields

**File**: `src/Abuvi.API/Features/Users/UsersModels.cs`

Add two nullable properties to the `User` class, after the existing `EmailVerificationTokenExpiry` field:

```csharp
public string? PasswordResetToken { get; set; }
public DateTime? PasswordResetTokenExpiry { get; set; }
```

**Notes**:
- These are intentionally separate from `EmailVerificationToken` — different lifetime (1h vs 24h), different purpose, different query path.
- Both are nullable because the fields are only populated during an active reset request.

---

### Step 2: Configure New Columns in EF Core

**File**: `src/Abuvi.API/Data/Configurations/UserConfiguration.cs`

Add inside `Configure()`, after the `EmailVerificationTokenExpiry` configuration block:

```csharp
builder.Property(u => u.PasswordResetToken)
    .HasMaxLength(512)
    .HasColumnName("password_reset_token");

builder.Property(u => u.PasswordResetTokenExpiry)
    .HasColumnName("password_reset_token_expiry");
```

**Notes**:
- `HasMaxLength(512)` matches the existing `EmailVerificationToken` column pattern.
- No default value — the column is naturally `NULL` at rest.

---

### Step 3: Add Repository Method

**File**: `src/Abuvi.API/Features/Users/IUsersRepository.cs`

Add after the existing `GetByVerificationTokenAsync` signature:

```csharp
Task<User?> GetByPasswordResetTokenAsync(string token, CancellationToken cancellationToken = default);
```

**File**: `src/Abuvi.API/Features/Users/UsersRepository.cs`

Implement:

```csharp
public async Task<User?> GetByPasswordResetTokenAsync(string token, CancellationToken cancellationToken = default)
    => await _context.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == token, cancellationToken);
```

**Notes**:
- Follows the same pattern as the existing `GetByVerificationTokenAsync`.
- No `.AsNoTracking()` — the caller (`AuthService`) will update the entity immediately after finding it, so tracking is required.

---

### Step 4: Add Request/Response Models

**File**: `src/Abuvi.API/Features/Auth/AuthModels.cs`

Add at the end of the file:

```csharp
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string NewPassword);
```

**Notes**:
- Records are immutable and follow the existing DTO pattern in the slice.
- No response DTO needed — both endpoints return `ApiResponse<object>` with a message string.

---

### Step 5: Create FluentValidation Validators

#### 5a. ForgotPassword Validator

**File**: `src/Abuvi.API/Features/Auth/ForgotPasswordRequestValidator.cs` (**new file**)

```csharp
namespace Abuvi.API.Features.Auth;

using FluentValidation;

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio")
            .EmailAddress().WithMessage("Formato de correo electrónico inválido")
            .MaximumLength(255);
    }
}
```

#### 5b. ResetPassword Validator

**File**: `src/Abuvi.API/Features/Auth/ResetPasswordRequestValidator.cs` (**new file**)

```csharp
namespace Abuvi.API.Features.Auth;

using FluentValidation;

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("El token de recuperación es obligatorio");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("La nueva contraseña es obligatoria")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres")
            .Matches(@"[A-Z]").WithMessage("La contraseña debe contener al menos una letra mayúscula")
            .Matches(@"[a-z]").WithMessage("La contraseña debe contener al menos una letra minúscula")
            .Matches(@"\d").WithMessage("La contraseña debe contener al menos un dígito")
            .Matches(@"[@$!%*?&#]").WithMessage("La contraseña debe contener al menos un carácter especial");
    }
}
```

**Notes**:
- Password rules match `RegisterUserValidator.cs` exactly — consistency is key.

---

### Step 6: Extend IAuthService Interface

**File**: `src/Abuvi.API/Features/Auth/IAuthService.cs`

Add two new method signatures:

```csharp
Task ForgotPasswordAsync(string email, CancellationToken ct);
Task ResetPasswordAsync(string token, string newPassword, CancellationToken ct);
```

---

### Step 7: Implement Service Methods

**File**: `src/Abuvi.API/Features/Auth/AuthService.cs`

Add the two methods. The private `GenerateVerificationToken()` method already exists in this class and must be reused (do not duplicate).

#### ForgotPasswordAsync

```csharp
/// <summary>
/// Initiates password reset flow — always succeeds to prevent user enumeration.
/// If user exists and is active, saves a reset token and sends reset email.
/// </summary>
public virtual async Task ForgotPasswordAsync(string email, CancellationToken ct)
{
    var user = await _usersRepository.GetByEmailAsync(email, ct);

    if (user is null || !user.IsActive)
    {
        _logger.LogInformation(
            "Password reset requested for {Email} — user not found or inactive, no action taken",
            email);
        return; // Intentional: never reveal whether user exists
    }

    var resetToken = GenerateVerificationToken(); // reuses existing private method
    var tokenExpiry = DateTime.UtcNow.AddHours(1);

    user.PasswordResetToken = resetToken;
    user.PasswordResetTokenExpiry = tokenExpiry;
    user.UpdatedAt = DateTime.UtcNow;

    await _usersRepository.UpdateAsync(user, ct);
    await _emailService.SendPasswordResetEmailAsync(user.Email, user.FirstName, resetToken, ct);

    _logger.LogInformation("Password reset email sent to {Email}", email);
}
```

#### ResetPasswordAsync

```csharp
/// <summary>
/// Completes password reset — validates token, updates password hash, invalidates token.
/// </summary>
public virtual async Task ResetPasswordAsync(string token, string newPassword, CancellationToken ct)
{
    var user = await _usersRepository.GetByPasswordResetTokenAsync(token, ct)
        ?? throw new BusinessRuleException("El enlace de recuperación es inválido o ha expirado");

    if (user.PasswordResetTokenExpiry < DateTime.UtcNow)
        throw new BusinessRuleException("El enlace de recuperación es inválido o ha expirado");

    user.PasswordHash = _passwordHasher.HashPassword(newPassword);
    user.PasswordResetToken = null;       // one-time use — clear immediately
    user.PasswordResetTokenExpiry = null;
    user.UpdatedAt = DateTime.UtcNow;

    await _usersRepository.UpdateAsync(user, ct);

    _logger.LogInformation("Password reset completed for user {UserId}", user.Id);
}
```

**Notes**:
- Both methods are `virtual` — required for NSubstitute mocking in unit tests (existing pattern in this class).
- Both use `BusinessRuleException` — follow the existing exception handling pattern.
- Use the **same error message** for token-not-found and token-expired — never reveal which case applies (security).

---

### Step 8: Register New Endpoints

**File**: `src/Abuvi.API/Features/Auth/AuthEndpoints.cs`

Inside `MapAuthEndpoints()`, add after the existing `resend-verification` registration:

```csharp
group.MapPost("/forgot-password", ForgotPassword)
    .AddEndpointFilter<ValidationFilter<ForgotPasswordRequest>>()
    .WithName("ForgotPassword")
    .Produces<ApiResponse<object>>()
    .Produces(StatusCodes.Status400BadRequest);

group.MapPost("/reset-password", ResetPassword)
    .AddEndpointFilter<ValidationFilter<ResetPasswordRequest>>()
    .WithName("ResetPassword")
    .Produces<ApiResponse<object>>()
    .Produces(StatusCodes.Status400BadRequest);
```

Add the two handler methods as `private static`:

```csharp
/// <summary>
/// Initiates password reset — always returns 200 regardless of whether email exists.
/// </summary>
private static async Task<IResult> ForgotPassword(
    [FromBody] ForgotPasswordRequest request,
    IAuthService authService,
    CancellationToken cancellationToken)
{
    await authService.ForgotPasswordAsync(request.Email, cancellationToken);
    return Results.Ok(ApiResponse<object>.Ok(
        new { message = "Si tu correo está registrado, recibirás un enlace para restablecer tu contraseña." }
    ));
}

/// <summary>
/// Resets user password using a valid one-time token.
/// </summary>
private static async Task<IResult> ResetPassword(
    [FromBody] ResetPasswordRequest request,
    IAuthService authService,
    CancellationToken cancellationToken)
{
    try
    {
        await authService.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken);
        return Results.Ok(ApiResponse<object>.Ok(
            new { message = "Contraseña restablecida exitosamente." }
        ));
    }
    catch (BusinessRuleException ex)
    {
        return Results.Json(
            ApiResponse<object>.Fail(ex.Message, "INVALID_OR_EXPIRED_TOKEN"),
            statusCode: 400
        );
    }
}
```

**Notes**:
- `ForgotPassword` never throws — `ForgotPasswordAsync` intentionally swallows user-not-found.
- `ResetPassword` catches only `BusinessRuleException`; unexpected errors propagate to global error handler.
- No DI registration needed in `Program.cs` — `IAuthService` + `IUsersRepository` are already registered.

---

### Step 9: Create EF Core Migration

Run from the `src/Abuvi.API/` directory:

```bash
dotnet ef migrations add AddPasswordResetTokenToUsers --project src/Abuvi.API/Abuvi.API.csproj
dotnet ef database update --project src/Abuvi.API/Abuvi.API.csproj
```

**Expected migration**: Adds two nullable columns to the `users` table:
- `password_reset_token` — `varchar(512)`, nullable
- `password_reset_token_expiry` — `timestamp with time zone`, nullable

**Verify** the generated `Up()` and `Down()` methods look correct before running `database update`.

---

### Step 10: Write Unit Tests (TDD — write BEFORE implementing Steps 7–8)

> **TDD Reminder**: Steps 10 must be written BEFORE Steps 7 and 8. Write a failing test, then make it pass. Follow Red-Green-Refactor.

**Test project location**: `src/Abuvi.Tests/Unit/Features/Auth/`

**File 1**: `src/Abuvi.Tests/Unit/Features/Auth/AuthServiceTests_ForgotPassword.cs`

```csharp
namespace Abuvi.Tests.Unit.Features.Auth;

using Abuvi.API.Common.Services;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

public class AuthServiceTests_ForgotPassword
{
    private readonly IUsersRepository _repository;
    private readonly IEmailService _emailService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ILogger<AuthService> _logger;
    private readonly AuthService _service;

    public AuthServiceTests_ForgotPassword()
    {
        _repository = Substitute.For<IUsersRepository>();
        _emailService = Substitute.For<IEmailService>();
        _passwordHasher = Substitute.For<IPasswordHasher>();

        var jwtConfig = Substitute.For<IConfiguration>();
        _jwtTokenService = Substitute.For<JwtTokenService>(jwtConfig);
        _logger = Substitute.For<ILogger<AuthService>>();

        var configDict = new Dictionary<string, string?>
        {
            ["FrontendUrl"] = "http://localhost:5173"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        _service = new AuthService(
            _repository,
            _passwordHasher,
            _jwtTokenService,
            _emailService,
            configuration,
            _logger);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserNotFound_DoesNotSendEmailAndDoesNotThrow()
    {
        // Arrange
        _repository.GetByEmailAsync("notfound@example.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var act = () => _service.ForgotPasswordAsync("notfound@example.com", CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        await _emailService.DidNotReceive().SendPasswordResetEmailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserInactive_DoesNotSendEmail()
    {
        // Arrange
        var inactiveUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "inactive@example.com",
            FirstName = "Jane",
            IsActive = false
        };

        _repository.GetByEmailAsync("inactive@example.com", Arg.Any<CancellationToken>())
            .Returns(inactiveUser);

        // Act
        await _service.ForgotPasswordAsync("inactive@example.com", CancellationToken.None);

        // Assert
        await _emailService.DidNotReceive().SendPasswordResetEmailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserExists_SavesResetTokenWithOneHourExpiry()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "John",
            IsActive = true
        };

        _repository.GetByEmailAsync("user@example.com", Arg.Any<CancellationToken>())
            .Returns(user);
        _repository.UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(x => x.Arg<User>());

        var before = DateTime.UtcNow;

        // Act
        await _service.ForgotPasswordAsync("user@example.com", CancellationToken.None);

        // Assert
        await _repository.Received(1).UpdateAsync(
            Arg.Is<User>(u =>
                u.PasswordResetToken != null &&
                u.PasswordResetToken.Length > 0 &&
                u.PasswordResetTokenExpiry >= before.AddHours(1).AddSeconds(-5) &&
                u.PasswordResetTokenExpiry <= before.AddHours(1).AddSeconds(5)
            ),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserExists_SendsPasswordResetEmail()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "John",
            IsActive = true
        };

        _repository.GetByEmailAsync("user@example.com", Arg.Any<CancellationToken>())
            .Returns(user);
        _repository.UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(x => x.Arg<User>());

        // Act
        await _service.ForgotPasswordAsync("user@example.com", CancellationToken.None);

        // Assert
        await _emailService.Received(1).SendPasswordResetEmailAsync(
            "user@example.com",
            "John",
            Arg.Is<string>(t => t.Length > 0),
            Arg.Any<CancellationToken>());
    }
}
```

**File 2**: `src/Abuvi.Tests/Unit/Features/Auth/AuthServiceTests_ResetPassword.cs`

```csharp
namespace Abuvi.Tests.Unit.Features.Auth;

using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Services;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

public class AuthServiceTests_ResetPassword
{
    private readonly IUsersRepository _repository;
    private readonly IEmailService _emailService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ILogger<AuthService> _logger;
    private readonly AuthService _service;

    public AuthServiceTests_ResetPassword()
    {
        _repository = Substitute.For<IUsersRepository>();
        _emailService = Substitute.For<IEmailService>();
        _passwordHasher = Substitute.For<IPasswordHasher>();

        var jwtConfig = Substitute.For<IConfiguration>();
        _jwtTokenService = Substitute.For<JwtTokenService>(jwtConfig);
        _logger = Substitute.For<ILogger<AuthService>>();

        var configDict = new Dictionary<string, string?>
        {
            ["FrontendUrl"] = "http://localhost:5173"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        _service = new AuthService(
            _repository,
            _passwordHasher,
            _jwtTokenService,
            _emailService,
            configuration,
            _logger);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenNotFound_ThrowsBusinessRuleException()
    {
        // Arrange
        _repository.GetByPasswordResetTokenAsync("invalid-token", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var act = () => _service.ResetPasswordAsync("invalid-token", "NewPassword1!", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("El enlace de recuperación es inválido o ha expirado");
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenExpired_ThrowsBusinessRuleException()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordResetToken = "expired-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(-1) // expired 1 hour ago
        };

        _repository.GetByPasswordResetTokenAsync("expired-token", Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var act = () => _service.ResetPasswordAsync("expired-token", "NewPassword1!", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("El enlace de recuperación es inválido o ha expirado");
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenValid_UpdatesPasswordHash()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordHash = "old_hash",
            PasswordResetToken = "valid-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(30)
        };

        _repository.GetByPasswordResetTokenAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(user);
        _repository.UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(x => x.Arg<User>());
        _passwordHasher.HashPassword("NewPassword1!").Returns("new_hashed_password");

        // Act
        await _service.ResetPasswordAsync("valid-token", "NewPassword1!", CancellationToken.None);

        // Assert
        await _repository.Received(1).UpdateAsync(
            Arg.Is<User>(u => u.PasswordHash == "new_hashed_password"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenValid_ClearsResetToken()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordResetToken = "valid-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(30)
        };

        _repository.GetByPasswordResetTokenAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(user);
        _repository.UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(x => x.Arg<User>());
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("hashed");

        // Act
        await _service.ResetPasswordAsync("valid-token", "NewPassword1!", CancellationToken.None);

        // Assert
        await _repository.Received(1).UpdateAsync(
            Arg.Is<User>(u =>
                u.PasswordResetToken == null &&
                u.PasswordResetTokenExpiry == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenValid_DoesNotSendAnyEmail()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordResetToken = "valid-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(30)
        };

        _repository.GetByPasswordResetTokenAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(user);
        _repository.UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(x => x.Arg<User>());
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("hashed");

        // Act
        await _service.ResetPasswordAsync("valid-token", "NewPassword1!", CancellationToken.None);

        // Assert — no email is sent on successful reset (consistent with other reset flows)
        await _emailService.DidNotReceiveWithAnyArgs().SendPasswordResetEmailAsync(
            default!, default!, default!, default);
    }
}
```

**Notes on tests**:
- Use `NSubstitute` (not Moq). All mocks via `Substitute.For<T>()`.
- Follow **AAA pattern** with `// Arrange`, `// Act`, `// Assert` comments.
- Test name format: `MethodName_WhenCondition_ExpectedBehavior`.
- `_repository.UpdateAsync(...).Returns(x => x.Arg<User>())` — returns the passed entity to simulate EF Core's `UpdateAsync` behavior.
- **Write these tests FIRST** (TDD Red phase), then implement Steps 7–8.

---

### Step 11: Update Technical Documentation

**Action**: Update the two documentation files affected by this feature.

#### 11a. `ai-specs/specs/data-model.md` — Add new User fields

In the User entity table, add after `emailVerificationTokenExpiry`:

| Field | Type | Required | Notes |
|---|---|---|---|
| `passwordResetToken` | string? | No | URL-safe base64, max 512 chars, expires 1h |
| `passwordResetTokenExpiry` | datetime? | No | 1 hour from request creation |

#### 11b. `ai-specs/specs/api-endpoints.md` — Document new endpoints

Add to the Authentication Endpoints section:

---

### POST /api/auth/forgot-password

Initiates a password reset. Always returns 200 to prevent user enumeration.

**Request Body:**
```json
{ "email": "user@example.com" }
```

**Validation:** email required, valid format.

**Success Response (200 OK):**
```json
{
  "success": true,
  "data": { "message": "Si tu correo está registrado, recibirás un enlace para restablecer tu contraseña." }
}
```

---

### POST /api/auth/reset-password

Completes password reset using a one-time token.

**Request Body:**
```json
{ "token": "...", "newPassword": "NewPassword1!" }
```

**Validation:** token required; newPassword min 8 chars, uppercase, lowercase, digit, special char.

**Success Response (200 OK):**
```json
{
  "success": true,
  "data": { "message": "Contraseña restablecida exitosamente." }
}
```

**Error Response (400 Bad Request):**
```json
{
  "success": false,
  "error": { "message": "El enlace de recuperación es inválido o ha expirado", "code": "INVALID_OR_EXPIRED_TOKEN" }
}
```

---

## Implementation Order

1. **Step 0** — Create feature branch
2. **Step 10** — Write failing unit tests (TDD Red phase)
3. **Step 1** — Extend User entity with new fields
4. **Step 2** — Configure EF Core columns
5. **Step 3** — Add repository method
6. **Step 4** — Add request models
7. **Step 5** — Create validators
8. **Step 6** — Extend IAuthService interface
9. **Step 7** — Implement service methods (tests should now pass — Green phase)
10. **Step 8** — Register endpoints
11. **Step 9** — Create and apply EF Core migration
12. **Step 10 (verify)** — Run tests, confirm all pass
13. **Step 11** — Update documentation

---

## Testing Checklist

- [ ] `ForgotPasswordAsync_WhenUserNotFound_DoesNotSendEmailAndDoesNotThrow` — passes
- [ ] `ForgotPasswordAsync_WhenUserInactive_DoesNotSendEmail` — passes
- [ ] `ForgotPasswordAsync_WhenUserExists_SavesResetTokenWithOneHourExpiry` — passes
- [ ] `ForgotPasswordAsync_WhenUserExists_SendsPasswordResetEmail` — passes
- [ ] `ResetPasswordAsync_WhenTokenNotFound_ThrowsBusinessRuleException` — passes
- [ ] `ResetPasswordAsync_WhenTokenExpired_ThrowsBusinessRuleException` — passes
- [ ] `ResetPasswordAsync_WhenTokenValid_UpdatesPasswordHash` — passes
- [ ] `ResetPasswordAsync_WhenTokenValid_ClearsResetToken` — passes
- [ ] `ResetPasswordAsync_WhenTokenValid_DoesNotSendAnyEmail` — passes
- [ ] `dotnet test` — all existing tests still pass (no regression)
- [ ] Build succeeds with no warnings: `dotnet build`

---

## Error Response Format

All errors follow the `ApiResponse<T>` envelope:

```json
{
  "success": false,
  "error": {
    "message": "Human-readable message in Spanish",
    "code": "SCREAMING_SNAKE_CASE_CODE"
  }
}
```

| Scenario | HTTP Status | Error Code |
|---|---|---|
| Validation failure (empty email, bad format) | 400 | _(FluentValidation details)_ |
| Token not found or expired | 400 | `INVALID_OR_EXPIRED_TOKEN` |
| User not found (forgot-password) | 200 | _(no error — intentional)_ |

---

## Dependencies

### No new NuGet packages required

All dependencies already present:
- `BCrypt.Net-Next` — already used in `PasswordHasher.cs`
- `FluentValidation.AspNetCore` — already registered
- `Microsoft.EntityFrameworkCore` — already used
- `Resend` — already used in `ResendEmailService.cs`

### EF Core Migration Commands

```bash
# Run from repository root
dotnet ef migrations add AddPasswordResetTokenToUsers --project src/Abuvi.API/Abuvi.API.csproj
dotnet ef database update --project src/Abuvi.API/Abuvi.API.csproj
```

---

## Notes

- **User enumeration prevention**: `ForgotPasswordAsync` MUST return without error even if user doesn't exist. This is a hard security requirement. The endpoint always returns HTTP 200.
- **Same error message for both invalid and expired tokens**: Never reveal which case applies. Both map to `"El enlace de recuperación es inválido o ha expirado"`.
- **Token lifetime**: 1 hour (not 24 hours — password reset is more sensitive than email verification).
- **Token reuse prevention**: Token is cleared in `ResetPasswordAsync` before returning. The `UpdateAsync` call persists this to the DB.
- **Separate fields**: `PasswordResetToken` is NOT the same as `EmailVerificationToken`. Do not share fields — they have different lifetimes and different consumers.
- **Language**: All user-facing strings and log messages are in **Spanish** for user-facing content; English for internal/developer log messages.
- **`virtual` on service methods**: Required so NSubstitute can substitute `AuthService` in tests (existing pattern in the codebase — see `RegisterUserAsync`, `VerifyEmailAsync`, `ResendVerificationAsync`).
- **No `Program.cs` changes needed**: `IAuthService` and `IUsersRepository` are already registered via DI.

---

## Next Steps After Implementation

1. Trigger frontend ticket: `feature/feat-forgot-password-frontend`
2. Test end-to-end with Resend sandbox: verify email arrives, link opens correct frontend URL, token works once, second use returns error.
3. Consider adding rate limiting to `POST /api/auth/forgot-password` in a follow-up (e.g., 3 requests per email per hour via `AspNetCoreRateLimit` or YARP middleware).

---

## Implementation Verification Checklist

- [ ] **Code Quality**: No compiler warnings, nullable reference types handled (`string?`, `DateTime?`)
- [ ] **Naming**: Files and types follow project conventions (PascalCase for C#, snake_case for DB columns)
- [ ] **Architecture**: All code in `Features/Auth/` or `Features/Users/` — no logic in `Program.cs`
- [ ] **Functionality**: `POST /api/auth/forgot-password` returns 200 for both existing and non-existing emails
- [ ] **Functionality**: `POST /api/auth/reset-password` returns 400 with `INVALID_OR_EXPIRED_TOKEN` for bad/expired tokens
- [ ] **Testing**: 9 new unit tests passing, 0 regressions
- [ ] **Database**: Migration generated and applied, 2 nullable columns in `users` table
- [ ] **Documentation**: `data-model.md` and `api-endpoints.md` updated
