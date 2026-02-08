# Phase 2: Authentication Layer (JWT + Login) - ENRICHED

## Goal

Add complete JWT-based authentication to the backend, including secure password hashing, login endpoint, token generation, and endpoint protection following **Test-Driven Development (TDD)** principles and **Vertical Slice Architecture**.

## Prerequisites

- **Phase 1 completed**: User entity exists with the following structure (verified against data model):
  - `id` (UUID, PK)
  - `email` (string, unique, required, max 255)
  - `passwordHash` (string, required)
  - `firstName`, `lastName` (string, required, max 100)
  - `phone` (string, optional, max 20)
  - `role` (enum: Admin | Board | Member)
  - `isActive` (boolean, default true)
  - `createdAt`, `updatedAt` (datetime)
- User CRUD endpoints work (POST /api/users, GET /api/users, etc.)
- PostgreSQL database is running
- Phase 1 repository includes `GetByEmailAsync` method (required for auth)

## Why After Phase 1?

- User entity already functional; can create test users for auth testing
- Backend auth can be fully tested with Postman/curl before touching frontend
- Independent from UI concerns
- Clear separation of concerns following Vertical Slice Architecture

## Development Approach

**CRITICAL**: Follow Test-Driven Development (TDD) throughout this phase:
1. ✅ Write failing test first
2. ✅ Implement minimum code to make test pass
3. ✅ Refactor if needed
4. ✅ Repeat for next feature

**Work in baby steps**, completing one subtask at a time. Do not proceed to the next subtask until the current one is fully complete with passing tests.

## Task Breakdown (Sequential Implementation)

Complete these subtasks **one at a time** in order:

### Subtask 1: Password Hashing Service (TDD)
1. Write failing tests for password hashing and verification
2. Install BCrypt.Net-Next package
3. Implement `IPasswordHasher` interface and `PasswordHasher` class
4. Verify all tests pass
5. Register service in DI container

### Subtask 2: JWT Configuration
1. Add JWT settings to appsettings.json
2. Configure JWT secret using user-secrets (development)
3. Install Microsoft.AspNetCore.Authentication.JwtBearer package
4. Verify configuration loads correctly

### Subtask 3: JWT Token Service (TDD)
1. Write failing tests for token generation
2. Implement `JwtTokenService` with token generation
3. Verify token structure and claims
4. Ensure all tests pass

### Subtask 4: Authentication Middleware Setup
1. Configure JWT Bearer authentication in Program.cs
2. Add UseAuthentication() and UseAuthorization() middleware
3. Verify middleware is correctly registered

### Subtask 5: Auth Feature Implementation (TDD)
1. Write failing integration tests for login endpoint
2. Create AuthModels.cs (LoginRequest, LoginResponse, RegisterRequest, UserInfo)
3. Create FluentValidation validators (LoginRequestValidator, RegisterRequestValidator)
4. Implement AuthService with LoginAsync and RegisterAsync methods
5. Create AuthEndpoints.cs with login and register endpoints
6. Verify all tests pass (unit + integration)

### Subtask 6: Endpoint Protection (TDD)
1. Write failing tests for protected endpoints without token (expect 401)
2. Write failing tests for protected endpoints with token (expect 200)
3. Add RequireAuthorization to UsersEndpoints
4. Implement role-based authorization policies
5. Verify all tests pass

### Subtask 7: Update Phase 1 User Creation (TDD)
1. Write failing tests for CreateAsync using hashed passwords
2. Update UsersService to inject IPasswordHasher
3. Modify CreateAsync to use PasswordHasher instead of plaintext
4. Update existing tests to verify password hashing
5. Verify all tests pass

### Subtask 8: Comprehensive Testing & Documentation
1. Run full test suite (aim for >=90% coverage)
2. Manual testing with Postman/curl
3. Verify all checklist items
4. Document endpoints for frontend team

---

## Architecture

```
┌─────────────┐
│  POST /login│
└──────┬──────┘
       │
       v
┌─────────────────┐
│  AuthService    │──> Validate credentials
│                 │──> PasswordHasher.Verify()
│                 │──> JwtTokenService.Generate()
└─────────────────┘
       │
       v
┌─────────────────┐
│  JWT Token      │──> {"userId": "...", "email": "...", "role": "..."}
└─────────────────┘
       │
       v
┌─────────────────┐
│ Protected       │──> [RequireAuthorization]
│ Endpoints       │──> JWT Bearer middleware validates token
└─────────────────┘
```

**Architecture Notes:**
- Follows **Vertical Slice Architecture**: All auth code lives in `Features/Auth/`
- Adheres to **SOLID principles**:
  - **SRP**: PasswordHasher handles hashing, JwtTokenService handles tokens, AuthService handles auth logic
  - **DIP**: AuthService depends on abstractions (IUsersRepository, IPasswordHasher)
  - **ISP**: Clear, focused interfaces
- Uses **Minimal APIs** for endpoint definitions
- Integrates with existing **FluentValidation** pipeline
- Follows **Repository Pattern** established in Phase 1

---

## NuGet Packages to Add

### 1. JWT Bearer Authentication
```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.*" />
```

### 2. BCrypt for Password Hashing
```xml
<PackageReference Include="BCrypt.Net-Next" Version="4.0.*" />
```

**Add to**: `src/Abuvi.API/Abuvi.API.csproj`

**Command**:
```bash
dotnet add src/Abuvi.API package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src/Abuvi.API package BCrypt.Net-Next
dotnet restore
```

---

## Configuration

### 1. JWT Settings in appsettings.json
**Path**: `src/Abuvi.API/appsettings.json`

**Add JWT section**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "..."
  },
  "Jwt": {
    "Issuer": "https://abuvi.api",
    "Audience": "https://abuvi.app",
    "ExpiryInHours": 24
  },
  "AllowedOrigins": "http://localhost:5173"
}
```

### 2. JWT Secret in Development Settings
**DO NOT commit secrets to git!**

**Setup user secrets (REQUIRED for development)**:
```bash
cd src/Abuvi.API
dotnet user-secrets init
dotnet user-secrets set "Jwt:Secret" "your-strong-secret-key-at-least-32-characters-long-change-this-value"
```

⚠️ **Security**: For production, use **Environment Variables** or **Azure Key Vault**, never commit secrets to git.

---

## Files to Create

### 1. IPasswordHasher.cs
**Path**: `src/Abuvi.API/Features/Auth/IPasswordHasher.cs`

**Purpose**: Interface for password hashing operations

```csharp
namespace Abuvi.API.Features.Auth;

/// <summary>
/// Provides password hashing and verification using BCrypt
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a plaintext password using BCrypt with salt
    /// </summary>
    /// <param name="password">The plaintext password to hash</param>
    /// <returns>BCrypt hashed password string</returns>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a plaintext password against a BCrypt hash
    /// </summary>
    /// <param name="password">The plaintext password to verify</param>
    /// <param name="passwordHash">The BCrypt hash to verify against</param>
    /// <returns>True if password matches hash, false otherwise</returns>
    bool VerifyPassword(string password, string passwordHash);
}
```

### 2. PasswordHasher.cs
**Path**: `src/Abuvi.API/Features/Auth/PasswordHasher.cs`

**Implementation using BCrypt**:
```csharp
namespace Abuvi.API.Features.Auth;

/// <summary>
/// BCrypt-based password hasher with configurable work factor
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12; // 2^12 = 4096 iterations

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: WorkFactor);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
```

**Security notes**:
- Work factor 12 provides strong security (2^12 = 4096 iterations)
- BCrypt automatically generates and stores salt in the hash
- Each hash is unique even for the same password (salted)

**TDD**: Write tests first in `src/Abuvi.Tests/Unit/Features/Auth/PasswordHasherTests.cs`

### 3. JwtTokenService.cs
**Path**: `src/Abuvi.API/Features/Auth/JwtTokenService.cs`

**Purpose**: Generate JWT tokens with user claims

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Abuvi.API.Features.Users;

namespace Abuvi.API.Features.Auth;

/// <summary>
/// Generates JWT tokens for authenticated users
/// </summary>
public class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Generates a JWT token for the specified user
    /// </summary>
    /// <param name="user">The user to generate a token for</param>
    /// <returns>JWT token string</returns>
    /// <exception cref="InvalidOperationException">Thrown when JWT secret is not configured</exception>
    public string GenerateToken(User user)
    {
        var secret = _configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT secret not configured");
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];
        var expiryHours = int.Parse(_configuration["Jwt:ExpiryInHours"] ?? "24");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

**Claims included**:
- `sub`: User ID (subject) - standard JWT claim
- `email`: User email
- `role`: User role (Admin, Board, Member) - for authorization
- `jti`: Unique token identifier - for token revocation (future)

**TDD**: Write tests first in `src/Abuvi.Tests/Unit/Features/Auth/JwtTokenServiceTests.cs`

### 4. AuthModels.cs
**Path**: `src/Abuvi.API/Features/Auth/AuthModels.cs`

**DTOs using C# records**:
```csharp
namespace Abuvi.API.Features.Auth;

/// <summary>
/// Request DTO for user login
/// </summary>
public record LoginRequest(
    string Email,
    string Password
);

/// <summary>
/// Request DTO for new user registration
/// </summary>
public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone
);

/// <summary>
/// Response DTO for successful login
/// </summary>
public record LoginResponse(
    string Token,
    UserInfo User
);

/// <summary>
/// User information included in auth responses
/// </summary>
public record UserInfo(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role
);
```

**Design notes**:
- Uses C# records (immutable DTOs)
- Follows naming conventions from backend standards
- Aligns with User entity structure from data model

### 5. LoginRequestValidator.cs
**Path**: `src/Abuvi.API/Features/Auth/LoginRequestValidator.cs`

```csharp
using FluentValidation;

namespace Abuvi.API.Features.Auth;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .WithMessage("A valid email address is required");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required");
    }
}
```

### 6. RegisterRequestValidator.cs
**Path**: `src/Abuvi.API/Features/Auth/RegisterRequestValidator.cs`

```csharp
using FluentValidation;

namespace Abuvi.API.Features.Auth;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255)
            .WithMessage("A valid email address (max 255 characters) is required");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one number");

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("First name is required (max 100 characters)");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Last name is required (max 100 characters)");

        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .When(x => x.Phone != null)
            .WithMessage("Phone number must not exceed 20 characters");
    }
}
```

**Validation notes**:
- Aligns with User entity constraints from data model
- Strong password requirements for security
- Uses FluentValidation following backend standards

### 7. AuthService.cs
**Path**: `src/Abuvi.API/Features/Auth/AuthService.cs`

**Purpose**: Business logic for authentication and registration

```csharp
using Abuvi.API.Features.Users;

namespace Abuvi.API.Features.Auth;

/// <summary>
/// Handles authentication and user registration logic
/// </summary>
public class AuthService
{
    private readonly IUsersRepository _usersRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;

    public AuthService(
        IUsersRepository usersRepository,
        IPasswordHasher passwordHasher,
        JwtTokenService jwtTokenService)
    {
        _usersRepository = usersRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>
    /// Authenticates a user and generates a JWT token
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>LoginResponse with token and user info, or null if authentication fails</returns>
    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        // Find user by email
        var user = await _usersRepository.GetByEmailAsync(request.Email);
        if (user == null) return null;

        // Verify password
        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            return null;

        // Check if user is active
        if (!user.IsActive) return null;

        // Generate JWT token
        var token = _jwtTokenService.GenerateToken(user);

        return new LoginResponse(
            token,
            new UserInfo(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.Role.ToString()
            )
        );
    }

    /// <summary>
    /// Registers a new user with Member role by default
    /// </summary>
    /// <param name="request">Registration details</param>
    /// <returns>UserInfo for the newly created user</returns>
    /// <exception cref="InvalidOperationException">Thrown when email already exists</exception>
    public async Task<UserInfo> RegisterAsync(RegisterRequest request)
    {
        // Check if email already exists
        var existing = await _usersRepository.GetByEmailAsync(request.Email);
        if (existing != null)
            throw new InvalidOperationException("Email already registered");

        // Hash password
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        // Create user with Member role by default
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = passwordHash,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            Role = UserRole.Member, // New registrations default to Member
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _usersRepository.AddAsync(user);

        return new UserInfo(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Role.ToString()
        );
    }
}
```

**Design notes**:
- Follows **Single Responsibility Principle**: AuthService handles auth logic only
- Depends on abstractions (IUsersRepository, IPasswordHasher) following **Dependency Inversion**
- Returns null for failed login (not throwing exceptions for authentication failures)
- Throws exception for business rule violations (duplicate email)
- New users default to Member role (security best practice)

**TDD**: Write tests first in `src/Abuvi.Tests/Unit/Features/Auth/AuthServiceTests.cs`

### 8. AuthEndpoints.cs
**Path**: `src/Abuvi.API/Features/Auth/AuthEndpoints.cs`

**Endpoints using Minimal API**:
```csharp
using Microsoft.AspNetCore.Mvc;
using Abuvi.API.Common.Models;

namespace Abuvi.API.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/login", Login)
            .WithName("Login")
            .Produces<ApiResponse<LoginResponse>>()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/register", Register)
            .WithName("Register")
            .Produces<ApiResponse<UserInfo>>()
            .Produces(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> Login(
        [FromBody] LoginRequest request,
        AuthService authService)
    {
        var result = await authService.LoginAsync(request);

        if (result == null)
        {
            return Results.Json(
                ApiResponse<LoginResponse>.Fail(
                    "Invalid email or password",
                    "INVALID_CREDENTIALS"
                ),
                statusCode: 401
            );
        }

        return Results.Ok(ApiResponse<LoginResponse>.Ok(result));
    }

    private static async Task<IResult> Register(
        [FromBody] RegisterRequest request,
        AuthService authService)
    {
        try
        {
            var user = await authService.RegisterAsync(request);
            return Results.Ok(ApiResponse<UserInfo>.Ok(user));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Json(
                ApiResponse<UserInfo>.Fail(ex.Message, "EMAIL_EXISTS"),
                statusCode: 400
            );
        }
    }
}
```

**Design notes**:
- Follows **Minimal API** pattern from backend standards
- Uses shared `ApiResponse<T>` wrapper for consistency
- Validation handled by FluentValidation endpoint filter (configured in Program.cs)
- Clear HTTP status codes: 200 (success), 401 (auth failed), 400 (validation/business error)

**TDD**: Write integration tests first in `src/Abuvi.Tests/Integration/Features/AuthIntegrationTests.cs`

---

## Files to Modify

### 1. Program.cs
**Path**: `src/Abuvi.API/Program.cs`

**Add authentication and authorization** (after services registration, before `var app = builder.Build();`):

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Abuvi.API.Features.Auth;

// ... existing code ...

// ========================================
// Authentication & Authorization
// ========================================
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT secret not configured. Use: dotnet user-secrets set \"Jwt:Secret\" \"your-secret-key\"");
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero // No tolerance for token expiry
        };
    });

builder.Services.AddAuthorization();

// Auth feature services
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AuthService>();
```

**Add middleware** (after `app.UseCors()`, before endpoint mapping):

```csharp
// Authentication and Authorization middleware (order matters!)
app.UseAuthentication(); // Must come before UseAuthorization
app.UseAuthorization();
```

**Map auth endpoints** (with other endpoint mappings):

```csharp
// Feature endpoints
app.MapAuthEndpoints();  // ADD THIS LINE
app.MapUsersEndpoints();
// ... other endpoints ...
```

**Configuration validation note**:
- The JWT secret validation throws a clear error message if not configured
- Helps developers quickly identify configuration issues on startup

### 2. UsersEndpoints.cs (Protect Endpoints)
**Path**: `src/Abuvi.API/Features/Users/UsersEndpoints.cs`

**Add RequireAuthorization**:

```csharp
public static void MapUsersEndpoints(this WebApplication app)
{
    var group = app.MapGroup("/api/users")
        .WithTags("Users");

    // List all users - Admin only
    group.MapGet("/", GetAllUsers)
        .RequireAuthorization(policy => policy.RequireRole("Admin"))
        .WithName("GetAllUsers");

    // Get by ID - Any authenticated user can view users
    group.MapGet("/{id:guid}", GetUserById)
        .RequireAuthorization()
        .WithName("GetUserById");

    // Create user - Admin only (Register endpoint is public)
    group.MapPost("/", CreateUser)
        .RequireAuthorization(policy => policy.RequireRole("Admin"))
        .WithName("CreateUser");

    // Update user - Requires authentication (can add role check or self-check later)
    group.MapPut("/{id:guid}", UpdateUser)
        .RequireAuthorization()
        .WithName("UpdateUser");

    // Delete user - Admin only
    group.MapDelete("/{id:guid}", DeleteUser)
        .RequireAuthorization(policy => policy.RequireRole("Admin"))
        .WithName("DeleteUser");
}
```

**Authorization notes**:
- **Admin-only endpoints**: List all users, Create user (via admin), Delete user
- **Authenticated endpoints**: Get user by ID, Update user
- **Public endpoint**: Register (POST /api/auth/register) - no authentication required
- Role-based authorization uses `RequireRole()` with role names matching UserRole enum

**Future enhancement**: Add self-authorization check for UpdateUser (users can only update their own profile unless Admin)

### 3. UsersService.cs (Update to Use PasswordHasher)
**Path**: `src/Abuvi.API/Features/Users/UsersService.cs`

**Inject IPasswordHasher** and update CreateAsync:

```csharp
using Abuvi.API.Features.Auth; // ADD THIS

public class UsersService
{
    private readonly IUsersRepository _repository;
    private readonly IPasswordHasher _passwordHasher; // ADD THIS

    // UPDATE CONSTRUCTOR
    public UsersService(IUsersRepository repository, IPasswordHasher passwordHasher)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
    }

    // UPDATE CreateAsync METHOD
    public async Task<UserResponse> CreateAsync(CreateUserRequest request)
    {
        // Check if email already exists
        var existing = await _repository.GetByEmailAsync(request.Email);
        if (existing != null)
            throw new InvalidOperationException("Email already exists");

        // Hash password with BCrypt (UPDATED)
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = passwordHash,  // Now uses BCrypt instead of plaintext
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            Role = request.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(user);
        return MapToResponse(user);
    }

    // ... rest of the service methods ...
}
```

**Migration note**:
- Existing users with plaintext passwords in the database will NOT work after this change
- **Action required**: Either reset the database or migrate existing passwords to BCrypt hashes
- For development: Reset database and recreate test users with hashed passwords

### 4. IUsersRepository.cs (Verify Method Exists)
**Path**: `src/Abuvi.API/Features/Users/IUsersRepository.cs`

**Ensure GetByEmailAsync exists**:

```csharp
public interface IUsersRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email); // VERIFY THIS EXISTS
    Task<IReadOnlyList<User>> GetAllAsync();
    Task AddAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(Guid id);
}
```

**If GetByEmailAsync does NOT exist**, add it to the repository interface and implementation.

**UsersRepository.cs implementation**:
```csharp
public async Task<User?> GetByEmailAsync(string email)
{
    return await _context.Users
        .AsNoTracking()
        .FirstOrDefaultAsync(u => u.Email == email);
}
```

---

## Testing

### Test Structure

Following **TDD** and backend standards, tests should be organized as:

```
Abuvi.Tests/
├── Unit/
│   └── Features/
│       └── Auth/
│           ├── PasswordHasherTests.cs
│           ├── JwtTokenServiceTests.cs
│           ├── AuthServiceTests.cs
│           └── AuthValidatorTests.cs
└── Integration/
    └── Features/
        └── AuthIntegrationTests.cs
```

### Unit Tests

#### 1. PasswordHasherTests.cs
**Path**: `src/Abuvi.Tests/Unit/Features/Auth/PasswordHasherTests.cs`

**Write these tests FIRST (TDD)**:

```csharp
using FluentAssertions;
using Xunit;
using Abuvi.API.Features.Auth;

namespace Abuvi.Tests.Unit.Features.Auth;

public class PasswordHasherTests
{
    private readonly IPasswordHasher _sut;

    public PasswordHasherTests()
    {
        _sut = new PasswordHasher();
    }

    [Fact]
    public void HashPassword_WithValidPassword_ReturnsNonEmptyHash()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash = _sut.HashPassword(password);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Should().StartWith("$2"); // BCrypt hash prefix
    }

    [Fact]
    public void HashPassword_SamePasswordTwice_ReturnsDifferentHashes()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash1 = _sut.HashPassword(password);
        var hash2 = _sut.HashPassword(password);

        // Assert
        hash1.Should().NotBe(hash2); // BCrypt uses random salt
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "TestPassword123!";
        var hash = _sut.HashPassword(password);

        // Act
        var result = _sut.VerifyPassword(password, hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ReturnsFalse()
    {
        // Arrange
        var password = "TestPassword123!";
        var wrongPassword = "WrongPassword456!";
        var hash = _sut.HashPassword(password);

        // Act
        var result = _sut.VerifyPassword(wrongPassword, hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_WithEmptyPassword_ReturnsFalse()
    {
        // Arrange
        var password = "TestPassword123!";
        var hash = _sut.HashPassword(password);

        // Act
        var result = _sut.VerifyPassword(string.Empty, hash);

        // Assert
        result.Should().BeFalse();
    }
}
```

#### 2. JwtTokenServiceTests.cs
**Path**: `src/Abuvi.Tests/Unit/Features/Auth/JwtTokenServiceTests.cs`

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Users;

namespace Abuvi.Tests.Unit.Features.Auth;

public class JwtTokenServiceTests
{
    private readonly JwtTokenService _sut;
    private readonly IConfiguration _configuration;

    public JwtTokenServiceTests()
    {
        // Arrange - Mock configuration
        var inMemorySettings = new Dictionary<string, string>
        {
            {"Jwt:Secret", "test-secret-key-at-least-32-characters-long-for-hmacsha256"},
            {"Jwt:Issuer", "https://test.api"},
            {"Jwt:Audience", "https://test.app"},
            {"Jwt:ExpiryInHours", "24"}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        _sut = new JwtTokenService(_configuration);
    }

    [Fact]
    public void GenerateToken_WithValidUser_ReturnsValidJwtToken()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Role = UserRole.Member,
            FirstName = "Test",
            LastName = "User"
        };

        // Act
        var token = _sut.GenerateToken(user);

        // Assert
        token.Should().NotBeNullOrEmpty();
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Email);
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Member");
    }

    [Fact]
    public void GenerateToken_WithAdminUser_IncludesAdminRole()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.com",
            Role = UserRole.Admin,
            FirstName = "Admin",
            LastName = "User"
        };

        // Act
        var token = _sut.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
    }

    [Fact]
    public void GenerateToken_TokenExpiresAfterConfiguredHours()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", Role = UserRole.Member, FirstName = "Test", LastName = "User" };

        // Act
        var token = _sut.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var expiryTime = jwtToken.ValidTo;
        var expectedExpiry = DateTime.UtcNow.AddHours(24);

        expiryTime.Should().BeCloseTo(expectedExpiry, TimeSpan.FromMinutes(1));
    }
}
```

#### 3. AuthServiceTests.cs
**Path**: `src/Abuvi.Tests/Unit/Features/Auth/AuthServiceTests.cs`

```csharp
using FluentAssertions;
using NSubstitute;
using Xunit;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Users;

namespace Abuvi.Tests.Unit.Features.Auth;

public class AuthServiceTests
{
    private readonly IUsersRepository _usersRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _usersRepository = Substitute.For<IUsersRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _jwtTokenService = Substitute.For<JwtTokenService>(Substitute.For<IConfiguration>());
        _sut = new AuthService(_usersRepository, _passwordHasher, _jwtTokenService);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsLoginResponse()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hashed-password",
            FirstName = "Test",
            LastName = "User",
            Role = UserRole.Member,
            IsActive = true
        };

        _usersRepository.GetByEmailAsync("test@example.com").Returns(user);
        _passwordHasher.VerifyPassword("password123", "hashed-password").Returns(true);
        _jwtTokenService.GenerateToken(user).Returns("jwt-token");

        var request = new LoginRequest("test@example.com", "password123");

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Token.Should().Be("jwt-token");
        result.User.Email.Should().Be("test@example.com");
        result.User.Role.Should().Be("Member");
    }

    [Fact]
    public async Task LoginAsync_WithInvalidEmail_ReturnsNull()
    {
        // Arrange
        _usersRepository.GetByEmailAsync("invalid@example.com").Returns((User?)null);
        var request = new LoginRequest("invalid@example.com", "password123");

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ReturnsNull()
    {
        // Arrange
        var user = new User
        {
            Email = "test@example.com",
            PasswordHash = "hashed-password",
            IsActive = true
        };

        _usersRepository.GetByEmailAsync("test@example.com").Returns(user);
        _passwordHasher.VerifyPassword("wrong-password", "hashed-password").Returns(false);
        var request = new LoginRequest("test@example.com", "wrong-password");

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_WithInactiveUser_ReturnsNull()
    {
        // Arrange
        var user = new User
        {
            Email = "test@example.com",
            PasswordHash = "hashed-password",
            IsActive = false
        };

        _usersRepository.GetByEmailAsync("test@example.com").Returns(user);
        _passwordHasher.VerifyPassword("password123", "hashed-password").Returns(true);
        var request = new LoginRequest("test@example.com", "password123");

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RegisterAsync_WithUniqueEmail_CreatesUserAndReturnsUserInfo()
    {
        // Arrange
        _usersRepository.GetByEmailAsync("newuser@example.com").Returns((User?)null);
        _passwordHasher.HashPassword("Password123!").Returns("hashed-password");
        var request = new RegisterRequest("newuser@example.com", "Password123!", "New", "User", null);

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be("newuser@example.com");
        result.Role.Should().Be("Member");
        await _usersRepository.Received(1).AddAsync(Arg.Is<User>(u =>
            u.Email == "newuser@example.com" &&
            u.PasswordHash == "hashed-password" &&
            u.Role == UserRole.Member &&
            u.IsActive == true
        ));
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ThrowsInvalidOperationException()
    {
        // Arrange
        var existingUser = new User { Email = "existing@example.com" };
        _usersRepository.GetByEmailAsync("existing@example.com").Returns(existingUser);
        var request = new RegisterRequest("existing@example.com", "Password123!", "New", "User", null);

        // Act
        var act = () => _sut.RegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Email already registered");
    }
}
```

### Integration Tests

#### AuthIntegrationTests.cs
**Path**: `src/Abuvi.Tests/Integration/Features/AuthIntegrationTests.cs`

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;
using Abuvi.API.Features.Auth;
using Abuvi.API.Common.Models;

namespace Abuvi.Tests.Integration.Features;

public class AuthIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidData_Returns200AndCreatesUser()
    {
        // Arrange
        var request = new RegisterRequest(
            $"test-{Guid.NewGuid()}@example.com",
            "Password123!",
            "Test",
            "User",
            null
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserInfo>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Email.Should().Be(request.Email);
        result.Data.Role.Should().Be("Member");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns400()
    {
        // Arrange
        var email = $"duplicate-{Guid.NewGuid()}@example.com";
        var request = new RegisterRequest(email, "Password123!", "Test", "User", null);

        // Create first user
        await _client.PostAsJsonAsync("/api/auth/register", request);

        // Act - Try to create again
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserInfo>>();
        result!.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("EMAIL_EXISTS");
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200AndToken()
    {
        // Arrange - Create user first
        var email = $"login-{Guid.NewGuid()}@example.com";
        var password = "Password123!";
        var registerRequest = new RegisterRequest(email, password, "Test", "User", null);
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new LoginRequest(email, password);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Token.Should().NotBeNullOrEmpty();
        result.Data.User.Email.Should().Be(email);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_Returns401()
    {
        // Arrange
        var email = $"invalid-pw-{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "Password123!", "Test", "User", null));

        var loginRequest = new LoginRequest(email, "WrongPassword!");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        result!.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_WithInvalidEmail_Returns401()
    {
        // Arrange
        var loginRequest = new LoginRequest("nonexistent@example.com", "Password123!");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_Returns200()
    {
        // Arrange - Register and login
        var email = $"protected-{Guid.NewGuid()}@example.com";
        var password = "Password123!";
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, password, "Test", "User", null));

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, password));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        var token = loginResult!.Data!.Token;

        // Set authorization header
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/users/{loginResult.Data.User.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminOnlyEndpoint_WithMemberToken_Returns403()
    {
        // Arrange - Register and login as Member
        var email = $"member-{Guid.NewGuid()}@example.com";
        var password = "Password123!";
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, password, "Test", "User", null));

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, password));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        var token = loginResult!.Data!.Token;

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Try to access admin-only endpoint
        var response = await _client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
```

---

## Verification Checklist

After completing Phase 2, verify the following:

### Configuration
- [ ] JWT settings present in appsettings.json (Issuer, Audience, ExpiryInHours)
- [ ] JWT secret configured in user-secrets (NOT in appsettings.Development.json)
- [ ] Authentication/Authorization registered in Program.cs
- [ ] UseAuthentication() placed BEFORE UseAuthorization() in middleware pipeline
- [ ] All auth services registered in DI container

### Password Security
- [ ] BCrypt.Net-Next package installed
- [ ] IPasswordHasher and PasswordHasher implemented
- [ ] UsersService uses PasswordHasher.HashPassword() for new users
- [ ] Passwords are NEVER stored in plaintext
- [ ] Password verification uses BCrypt.Verify()
- [ ] Work factor is 12 (strong security)

### Authentication Endpoints
- [ ] POST /api/auth/register creates new user with Member role (200 OK)
- [ ] POST /api/auth/register with duplicate email returns 400 with "EMAIL_EXISTS"
- [ ] POST /api/auth/register validates password strength (8+ chars, uppercase, lowercase, number)
- [ ] POST /api/auth/login with valid credentials returns JWT token (200 OK)
- [ ] POST /api/auth/login with invalid email returns 401 with "INVALID_CREDENTIALS"
- [ ] POST /api/auth/login with invalid password returns 401 with "INVALID_CREDENTIALS"
- [ ] POST /api/auth/login with inactive user returns 401
- [ ] LoginResponse includes both token and user info

### Authorization
- [ ] GET /api/users without token returns 401 Unauthorized
- [ ] GET /api/users with Member token returns 403 Forbidden (Admin only)
- [ ] GET /api/users with Admin token returns 200 OK
- [ ] GET /api/users/{id} with valid token returns 200 OK (authenticated)
- [ ] POST /api/users with Admin token returns 201 Created
- [ ] POST /api/users with Member token returns 403 Forbidden

### JWT Token
- [ ] Token contains `sub` claim (User ID)
- [ ] Token contains `email` claim
- [ ] Token contains `role` claim (Admin, Board, or Member)
- [ ] Token contains `jti` claim (unique identifier)
- [ ] Token is signed with HMACSHA256
- [ ] Token expires after configured hours (24h for dev)
- [ ] Token can be decoded and validated by JWT middleware

### Tests
- [ ] All unit tests pass (PasswordHasher, JwtTokenService, AuthService)
- [ ] All integration tests pass (register, login, protected endpoints)
- [ ] Test coverage >= 90% for Auth feature
- [ ] Tests follow AAA pattern (Arrange-Act-Assert)
- [ ] Tests use descriptive names (MethodName_StateUnderTest_ExpectedBehavior)

### Code Quality
- [ ] All code follows Vertical Slice Architecture (Features/Auth/)
- [ ] All code follows SOLID principles
- [ ] All code uses C# 13 features (primary constructors, records, file-scoped namespaces)
- [ ] All async methods accept CancellationToken
- [ ] No compiler warnings or errors
- [ ] Code passes dotnet format

---

## Testing with Postman/curl

### 1. Register a new user
```bash
curl -X POST http://localhost:5079/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "testuser@example.com",
    "password": "TestPassword123!",
    "firstName": "Test",
    "lastName": "User",
    "phone": null
  }'
```

**Expected response**:
```json
{
  "success": true,
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "testuser@example.com",
    "firstName": "Test",
    "lastName": "User",
    "role": "Member"
  },
  "error": null
}
```

### 2. Login
```bash
curl -X POST http://localhost:5079/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "testuser@example.com",
    "password": "TestPassword123!"
  }'
```

**Expected response**:
```json
{
  "success": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "user": {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "email": "testuser@example.com",
      "firstName": "Test",
      "lastName": "User",
      "role": "Member"
    }
  },
  "error": null
}
```

### 3. Access protected endpoint (without token - should fail)
```bash
curl -X GET http://localhost:5079/api/users
```

**Expected response**: `401 Unauthorized`

### 4. Access protected endpoint (with token - should succeed)
```bash
curl -X GET http://localhost:5079/api/users/{userId} \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

**Expected response**: `200 OK` with user data

### 5. Access admin-only endpoint with Member token (should fail)
```bash
curl -X GET http://localhost:5079/api/users \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

**Expected response**: `403 Forbidden`

---

## Database Migration Notes

**IMPORTANT**: Existing users with plaintext passwords will NOT work after this phase.

**Options**:

1. **Reset database** (recommended for development):
   ```bash
   dotnet ef database drop --project src/Abuvi.API --force
   dotnet ef database update --project src/Abuvi.API
   ```

2. **Migrate existing passwords** (if preserving data):
   - Create a migration script to hash existing plaintext passwords
   - Run once during deployment

**For Phase 2 development**: Option 1 (reset database) is recommended.

---

## Security Notes

### Development
- ✅ Use `dotnet user-secrets` for JWT secret
- ✅ NEVER commit `appsettings.Development.json` with secrets
- ✅ JWT secret must be at least 32 characters for HMACSHA256

### Production
- ✅ Use Environment Variables or Azure Key Vault for JWT secret
- ✅ Set token expiry to 1 hour (not 24 hours)
- ✅ Implement refresh tokens (future enhancement)
- ✅ Add rate limiting for login endpoint (future enhancement)
- ✅ Log failed login attempts (future enhancement)
- ✅ Use HTTPS only in production

### Password Security
- ✅ BCrypt work factor 12 (strong, but not too slow)
- ✅ Passwords never stored in plaintext
- ✅ Passwords never logged
- ✅ Strong password requirements enforced by validation

---

## Next Steps

After Phase 2 is complete and verified:

1. ✅ Backend authentication is fully functional
2. ✅ Test all scenarios with Postman/curl
3. ✅ Ensure test coverage >= 90%
4. ✅ Document API endpoints with OpenAPI/Swagger annotations
5. ➡️ Proceed to **Phase 3: Frontend Integration**

---

## Common Issues & Troubleshooting

### Issue: "JWT secret not configured" error
**Solution**: Run `dotnet user-secrets set "Jwt:Secret" "your-strong-secret-key-at-least-32-characters-long" --project src/Abuvi.API`

### Issue: Token validation fails
**Solution**: Ensure `ValidateIssuerSigningKey = true` and secret matches between token generation and validation

### Issue: Existing users can't login
**Solution**: Existing passwords are plaintext. Reset database or migrate passwords to BCrypt hashes

### Issue: 401 Unauthorized on all endpoints
**Solution**: Verify `UseAuthentication()` is called BEFORE `UseAuthorization()` in Program.cs

### Issue: 403 Forbidden on admin endpoints
**Solution**: Verify role claim is correctly set in JWT token and matches `RequireRole("Admin")`

---

## Definition of Done

Phase 2 is complete when:

- [x] All NuGet packages installed
- [x] All configuration files updated
- [x] All new files created in Features/Auth/
- [x] All existing files modified (Program.cs, UsersService.cs, UsersEndpoints.cs)
- [x] All unit tests written and passing
- [x] All integration tests written and passing
- [x] Test coverage >= 90%
- [x] Manual testing with Postman/curl successful
- [x] All verification checklist items checked
- [x] Code follows Vertical Slice Architecture
- [x] Code follows SOLID principles
- [x] Code passes `dotnet format`
- [x] Documentation updated for Phase 3 frontend integration
