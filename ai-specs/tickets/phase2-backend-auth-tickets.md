# Phase 2: Authentication Layer - Backend Implementation Tickets

**Epic**: JWT-based Authentication System
**Priority**: High
**Phase**: 2
**Approach**: Test-Driven Development (TDD) + Vertical Slice Architecture

---

## Pre-Implementation Checklist

- [ ] Phase 1 completed: User CRUD endpoints functional
- [ ] PostgreSQL database running
- [ ] User entity has required fields (id, email, passwordHash, firstName, lastName, phone, role, isActive, createdAt, updatedAt)
- [ ] IUsersRepository.GetByEmailAsync exists ✅ (VERIFIED)
- [ ] Current working branch: `feature/phase2-authentication-backend`

---

## Ticket 1: Password Hashing Service Implementation (TDD)

**Story**: As a backend developer, I need a secure password hashing service using BCrypt so that user passwords are never stored in plaintext.

**Acceptance Criteria**:
- [ ] BCrypt.Net-Next package installed (version 4.0.*)
- [ ] IPasswordHasher interface created with HashPassword and VerifyPassword methods
- [ ] PasswordHasher class implements IPasswordHasher using BCrypt with work factor 12
- [ ] All unit tests pass (TDD approach)
- [ ] Service registered in DI container

**Files to Create**:
- `src/Abuvi.API/Features/Auth/IPasswordHasher.cs`
- `src/Abuvi.API/Features/Auth/PasswordHasher.cs`
- `src/Abuvi.Tests/Unit/Features/Auth/PasswordHasherTests.cs`

**TDD Test Cases** (Write FIRST):
1. `HashPassword_WithValidPassword_ReturnsNonEmptyHash` - Verify hash is not empty and starts with "$2"
2. `HashPassword_SamePasswordTwice_ReturnsDifferentHashes` - Verify BCrypt uses random salt
3. `VerifyPassword_WithCorrectPassword_ReturnsTrue` - Verify correct password verification
4. `VerifyPassword_WithIncorrectPassword_ReturnsFalse` - Verify incorrect password rejection
5. `VerifyPassword_WithEmptyPassword_ReturnsFalse` - Verify empty password handling

**Implementation Steps** (TDD):
1. ✅ Write failing tests first
2. Install NuGet package: `dotnet add src/Abuvi.API package BCrypt.Net-Next`
3. Create IPasswordHasher interface (tests will fail)
4. Implement PasswordHasher class (tests should pass)
5. Register in Program.cs: `builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();`
6. Verify all tests pass: `dotnet test --filter "PasswordHasherTests"`

**Verification**:
```bash
# Run tests
dotnet test --filter "PasswordHasherTests"

# Expected: All 5 tests pass, 0 failures
```

**Estimated Time**: 1-2 hours

---

## Ticket 2: JWT Configuration Setup

**Story**: As a backend developer, I need JWT configuration properly set up so that tokens can be generated and validated securely.

**Acceptance Criteria**:
- [ ] JWT settings added to appsettings.json (Issuer, Audience, ExpiryInHours)
- [ ] JWT secret configured in user-secrets (NOT in appsettings)
- [ ] Microsoft.AspNetCore.Authentication.JwtBearer package installed (version 9.0.*)
- [ ] Configuration validation on startup (fails fast if secret missing)

**Files to Modify**:
- `src/Abuvi.API/appsettings.json`
- `src/Abuvi.API/Abuvi.API.csproj`

**Implementation Steps**:
1. Install NuGet package: `dotnet add src/Abuvi.API package Microsoft.AspNetCore.Authentication.JwtBearer`
2. Add JWT section to appsettings.json:
```json
{
  "Jwt": {
    "Issuer": "https://abuvi.api",
    "Audience": "https://abuvi.app",
    "ExpiryInHours": 24
  }
}
```
3. Initialize and set user secret:
```bash
cd src/Abuvi.API
dotnet user-secrets init
dotnet user-secrets set "Jwt:Secret" "your-strong-secret-key-at-least-32-characters-long-change-this-value"
```

**Verification**:
```bash
# Verify secret is set (should show the secret)
dotnet user-secrets list --project src/Abuvi.API

# App should start without errors
dotnet run --project src/Abuvi.API
```

**Security Notes**:
- ⚠️ NEVER commit secrets to git
- ✅ Use environment variables for production
- ✅ Secret must be at least 32 characters for HMACSHA256

**Estimated Time**: 30 minutes

---

## Ticket 3: JWT Token Service Implementation (TDD)

**Story**: As a backend developer, I need a JWT token generation service so that authenticated users can receive valid JWT tokens.

**Acceptance Criteria**:
- [ ] JwtTokenService class created with GenerateToken method
- [ ] Token includes claims: sub (userId), email, role, jti
- [ ] Token signed with HMACSHA256
- [ ] Token expiry configurable via appsettings
- [ ] All unit tests pass (TDD approach)
- [ ] Service registered in DI container

**Files to Create**:
- `src/Abuvi.API/Features/Auth/JwtTokenService.cs`
- `src/Abuvi.Tests/Unit/Features/Auth/JwtTokenServiceTests.cs`

**TDD Test Cases** (Write FIRST):
1. `GenerateToken_WithValidUser_ReturnsValidJwtToken` - Verify token structure
2. `GenerateToken_TokenContainsUserClaims` - Verify sub, email, role claims
3. `GenerateToken_WithAdminUser_IncludesAdminRole` - Verify role claim for Admin
4. `GenerateToken_WithMemberUser_IncludesMemberRole` - Verify role claim for Member
5. `GenerateToken_TokenExpiresAfterConfiguredHours` - Verify expiry time

**Implementation Steps** (TDD):
1. ✅ Write failing tests first (with mock IConfiguration)
2. Create JwtTokenService class (tests will fail)
3. Implement GenerateToken method using JwtSecurityTokenHandler
4. Verify all tests pass: `dotnet test --filter "JwtTokenServiceTests"`
5. Register in Program.cs: `builder.Services.AddScoped<JwtTokenService>();`

**Verification**:
```bash
# Run tests
dotnet test --filter "JwtTokenServiceTests"

# Expected: All 5 tests pass, 0 failures
```

**Token Structure**:
```
Claims:
- sub: User.Id (Guid)
- email: User.Email
- role: User.Role (Admin/Board/Member)
- jti: Unique token identifier (Guid)
- exp: Expiry timestamp
- iss: Issuer from config
- aud: Audience from config
```

**Estimated Time**: 2-3 hours

---

## Ticket 4: Authentication Middleware Setup

**Story**: As a backend developer, I need JWT Bearer authentication middleware configured so that protected endpoints can validate tokens.

**Acceptance Criteria**:
- [ ] JWT Bearer authentication registered in DI
- [ ] Token validation parameters configured (ValidateIssuer, ValidateAudience, ValidateLifetime, ValidateIssuerSigningKey)
- [ ] UseAuthentication() middleware added BEFORE UseAuthorization()
- [ ] Authorization services registered
- [ ] Startup fails fast with clear error if JWT secret missing

**Files to Modify**:
- `src/Abuvi.API/Program.cs`

**Implementation Steps**:
1. Add authentication configuration after CORS setup:
```csharp
// Authentication & Authorization
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
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
```

2. Add middleware after UseCors():
```csharp
app.UseAuthentication(); // MUST come before UseAuthorization
app.UseAuthorization();
```

**Verification**:
```bash
# Should start successfully
dotnet run --project src/Abuvi.API

# Should fail with clear error message if secret not set
dotnet user-secrets remove "Jwt:Secret" --project src/Abuvi.API
dotnet run --project src/Abuvi.API
# Expected: InvalidOperationException with helpful message

# Restore secret
dotnet user-secrets set "Jwt:Secret" "your-strong-secret-key-at-least-32-characters-long" --project src/Abuvi.API
```

**Estimated Time**: 1 hour

---

## Ticket 5: Auth Models and Validators

**Story**: As a backend developer, I need authentication DTOs and validators so that login and registration requests are properly validated.

**Acceptance Criteria**:
- [ ] LoginRequest record created (Email, Password)
- [ ] RegisterRequest record created (Email, Password, FirstName, LastName, Phone?)
- [ ] LoginResponse record created (Token, UserInfo)
- [ ] UserInfo record created (Id, Email, FirstName, LastName, Role)
- [ ] LoginRequestValidator created with email and password validation
- [ ] RegisterRequestValidator created with strong password requirements
- [ ] All validator tests pass

**Files to Create**:
- `src/Abuvi.API/Features/Auth/AuthModels.cs`
- `src/Abuvi.API/Features/Auth/LoginRequestValidator.cs`
- `src/Abuvi.API/Features/Auth/RegisterRequestValidator.cs`
- `src/Abuvi.Tests/Unit/Features/Auth/AuthValidatorsTests.cs`

**Password Requirements** (RegisterRequestValidator):
- Not empty
- Minimum 8 characters
- Contains at least one uppercase letter
- Contains at least one lowercase letter
- Contains at least one number

**Validator Test Cases**:
1. `LoginRequestValidator_WithValidData_PassesValidation`
2. `LoginRequestValidator_WithInvalidEmail_FailsValidation`
3. `LoginRequestValidator_WithEmptyPassword_FailsValidation`
4. `RegisterRequestValidator_WithValidData_PassesValidation`
5. `RegisterRequestValidator_WithWeakPassword_FailsValidation`
6. `RegisterRequestValidator_WithInvalidEmail_FailsValidation`
7. `RegisterRequestValidator_WithLongFields_FailsValidation` (email max 255, names max 100, phone max 20)

**Implementation Steps**:
1. Create AuthModels.cs with C# records
2. ✅ Write validator tests first
3. Implement LoginRequestValidator
4. Implement RegisterRequestValidator
5. Verify tests pass: `dotnet test --filter "AuthValidatorsTests"`

**Verification**:
```bash
dotnet test --filter "AuthValidatorsTests"
# Expected: All 7 tests pass
```

**Estimated Time**: 1-2 hours

---

## Ticket 6: Auth Service Implementation (TDD)

**Story**: As a backend developer, I need an authentication service that handles login and registration logic so that users can authenticate and create accounts.

**Acceptance Criteria**:
- [ ] AuthService class created with LoginAsync and RegisterAsync methods
- [ ] LoginAsync validates credentials, checks IsActive, generates JWT token
- [ ] RegisterAsync checks for duplicate email, hashes password, creates user with Member role
- [ ] LoginAsync returns null for invalid credentials (not throwing exceptions)
- [ ] RegisterAsync throws InvalidOperationException for duplicate email
- [ ] All unit tests pass (TDD approach)
- [ ] Service registered in DI container

**Files to Create**:
- `src/Abuvi.API/Features/Auth/AuthService.cs`
- `src/Abuvi.Tests/Unit/Features/Auth/AuthServiceTests.cs`

**Dependencies**:
- IUsersRepository (existing)
- IPasswordHasher (Ticket 1)
- JwtTokenService (Ticket 3)

**TDD Test Cases** (Write FIRST):
1. `LoginAsync_WithValidCredentials_ReturnsLoginResponse` - Happy path
2. `LoginAsync_WithInvalidEmail_ReturnsNull` - User not found
3. `LoginAsync_WithInvalidPassword_ReturnsNull` - Wrong password
4. `LoginAsync_WithInactiveUser_ReturnsNull` - User.IsActive = false
5. `RegisterAsync_WithUniqueEmail_CreatesUserAndReturnsUserInfo` - Happy path
6. `RegisterAsync_WithExistingEmail_ThrowsInvalidOperationException` - Duplicate email
7. `RegisterAsync_CreatesUserWithMemberRole` - Verify default role
8. `RegisterAsync_HashesPassword` - Verify password is hashed

**Implementation Steps** (TDD):
1. ✅ Write failing tests first (using NSubstitute for mocks)
2. Create AuthService class
3. Implement LoginAsync method
4. Implement RegisterAsync method
5. Verify all tests pass: `dotnet test --filter "AuthServiceTests"`
6. Register in Program.cs: `builder.Services.AddScoped<AuthService>();`

**Business Rules**:
- New registrations always get Member role (not Admin or Board)
- Login fails if user is inactive (IsActive = false)
- Login returns null for invalid credentials (generic failure for security)
- Registration throws exception for duplicate email (business rule violation)

**Verification**:
```bash
dotnet test --filter "AuthServiceTests"
# Expected: All 8 tests pass
```

**Estimated Time**: 2-3 hours

---

## Ticket 7: Auth Endpoints Implementation (TDD)

**Story**: As a frontend developer, I need login and registration endpoints so that users can authenticate and create accounts via the API.

**Acceptance Criteria**:
- [ ] POST /api/auth/login endpoint created
- [ ] POST /api/auth/register endpoint created
- [ ] Both endpoints use Minimal API pattern
- [ ] Both endpoints use ApiResponse<T> wrapper
- [ ] Login returns 200 with token on success, 401 on failure
- [ ] Register returns 200 on success, 400 on duplicate email or validation failure
- [ ] All integration tests pass (TDD approach)
- [ ] Endpoints mapped in Program.cs

**Files to Create**:
- `src/Abuvi.API/Features/Auth/AuthEndpoints.cs`
- `src/Abuvi.Tests/Integration/Features/AuthIntegrationTests.cs`

**TDD Integration Test Cases** (Write FIRST):
1. `Register_WithValidData_Returns200AndCreatesUser` - Happy path
2. `Register_WithDuplicateEmail_Returns400` - EMAIL_EXISTS error
3. `Register_WithInvalidData_Returns400` - Validation failure
4. `Login_WithValidCredentials_Returns200AndToken` - Happy path
5. `Login_WithInvalidPassword_Returns401` - INVALID_CREDENTIALS error
6. `Login_WithInvalidEmail_Returns401` - INVALID_CREDENTIALS error
7. `Login_WithInactiveUser_Returns401` - User is inactive

**Implementation Steps** (TDD):
1. ✅ Write failing integration tests first
2. Create AuthEndpoints.cs with MapAuthEndpoints method
3. Implement Login endpoint handler
4. Implement Register endpoint handler
5. Add `app.MapAuthEndpoints();` in Program.cs (before MapUsersEndpoints)
6. Verify all integration tests pass: `dotnet test --filter "AuthIntegrationTests"`

**Endpoint Specifications**:

### POST /api/auth/login
**Request**:
```json
{
  "email": "user@example.com",
  "password": "Password123!"
}
```

**Success Response (200)**:
```json
{
  "success": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "user": {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "email": "user@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "role": "Member"
    }
  },
  "error": null
}
```

**Error Response (401)**:
```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "Invalid email or password",
    "code": "INVALID_CREDENTIALS"
  }
}
```

### POST /api/auth/register
**Request**:
```json
{
  "email": "newuser@example.com",
  "password": "Password123!",
  "firstName": "Jane",
  "lastName": "Doe",
  "phone": "555-1234"
}
```

**Success Response (200)**:
```json
{
  "success": true,
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "newuser@example.com",
    "firstName": "Jane",
    "lastName": "Doe",
    "role": "Member"
  },
  "error": null
}
```

**Error Response (400)**:
```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "Email already registered",
    "code": "EMAIL_EXISTS"
  }
}
```

**Verification**:
```bash
# Run integration tests
dotnet test --filter "AuthIntegrationTests"
# Expected: All 7 tests pass

# Manual test with curl
curl -X POST http://localhost:5079/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"TestPass123!","firstName":"Test","lastName":"User","phone":null}'
```

**Estimated Time**: 2-3 hours

---

## Ticket 8: Protect User Endpoints with Authorization (TDD)

**Story**: As a backend developer, I need to protect existing user endpoints with authorization so that only authenticated users with appropriate roles can access them.

**Acceptance Criteria**:
- [ ] GET /api/users requires Admin role
- [ ] GET /api/users/{id} requires authentication (any role)
- [ ] POST /api/users requires Admin role
- [ ] PUT /api/users/{id} requires authentication (any role)
- [ ] DELETE /api/users/{id} requires Admin role
- [ ] POST /api/auth/register remains public (no auth required)
- [ ] All authorization tests pass (TDD approach)

**Files to Modify**:
- `src/Abuvi.API/Features/Users/UsersEndpoints.cs`

**TDD Integration Test Cases** (Write FIRST):
Add to existing integration tests or create `ProtectedEndpointsTests.cs`:
1. `GetAllUsers_WithoutToken_Returns401` - Unauthorized
2. `GetAllUsers_WithMemberToken_Returns403` - Forbidden (not Admin)
3. `GetAllUsers_WithAdminToken_Returns200` - Success
4. `GetUserById_WithoutToken_Returns401` - Unauthorized
5. `GetUserById_WithValidToken_Returns200` - Success (any role)
6. `CreateUser_WithoutToken_Returns401` - Unauthorized
7. `CreateUser_WithMemberToken_Returns403` - Forbidden (not Admin)
8. `CreateUser_WithAdminToken_Returns201` - Success
9. `DeleteUser_WithMemberToken_Returns403` - Forbidden (not Admin)

**Implementation Steps** (TDD):
1. ✅ Write failing tests first (create helper method to get tokens for different roles)
2. Modify UsersEndpoints.cs to add RequireAuthorization
3. Verify all tests pass: `dotnet test --filter "ProtectedEndpointsTests"`

**Code Changes** (UsersEndpoints.cs):
```csharp
public static void MapUsersEndpoints(this WebApplication app)
{
    var group = app.MapGroup("/api/users")
        .WithTags("Users");

    // List all users - Admin only
    group.MapGet("/", GetAllUsers)
        .RequireAuthorization(policy => policy.RequireRole("Admin"))
        .WithName("GetAllUsers");

    // Get by ID - Any authenticated user
    group.MapGet("/{id:guid}", GetUserById)
        .RequireAuthorization()
        .WithName("GetUserById");

    // Create user - Admin only
    group.MapPost("/", CreateUser)
        .RequireAuthorization(policy => policy.RequireRole("Admin"))
        .WithName("CreateUser");

    // Update user - Any authenticated user
    group.MapPut("/{id:guid}", UpdateUser)
        .RequireAuthorization()
        .WithName("UpdateUser");

    // Delete user - Admin only
    group.MapDelete("/{id:guid}", DeleteUser)
        .RequireAuthorization(policy => policy.RequireRole("Admin"))
        .WithName("DeleteUser");
}
```

**Test Helper** (for integration tests):
```csharp
private async Task<string> GetAdminTokenAsync()
{
    // Create admin user via direct database access or seed data
    // Login and return token
}

private async Task<string> GetMemberTokenAsync()
{
    // Register new user (defaults to Member)
    // Login and return token
}
```

**Verification**:
```bash
# Run tests
dotnet test --filter "ProtectedEndpointsTests"
# Expected: All 9 tests pass

# Manual verification
# 1. Get token
curl -X POST http://localhost:5079/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"TestPass123!"}'

# 2. Use token (should succeed if authenticated)
curl -X GET http://localhost:5079/api/users/{id} \
  -H "Authorization: Bearer {token}"

# 3. Without token (should fail with 401)
curl -X GET http://localhost:5079/api/users
```

**Estimated Time**: 2-3 hours

---

## Ticket 9: Update UsersService to Use BCrypt Password Hashing (TDD)

**Story**: As a backend developer, I need to update the UsersService to use BCrypt password hashing so that all user passwords are securely hashed.

**Acceptance Criteria**:
- [ ] UsersService constructor injects IPasswordHasher
- [ ] CreateAsync method uses IPasswordHasher.HashPassword instead of SHA256
- [ ] Old HashPassword method removed from UsersService
- [ ] All existing UsersService tests updated and passing
- [ ] No plaintext passwords stored in database

**Files to Modify**:
- `src/Abuvi.API/Features/Users/UsersService.cs`
- `src/Abuvi.Tests/Unit/Features/UsersServiceTests.cs`

**TDD Test Updates** (Update FIRST):
1. Update `CreateAsync_WithValidData_CreatesUser` - Verify IPasswordHasher.HashPassword called
2. Update `CreateAsync_WithDuplicateEmail_ThrowsException` - Ensure still works with new hasher
3. Add `CreateAsync_HashesPasswordWithBCrypt` - Verify BCrypt hash format

**Implementation Steps** (TDD):
1. ✅ Update failing tests first
2. Modify UsersService constructor to inject IPasswordHasher:
```csharp
public class UsersService(IUsersRepository repository, IPasswordHasher passwordHasher)
{
    // Update CreateAsync
    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        if (await repository.EmailExistsAsync(request.Email, cancellationToken))
            throw new InvalidOperationException("A user with this email already exists");

        var user = new User
        {
            Email = request.Email,
            PasswordHash = passwordHasher.HashPassword(request.Password), // UPDATED
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            Role = request.Role,
            IsActive = true
        };

        var createdUser = await repository.CreateAsync(user, cancellationToken);
        return MapToResponse(createdUser);
    }

    // Remove old HashPassword method
}
```
3. Update Program.cs to inject IPasswordHasher into UsersService (already done if registered in DI)
4. Verify all tests pass: `dotnet test --filter "UsersServiceTests"`

**Database Migration Notes**:
⚠️ **IMPORTANT**: Existing users with SHA256 passwords will NOT work after this change.

**Options**:
1. **Reset database** (recommended for development):
```bash
dotnet ef database drop --project src/Abuvi.API --force
dotnet ef database update --project src/Abuvi.API
```

2. **Production**: Create migration script to rehash existing passwords (not recommended - require password reset instead)

**Verification**:
```bash
# Run tests
dotnet test --filter "UsersServiceTests"
# Expected: All tests pass

# Create new user and verify password is BCrypt hash (starts with $2)
curl -X POST http://localhost:5079/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"bcrypt@example.com","password":"TestPass123!","firstName":"Test","lastName":"User","phone":null}'

# Check database
# SELECT "PasswordHash" FROM "Users" WHERE "Email" = 'bcrypt@example.com';
# Should start with $2a$ or $2b$
```

**Estimated Time**: 1-2 hours

---

## Ticket 10: Comprehensive Testing & Documentation

**Story**: As a team member, I need comprehensive test coverage and documentation so that the authentication system is reliable and maintainable.

**Acceptance Criteria**:
- [ ] All unit tests pass (>=90% coverage for Auth feature)
- [ ] All integration tests pass
- [ ] Manual testing completed with Postman/curl
- [ ] All verification checklist items checked
- [ ] API endpoints documented in code (XML comments)
- [ ] Swagger/OpenAPI documentation verified
- [ ] Phase 2 marked as complete in project documentation

**Testing Checklist**:

### Unit Tests
- [ ] PasswordHasherTests: 5/5 passing
- [ ] JwtTokenServiceTests: 5/5 passing
- [ ] AuthServiceTests: 8/8 passing
- [ ] AuthValidatorsTests: 7/7 passing
- [ ] UsersServiceTests: All passing with BCrypt

### Integration Tests
- [ ] AuthIntegrationTests: 7/7 passing
- [ ] ProtectedEndpointsTests: 9/9 passing
- [ ] Existing integration tests still passing

### Manual Testing with Postman/curl
- [ ] POST /api/auth/register creates user successfully
- [ ] POST /api/auth/register with duplicate email returns 400
- [ ] POST /api/auth/register with weak password returns 400
- [ ] POST /api/auth/login with valid credentials returns token
- [ ] POST /api/auth/login with invalid credentials returns 401
- [ ] GET /api/users without token returns 401
- [ ] GET /api/users with Member token returns 403
- [ ] GET /api/users with Admin token returns 200
- [ ] GET /api/users/{id} with valid token returns 200
- [ ] JWT token can be decoded at jwt.io and contains correct claims

### Code Quality
- [ ] No compiler warnings
- [ ] Code passes `dotnet format`
- [ ] All async methods have CancellationToken parameter
- [ ] XML documentation comments on all public methods
- [ ] Follows Vertical Slice Architecture (Features/Auth/)
- [ ] Follows SOLID principles

### Security Verification
- [ ] JWT secret stored in user-secrets (NOT appsettings)
- [ ] Passwords hashed with BCrypt (work factor 12)
- [ ] No plaintext passwords in database
- [ ] Token expiry configured correctly
- [ ] CORS configured properly
- [ ] No sensitive data in logs

**Documentation Tasks**:
1. Update [README.md](../../README.md) to reflect Phase 2 completion
2. Create API documentation for frontend team:
   - Login endpoint specs
   - Register endpoint specs
   - JWT token structure
   - Error codes (INVALID_CREDENTIALS, EMAIL_EXISTS)
3. Update [base-standards.md](../specs/base-standards.md) if needed
4. Create [phase2-completion-report.md](./phase2-completion-report.md)

**Verification Commands**:
```bash
# Run all tests
dotnet test

# Check test coverage (if using Coverlet)
dotnet test /p:CollectCoverage=true /p:CoverageReportFormat=cobertura

# Format code
dotnet format

# Build in Release mode
dotnet build -c Release

# Run app and verify Swagger docs
dotnet run --project src/Abuvi.API
# Navigate to: http://localhost:5079/swagger
```

**Phase 2 Completion Report Template**:
```markdown
# Phase 2: Authentication Layer - Completion Report

**Date**: [Date]
**Completed By**: [Developer Name]

## Summary
JWT-based authentication system fully implemented and tested.

## Deliverables
- ✅ Password hashing service (BCrypt)
- ✅ JWT token generation service
- ✅ Authentication middleware
- ✅ Login endpoint
- ✅ Register endpoint
- ✅ Protected user endpoints
- ✅ Comprehensive test coverage (90%+)

## Test Results
- Unit Tests: [X/X passing]
- Integration Tests: [X/X passing]
- Manual Tests: All passing

## Known Issues
[None or list any known issues]

## Next Steps
- Proceed to Phase 3: Frontend Integration
- Implement refresh tokens (future enhancement)
- Add rate limiting (future enhancement)
```

**Estimated Time**: 2-3 hours

---

## Summary: Phase 2 Implementation Order

**Total Estimated Time**: 16-23 hours

1. ✅ **Ticket 1**: Password Hashing Service (1-2h) - TDD - **COMPLETED**
2. ✅ **Ticket 2**: JWT Configuration (0.5h) - **COMPLETED**
3. ✅ **Ticket 3**: JWT Token Service (2-3h) - TDD - **COMPLETED**
4. ✅ **Ticket 4**: Authentication Middleware (1h) - **COMPLETED**
5. **Ticket 5**: Auth Models & Validators (1-2h) - TDD
6. **Ticket 6**: Auth Service (2-3h) - TDD
7. **Ticket 7**: Auth Endpoints (2-3h) - TDD
8. **Ticket 8**: Protect Endpoints (2-3h) - TDD
9. **Ticket 9**: Update UsersService (1-2h) - TDD
10. **Ticket 10**: Testing & Documentation (2-3h)

## Development Workflow

**For Each Ticket**:
1. ✅ **Red**: Write failing tests first
2. ✅ **Green**: Implement minimum code to pass tests
3. ✅ **Refactor**: Clean up code while keeping tests green
4. ✅ **Verify**: Run all tests, check coverage
5. ✅ **Commit**: Create git commit with descriptive message

**Git Workflow**:
```bash
# Create feature branch
git checkout -b feature/phase2-authentication-backend

# For each ticket
git add .
git commit -m "feat(auth): [ticket name] - [brief description]"

# After all tickets complete
git push origin feature/phase2-authentication-backend
# Create PR to main branch
```

**PR Checklist**:
- [ ] All tests passing
- [ ] Code coverage >= 90%
- [ ] No merge conflicts
- [ ] Code follows standards
- [ ] Documentation updated

---

## Dependencies Between Tickets

```
Ticket 2 (JWT Config)
    ↓
Ticket 1 (Password Hasher) → Ticket 3 (JWT Service)
    ↓                              ↓
Ticket 5 (Models/Validators)      ↓
    ↓                              ↓
Ticket 6 (Auth Service) ←─────────┘
    ↓
Ticket 4 (Middleware)
    ↓
Ticket 7 (Auth Endpoints)
    ↓
Ticket 8 (Protect Endpoints) ← Ticket 9 (Update UsersService)
    ↓
Ticket 10 (Testing & Docs)
```

**Parallel Work Possible**:
- Ticket 1 and Ticket 2 can be done in parallel
- Ticket 1 and Ticket 3 can be done in parallel (but both needed for Ticket 6)
- Ticket 8 and Ticket 9 can be done in parallel

**Critical Path**: 1 → 6 → 7 → 8 → 10

---

## Success Criteria

Phase 2 is **DONE** when:
- ✅ All 10 tickets completed
- ✅ All tests passing (unit + integration)
- ✅ Test coverage >= 90% for Auth feature
- ✅ Manual testing successful
- ✅ Code follows architecture standards
- ✅ Documentation complete
- ✅ PR approved and merged
- ✅ Ready for Phase 3 (Frontend Integration)
