# Phase 2: Authentication Layer (JWT + Login)

## Goal

Add complete JWT-based authentication to the backend, including secure password hashing, login endpoint, token generation, and endpoint protection.

## Prerequisites

- **Phase 1 completed**: User entity exists, CRUD endpoints work
- Test users can be created via POST /api/users
- PostgreSQL database is running

## Why After Phase 1?

- User entity already functional; can create test users for auth testing
- Backend auth can be fully tested with Postman/curl before touching frontend
- Independent from UI concerns
- Clear separation of concerns

## What We're Building

1. BCrypt password hashing service
2. JWT token generation service
3. Login endpoint (POST /api/auth/login)
4. Register endpoint (POST /api/auth/register)
5. JWT authentication middleware
6. Endpoint protection with RequireAuthorization
7. Role-based authorization (Admin, Board, Member)
8. Comprehensive auth tests

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
│ Protected       │──> [Authorize] attribute
│ Endpoints       │──> JWT Bearer middleware validates token
└─────────────────┘
```

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
```

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
**Path**: `src/Abuvi.API/appsettings.Development.json`

**Add Secret (for development ONLY)**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Jwt": {
    "Secret": "your-256-bit-secret-key-for-development-only-change-this"
  }
}
```

⚠️ **Important**: For production, use **User Secrets** or **Environment Variables**, never commit secrets to git.

**Setup user secrets (recommended for development)**:
```bash
cd src/Abuvi.API
dotnet user-secrets init
dotnet user-secrets set "Jwt:Secret" "your-strong-secret-key-at-least-32-characters-long"
```

## Files to Create

### 1. IPasswordHasher.cs + PasswordHasher.cs
**Path**: `src/Abuvi.API/Features/Auth/IPasswordHasher.cs`

**Interface**:
```csharp
namespace Abuvi.API.Features.Auth;

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}
```

**Path**: `src/Abuvi.API/Features/Auth/PasswordHasher.cs`

**Implementation using BCrypt**:
```csharp
using BCrypt.Net;

namespace Abuvi.API.Features.Auth;

public class PasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
```

**Security notes**:
- Work factor 12 provides strong security (2^12 = 4096 iterations)
- BCrypt automatically generates and stores salt
- Each hash is unique even for same password

### 2. JwtTokenService.cs
**Path**: `src/Abuvi.API/Features/Auth/JwtTokenService.cs`

**Purpose**: Generate JWT tokens with user claims

**Dependencies**:
```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Abuvi.API.Features.Users;
```

**Key methods**:
```csharp
public class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

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
- `sub`: User ID (subject)
- `email`: User email
- `role`: User role (Admin, Board, Member)
- `jti`: Unique token identifier

### 3. AuthModels.cs
**Path**: `src/Abuvi.API/Features/Auth/AuthModels.cs`

**DTOs**:
```csharp
namespace Abuvi.API.Features.Auth;

public record LoginRequest(
    string Email,
    string Password
);

public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone
);

public record LoginResponse(
    string Token,
    UserInfo User
);

public record UserInfo(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role
);
```

### 4. AuthService.cs
**Path**: `src/Abuvi.API/Features/Auth/AuthService.cs`

**Purpose**: Handle login logic

**Key methods**:
```csharp
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
            Role = UserRole.Member,
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
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty();
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
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one number");

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .When(x => x.Phone != null);
    }
}
```

### 7. AuthEndpoints.cs
**Path**: `src/Abuvi.API/Features/Auth/AuthEndpoints.cs`

**Endpoints**:
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

        group.MapPost("/login", Login);
        group.MapPost("/register", Register);
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

## Files to Modify

### 1. Program.cs
**Path**: `src/Abuvi.API/Program.cs`

**Add JWT Authentication** (after services registration, before `var app = builder.Build();`):

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// ... existing code ...

// Authentication & Authorization
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT secret not configured");
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
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
app.UseAuthentication();
app.UseAuthorization();
```

**Map auth endpoints** (with other endpoint mappings):

```csharp
app.MapAuthEndpoints();
```

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
        .RequireAuthorization(policy => policy.RequireRole("Admin"));

    // Get by ID - Any authenticated user can view users
    group.MapGet("/{id}", GetUserById)
        .RequireAuthorization();

    // Create user - Admin only (Register endpoint is public)
    group.MapPost("/", CreateUser)
        .RequireAuthorization(policy => policy.RequireRole("Admin"));

    // Update user - Admin or self
    group.MapPut("/{id}", UpdateUser)
        .RequireAuthorization();
}
```

### 3. UsersService.cs (Update to Use PasswordHasher)
**Path**: `src/Abuvi.API/Features/Users/UsersService.cs`

**Inject IPasswordHasher** and update CreateAsync:

```csharp
private readonly IPasswordHasher _passwordHasher;

public UsersService(IUsersRepository repository, IPasswordHasher passwordHasher)
{
    _repository = repository;
    _passwordHasher = passwordHasher;
}

public async Task<UserResponse> CreateAsync(CreateUserRequest request)
{
    // Check if email already exists
    var existing = await _repository.GetByEmailAsync(request.Email);
    if (existing != null)
        throw new InvalidOperationException("Email already exists");

    // Hash password with BCrypt
    var passwordHash = _passwordHasher.HashPassword(request.Password);

    var user = new User
    {
        Id = Guid.NewGuid(),
        Email = request.Email,
        PasswordHash = passwordHash,  // Now uses BCrypt
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
```

## Testing

### Unit Tests

#### 1. PasswordHasherTests.cs
**Path**: `src/Abuvi.Tests/Unit/Features/Auth/PasswordHasherTests.cs`

```csharp
[Fact]
public void HashPassword_ReturnsDifferentHashForSamePassword()
{
    var hasher = new PasswordHasher();
    var hash1 = hasher.HashPassword("password123");
    var hash2 = hasher.HashPassword("password123");

    hash1.Should().NotBe(hash2); // BCrypt uses random salt
}

[Fact]
public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
{
    var hasher = new PasswordHasher();
    var password = "password123";
    var hash = hasher.HashPassword(password);

    var result = hasher.VerifyPassword(password, hash);

    result.Should().BeTrue();
}
```

#### 2. JwtTokenServiceTests.cs
**Path**: `src/Abuvi.Tests/Unit/Features/Auth/JwtTokenServiceTests.cs`

```csharp
[Fact]
public void GenerateToken_ReturnsValidJwtToken()
{
    var config = CreateMockConfiguration();
    var service = new JwtTokenService(config);
    var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", Role = UserRole.Member };

    var token = service.GenerateToken(user);

    token.Should().NotBeNullOrEmpty();
    var handler = new JwtSecurityTokenHandler();
    var jwtToken = handler.ReadJwtToken(token);
    jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "test@example.com");
}
```

### Integration Tests

#### 3. AuthIntegrationTests.cs
**Path**: `src/Abuvi.Tests/Integration/Features/AuthIntegrationTests.cs`

```csharp
[Fact]
public async Task Login_WithValidCredentials_ReturnsToken()
{
    // Arrange: Create user first
    await CreateTestUser("test@example.com", "Password123");
    var loginRequest = new LoginRequest("test@example.com", "Password123");

    // Act
    var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
    result!.Success.Should().BeTrue();
    result.Data!.Token.Should().NotBeNullOrEmpty();
}

[Fact]
public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
{
    await CreateTestUser("test@example.com", "Password123");
    var loginRequest = new LoginRequest("test@example.com", "WrongPassword");

    var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}

[Fact]
public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
{
    var response = await _client.GetAsync("/api/users");

    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}

[Fact]
public async Task ProtectedEndpoint_WithValidToken_ReturnsOk()
{
    var token = await GetAuthToken();
    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var response = await _client.GetAsync("/api/users");

    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

## Verification Checklist

After completing Phase 2, verify:

### Configuration
- [ ] JWT settings in appsettings.json
- [ ] JWT secret configured (user-secrets or environment variable)
- [ ] Authentication/Authorization registered in Program.cs
- [ ] UseAuthentication() and UseAuthorization() in pipeline

### Password Security
- [ ] Existing users' passwords migrated to BCrypt hashes
- [ ] New users get BCrypt hashed passwords
- [ ] Passwords are NOT stored in plaintext
- [ ] Password verification works correctly

### Authentication
- [ ] POST /api/auth/login with valid credentials returns JWT (200 OK)
- [ ] POST /api/auth/login with invalid email returns 401
- [ ] POST /api/auth/login with invalid password returns 401
- [ ] POST /api/auth/login with inactive user returns 401
- [ ] POST /api/auth/register creates new user with Member role (200 OK)
- [ ] POST /api/auth/register with duplicate email returns 400

### Authorization
- [ ] GET /api/users without token returns 401 Unauthorized
- [ ] GET /api/users with valid token returns 200 OK
- [ ] GET /api/users with Admin role succeeds
- [ ] GET /api/users with Member role fails (403 Forbidden)
- [ ] Token expires after configured time

### JWT Token
- [ ] Token contains userId claim
- [ ] Token contains email claim
- [ ] Token contains role claim
- [ ] Token can be decoded and validated
- [ ] Token signature is valid

### Tests
- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] Test coverage >= 90%

## Testing with Postman/curl

### 1. Register a new user
```bash
curl -X POST http://localhost:5079/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@example.com",
    "password": "Admin123!",
    "firstName": "Admin",
    "lastName": "User",
    "phone": null
  }'
```

### 2. Login
```bash
curl -X POST http://localhost:5079/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@example.com",
    "password": "Admin123!"
  }'
```

Response:
```json
{
  "success": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "user": {
      "id": "...",
      "email": "admin@example.com",
      "firstName": "Admin",
      "lastName": "User",
      "role": "Member"
    }
  }
}
```

### 3. Access protected endpoint
```bash
curl -X GET http://localhost:5079/api/users \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

## Next Steps

After Phase 2 is complete and verified:
1. Backend authentication is fully functional
2. Test all scenarios with Postman
3. Document API endpoints for frontend team
4. Proceed to **Phase 3: Frontend Integration** (`phase3_frontend_integration.md`)

## Security Notes

- **Never** commit JWT secrets to git
- Use **user-secrets** for local development
- Use **environment variables** or **Azure Key Vault** for production
- Set appropriate token expiry (1 hour for production, 24 hours for dev)
- Implement refresh tokens for better UX (optional, future enhancement)
- Consider rate limiting for login endpoint (future enhancement)
- Add logging for failed login attempts (future enhancement)
