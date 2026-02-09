# Backend Implementation Plan: Phase1 - User Entity + Basic CRUD

## Overview

Implement the User entity with full CRUD operations following Vertical Slice Architecture. This establishes foundational patterns for all future features in the ABUVI application. The implementation validates the architecture before adding authentication complexity in Phase 2.

**Architecture Principles:**
- **Vertical Slice Architecture**: All user-related code lives in `src/Abuvi.API/Features/Users/`
- **Minimal APIs**: Lightweight HTTP endpoints using `MapGroup()` and `TypedResults`
- **Repository Pattern**: Data access abstraction within the feature slice
- **FluentValidation**: Declarative validation rules with automatic endpoint filtering
- **EF Core**: PostgreSQL database access with Fluent API configuration

## Architecture Context

### Feature Slice
- **Location**: `src/Abuvi.API/Features/Users/`
- **Files to Create**:
  - `UsersModels.cs` - Entity, DTOs, enums
  - `UsersRepository.cs` - Data access interface and implementation
  - `UsersService.cs` - Business logic layer
  - `CreateUserValidator.cs` - Validation for create operations
  - `UpdateUserValidator.cs` - Validation for update operations
  - `UsersEndpoints.cs` - Minimal API endpoint definitions

### Cross-Cutting Concerns
- **Shared Models**: `src/Abuvi.API/Common/Models/ApiResponse.cs`
- **Filters**: `src/Abuvi.API/Common/Filters/ValidationFilter.cs`
- **EF Core**: `src/Abuvi.API/Data/AbuviDbContext.cs` (modified)
- **Configuration**: `src/Abuvi.API/Data/Configurations/UserConfiguration.cs`
- **Startup**: `src/Abuvi.API/Program.cs` (modified)

### Tests
- **Unit Tests**: `src/Abuvi.Tests/Unit/Features/Users/`
  - `UsersServiceTests.cs`
  - `CreateUserValidatorTests.cs`
  - `UpdateUserValidatorTests.cs`
- **Integration Tests**: `src/Abuvi.Tests/Integration/Features/`
  - `UsersIntegrationTests.cs`

---

## Implementation Steps

### Step 0: Create Feature Branch

**Action**: Create and switch to feature branch for backend implementation

**Branch Naming**: `feature/phase1-user-crud-backend`

**Implementation Steps**:
1. Check current branch: `git branch`
2. Ensure on latest main: `git checkout main && git pull origin main`
3. Create feature branch: `git checkout -b feature/phase1-user-crud-backend`
4. Verify branch creation: `git branch` (should show * on new branch)

**Notes**:
- This MUST be the first step before any code changes
- Follow branch naming convention from `backend-standards.mdc`
- Keep backend work separate from potential frontend branches

---

### Step 1: Create Shared ApiResponse Models

**File**: `src/Abuvi.API/Common/Models/ApiResponse.cs`

**Action**: Create standardized API response wrapper for consistent responses

**Implementation Steps**:

1. **Create directory structure** (if not exists):
   ```bash
   mkdir -p src/Abuvi.API/Common/Models
   ```

2. **Create ApiResponse.cs** with the following content:

```csharp
namespace Abuvi.API.Common.Models;

/// <summary>
/// Standard API response wrapper for consistent response format
/// </summary>
public record ApiResponse<T>(bool Success, T? Data = default, ApiError? Error = null)
{
    /// <summary>
    /// Creates a successful response with data
    /// </summary>
    public static ApiResponse<T> Ok(T data) => new(true, data);

    /// <summary>
    /// Creates a not found error response
    /// </summary>
    public static ApiResponse<T> NotFound(string message) =>
        new(false, Error: new ApiError(message, "NOT_FOUND"));

    /// <summary>
    /// Creates a generic error response with custom code
    /// </summary>
    public static ApiResponse<T> Fail(string message, string code) =>
        new(false, Error: new ApiError(message, code));

    /// <summary>
    /// Creates a validation error response with field details
    /// </summary>
    public static ApiResponse<T> ValidationFail(string message, List<ValidationError> details) =>
        new(false, Error: new ApiError(message, "VALIDATION_ERROR", details));
}

/// <summary>
/// Error details in API response
/// </summary>
public record ApiError(
    string Message,
    string Code,
    List<ValidationError>? Details = null
);

/// <summary>
/// Individual validation error for a specific field
/// </summary>
public record ValidationError(
    string Field,
    string Message
);
```

**Dependencies**: None (core C# records)

**Implementation Notes**:
- Uses C# records for immutability
- Static factory methods provide clean API surface
- Supports both simple errors and detailed validation errors
- Generic type parameter allows type-safe data responses

---

### Step 2: Create Validation Filter

**File**: `src/Abuvi.API/Common/Filters/ValidationFilter.cs`

**Action**: Create endpoint filter for automatic FluentValidation integration

**Implementation Steps**:

1. **Create directory structure** (if not exists):
   ```bash
   mkdir -p src/Abuvi.API/Common/Filters
   ```

2. **Create ValidationFilter.cs**:

```csharp
using Abuvi.API.Common.Models;
using FluentValidation;

namespace Abuvi.API.Common.Filters;

/// <summary>
/// Endpoint filter that automatically validates request DTOs using FluentValidation
/// </summary>
public class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        // Get validator from DI container
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator is null)
            return await next(context);

        // Extract request from endpoint arguments
        var request = context.Arguments.OfType<T>().FirstOrDefault();
        if (request is null)
            return await next(context);

        // Validate request
        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        if (!result.IsValid)
        {
            // Convert FluentValidation errors to our format
            var errors = result.Errors
                .Select(e => new ValidationError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return Results.BadRequest(
                ApiResponse<object>.ValidationFail("Validation failed", errors)
            );
        }

        return await next(context);
    }
}
```

**Dependencies**:
- `FluentValidation` NuGet package (should already be in project)
- `Abuvi.API.Common.Models` namespace

**Implementation Notes**:
- Integrates with ASP.NET Core endpoint filter pipeline
- Automatically extracts validator from DI container
- Returns structured validation errors
- Uses `CancellationToken` from HttpContext for proper cancellation support

---

### Step 3: Create User Entity and DTOs

**File**: `src/Abuvi.API/Features/Users/UsersModels.cs`

**Action**: Define User entity, UserRole enum, and all request/response DTOs

**Implementation Steps**:

1. **Create directory structure**:
   ```bash
   mkdir -p src/Abuvi.API/Features/Users
   ```

2. **Create UsersModels.cs**:

```csharp
namespace Abuvi.API.Features.Users;

/// <summary>
/// User entity representing a platform account
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public UserRole Role { get; set; }
    public Guid? FamilyUnitId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// User roles in the system
/// </summary>
public enum UserRole
{
    Admin,
    Board,
    Member
}

/// <summary>
/// Request to create a new user
/// </summary>
public record CreateUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone,
    UserRole Role
);

/// <summary>
/// Request to update an existing user
/// </summary>
public record UpdateUserRequest(
    string FirstName,
    string LastName,
    string? Phone,
    bool IsActive
);

/// <summary>
/// User response DTO
/// </summary>
public record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

**Dependencies**: None (core .NET types)

**Implementation Notes**:
- Entity uses class for EF Core tracking
- DTOs use records for immutability
- Email, FirstName, LastName default to empty string (not null) for required fields
- Phone is nullable (optional field)
- IsActive defaults to true
- Password field only in CreateRequest (never in response)
- FamilyUnitId is nullable (foreign key will be added when FamilyUnit entity exists)

---

### Step 4: Create EF Core Entity Configuration

**File**: `src/Abuvi.API/Data/Configurations/UserConfiguration.cs`

**Action**: Configure User entity using Fluent API

**Implementation Steps**:

1. **Create directory structure** (if not exists):
   ```bash
   mkdir -p src/Abuvi.API/Data/Configurations
   ```

2. **Create UserConfiguration.cs**:

```csharp
using Abuvi.API.Features.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

/// <summary>
/// EF Core configuration for User entity
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Table name (snake_case for PostgreSQL convention)
        builder.ToTable("users");

        // Primary key
        builder.HasKey(u => u.Id);

        // Email: required, max 255, unique index
        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255)
            .HasColumnName("email");

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email");

        // Password hash: required
        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasColumnName("password_hash");

        // First name: required, max 100
        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("first_name");

        // Last name: required, max 100
        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("last_name");

        // Phone: optional, max 20
        builder.Property(u => u.Phone)
            .HasMaxLength(20)
            .HasColumnName("phone");

        // Role: stored as string, max 20
        builder.Property(u => u.Role)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("role");

        // Family unit FK: nullable (will be enforced when FamilyUnit entity exists)
        builder.Property(u => u.FamilyUnitId)
            .HasColumnName("family_unit_id");

        // IsActive: required, default true
        builder.Property(u => u.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");

        // Timestamps: required, default NOW()
        builder.Property(u => u.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(u => u.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");
    }
}
```

**Dependencies**:
- `Microsoft.EntityFrameworkCore`
- `Abuvi.API.Features.Users` namespace

**Implementation Notes**:
- Uses snake_case column names for PostgreSQL convention
- Role stored as string (not integer) for readability and flexibility
- Unique index on email for constraint enforcement
- Default values for IsActive and timestamps at database level
- FamilyUnitId nullable without FK constraint (will be added in future phase)

---

### Step 5: Update DbContext

**File**: `src/Abuvi.API/Data/AbuviDbContext.cs`

**Action**: Add Users DbSet and ensure configuration is applied

**Implementation Steps**:

1. **Read existing AbuviDbContext.cs**:
   - Check if file exists and read current content
   - Identify where to add Users DbSet

2. **Add Users DbSet**:
   - Add: `public DbSet<User> Users => Set<User>();`
   - Ensure `OnModelCreating` applies configurations from assembly

3. **Expected structure**:

```csharp
using Abuvi.API.Features.Users;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Data;

public class AbuviDbContext(DbContextOptions<AbuviDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // This applies all IEntityTypeConfiguration implementations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AbuviDbContext).Assembly);
    }
}
```

**Dependencies**:
- `Microsoft.EntityFrameworkCore`
- `Abuvi.API.Features.Users` namespace

**Implementation Notes**:
- Use expression-bodied property for DbSet
- `ApplyConfigurationsFromAssembly` automatically discovers and applies UserConfiguration
- Primary constructor syntax for DbContext options

---

### Step 6: Create Users Repository

**File**: `src/Abuvi.API/Features/Users/UsersRepository.cs`

**Action**: Implement data access layer with interface and implementation

**Implementation Steps**:

1. **Create UsersRepository.cs** in `src/Abuvi.API/Features/Users/`:

```csharp
using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Users;

/// <summary>
/// Repository interface for User data access
/// </summary>
public interface IUsersRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<List<User>> GetAllAsync(CancellationToken ct);
    Task<User> AddAsync(User user, CancellationToken ct);
    Task UpdateAsync(User user, CancellationToken ct);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct);
    Task<bool> EmailExistsAsync(string email, Guid excludeUserId, CancellationToken ct);
}

/// <summary>
/// Repository implementation for User data access
/// </summary>
public class UsersRepository(AbuviDbContext db) : IUsersRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct)
    {
        return await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), ct);
    }

    public async Task<List<User>> GetAllAsync(CancellationToken ct)
    {
        return await db.Users
            .AsNoTracking()
            .OrderBy(u => u.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<User> AddAsync(User user, CancellationToken ct)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task UpdateAsync(User user, CancellationToken ct)
    {
        user.UpdatedAt = DateTime.UtcNow;
        db.Users.Update(user);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct)
    {
        return await db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email.ToLower() == email.ToLower(), ct);
    }

    public async Task<bool> EmailExistsAsync(string email, Guid excludeUserId, CancellationToken ct)
    {
        return await db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email.ToLower() == email.ToLower() && u.Id != excludeUserId, ct);
    }
}
```

**Function Signatures**:
- `GetByIdAsync(Guid id, CancellationToken ct)` - Returns user by ID or null
- `GetByEmailAsync(string email, CancellationToken ct)` - Returns user by email (case-insensitive) or null
- `GetAllAsync(CancellationToken ct)` - Returns all users ordered by creation date
- `AddAsync(User user, CancellationToken ct)` - Adds new user and returns it
- `UpdateAsync(User user, CancellationToken ct)` - Updates existing user
- `EmailExistsAsync(string email, CancellationToken ct)` - Checks if email exists (for create)
- `EmailExistsAsync(string email, Guid excludeUserId, CancellationToken ct)` - Checks if email exists excluding specific user (for update)

**Dependencies**:
- `Microsoft.EntityFrameworkCore`
- `Abuvi.API.Data` namespace

**Implementation Notes**:
- Use `AsNoTracking()` for read operations (performance optimization)
- Case-insensitive email comparison using `ToLower()`
- Repository handles `SaveChangesAsync` (transaction boundary)
- `UpdatedAt` explicitly set in `UpdateAsync`
- Two overloads of `EmailExistsAsync` for create vs update scenarios

---

### Step 7: Create Users Service

**File**: `src/Abuvi.API/Features/Users/UsersService.cs`

**Action**: Implement business logic layer

**Implementation Steps**:

1. **Create UsersService.cs** in `src/Abuvi.API/Features/Users/`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace Abuvi.API.Features.Users;

/// <summary>
/// Service for User business logic
/// </summary>
public class UsersService(IUsersRepository repository)
{
    public async Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var user = await repository.GetByIdAsync(id, ct);
        return user?.ToResponse();
    }

    public async Task<UserResponse?> GetByEmailAsync(string email, CancellationToken ct)
    {
        var user = await repository.GetByEmailAsync(email, ct);
        return user?.ToResponse();
    }

    public async Task<List<UserResponse>> GetAllAsync(CancellationToken ct)
    {
        var users = await repository.GetAllAsync(ct);
        return users.Select(u => u.ToResponse()).ToList();
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        // Check email uniqueness
        if (await repository.EmailExistsAsync(request.Email, ct))
        {
            throw new InvalidOperationException($"User with email '{request.Email}' already exists");
        }

        // Create user entity
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.Trim().ToLower(),
            PasswordHash = HashPasswordPlaceholder(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Phone = request.Phone?.Trim(),
            Role = request.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(user, ct);
        return user.ToResponse();
    }

    public async Task<UserResponse?> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await repository.GetByIdAsync(id, ct);
        if (user == null)
        {
            return null;
        }

        // Update fields
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Phone = request.Phone?.Trim();
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(user, ct);
        return user.ToResponse();
    }

    /// <summary>
    /// Placeholder password hashing using SHA-256.
    /// Will be replaced with BCrypt in Phase 2 (Authentication).
    /// </summary>
    private static string HashPasswordPlaceholder(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}

/// <summary>
/// Extension methods for mapping User entity to UserResponse
/// </summary>
public static class UserMappingExtensions
{
    public static UserResponse ToResponse(this User user)
    {
        return new UserResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Phone,
            user.Role,
            user.IsActive,
            user.CreatedAt,
            user.UpdatedAt
        );
    }
}
```

**Function Signatures**:
- `GetByIdAsync(Guid id, CancellationToken ct)` - Returns UserResponse or null
- `GetByEmailAsync(string email, CancellationToken ct)` - Returns UserResponse or null
- `GetAllAsync(CancellationToken ct)` - Returns list of UserResponse
- `CreateAsync(CreateUserRequest request, CancellationToken ct)` - Creates user, returns UserResponse
- `UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct)` - Updates user, returns UserResponse or null

**Dependencies**:
- `System.Security.Cryptography` (for SHA-256)
- `Abuvi.API.Features.Users` namespace

**Implementation Notes**:
- Email normalized to lowercase and trimmed
- All string fields trimmed to prevent whitespace issues
- Password hashed with SHA-256 (placeholder for Phase 2 BCrypt)
- Duplicate email check throws `InvalidOperationException`
- Returns null for not-found scenarios (handled by endpoints)
- Timestamps set in UTC
- Extension method for entity-to-DTO mapping

---

### Step 8: Create FluentValidation Validators

**Files**:
- `src/Abuvi.API/Features/Users/CreateUserValidator.cs`
- `src/Abuvi.API/Features/Users/UpdateUserValidator.cs`

**Action**: Create validation rules for request DTOs

**Implementation Steps**:

1. **Create CreateUserValidator.cs**:

```csharp
using FluentValidation;

namespace Abuvi.API.Features.Users;

/// <summary>
/// Validator for CreateUserRequest
/// </summary>
public class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Email must be a valid email address")
            .MaximumLength(255)
            .WithMessage("Email must not exceed 255 characters");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters long");

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("First name is required")
            .MaximumLength(100)
            .WithMessage("First name must not exceed 100 characters")
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("First name cannot be only whitespace");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Last name is required")
            .MaximumLength(100)
            .WithMessage("Last name must not exceed 100 characters")
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Last name cannot be only whitespace");

        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .WithMessage("Phone must not exceed 20 characters")
            .Matches(@"^\+?[0-9\s\-()]+$")
            .WithMessage("Phone must contain only valid phone characters")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.Role)
            .IsInEnum()
            .WithMessage("Role must be a valid value (Admin, Board, or Member)");
    }
}
```

2. **Create UpdateUserValidator.cs**:

```csharp
using FluentValidation;

namespace Abuvi.API.Features.Users;

/// <summary>
/// Validator for UpdateUserRequest
/// </summary>
public class UpdateUserValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("First name is required")
            .MaximumLength(100)
            .WithMessage("First name must not exceed 100 characters")
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("First name cannot be only whitespace");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Last name is required")
            .MaximumLength(100)
            .WithMessage("Last name must not exceed 100 characters")
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Last name cannot be only whitespace");

        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .WithMessage("Phone must not exceed 20 characters")
            .Matches(@"^\+?[0-9\s\-()]+$")
            .WithMessage("Phone must contain only valid phone characters")
            .When(x => !string.IsNullOrEmpty(x.Phone));
    }
}
```

**Dependencies**:
- `FluentValidation` NuGet package

**Implementation Notes**:
- Clear, descriptive error messages
- Email format validation
- Password minimum length (8 characters)
- Whitespace-only validation for names
- Phone regex accepts international formats
- Role enum validation
- Phone validation only when provided (optional field)

---

### Step 9: Create Minimal API Endpoints

**File**: `src/Abuvi.API/Features/Users/UsersEndpoints.cs`

**Action**: Define HTTP endpoints with routing and status codes

**Implementation Steps**:

1. **Create UsersEndpoints.cs**:

```csharp
using Abuvi.API.Common.Filters;
using Abuvi.API.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace Abuvi.API.Features.Users;

/// <summary>
/// Minimal API endpoints for User operations
/// </summary>
public static class UsersEndpoints
{
    public static void MapUsersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .WithOpenApi();

        group.MapGet("/", GetAllUsers)
            .WithName("GetAllUsers")
            .Produces<ApiResponse<List<UserResponse>>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", GetUserById)
            .WithName("GetUserById")
            .Produces<ApiResponse<UserResponse>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateUser)
            .WithName("CreateUser")
            .AddEndpointFilter<ValidationFilter<CreateUserRequest>>()
            .Produces<ApiResponse<UserResponse>>(StatusCodes.Status201Created)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", UpdateUser)
            .WithName("UpdateUser")
            .AddEndpointFilter<ValidationFilter<UpdateUserRequest>>()
            .Produces<ApiResponse<UserResponse>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetAllUsers(
        UsersService service,
        CancellationToken ct)
    {
        var users = await service.GetAllAsync(ct);
        return Results.Ok(ApiResponse<List<UserResponse>>.Ok(users));
    }

    private static async Task<IResult> GetUserById(
        [FromRoute] Guid id,
        UsersService service,
        CancellationToken ct)
    {
        var user = await service.GetByIdAsync(id, ct);

        return user is not null
            ? Results.Ok(ApiResponse<UserResponse>.Ok(user))
            : Results.NotFound(ApiResponse<UserResponse>.NotFound("User not found"));
    }

    private static async Task<IResult> CreateUser(
        [FromBody] CreateUserRequest request,
        UsersService service,
        CancellationToken ct)
    {
        try
        {
            var user = await service.CreateAsync(request, ct);
            return Results.Created(
                $"/api/users/{user.Id}",
                ApiResponse<UserResponse>.Ok(user)
            );
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            return Results.Conflict(ApiResponse<UserResponse>.Fail(ex.Message, "USER_ALREADY_EXISTS"));
        }
    }

    private static async Task<IResult> UpdateUser(
        [FromRoute] Guid id,
        [FromBody] UpdateUserRequest request,
        UsersService service,
        CancellationToken ct)
    {
        var user = await service.UpdateAsync(id, request, ct);

        return user is not null
            ? Results.Ok(ApiResponse<UserResponse>.Ok(user))
            : Results.NotFound(ApiResponse<UserResponse>.NotFound("User not found"));
    }
}
```

**Function Signatures**:
- `GetAllUsers(UsersService service, CancellationToken ct)` - Returns 200 with list
- `GetUserById(Guid id, UsersService service, CancellationToken ct)` - Returns 200 or 404
- `CreateUser(CreateUserRequest request, UsersService service, CancellationToken ct)` - Returns 201, 400, or 409
- `UpdateUser(Guid id, UpdateUserRequest request, UsersService service, CancellationToken ct)` - Returns 200, 400, or 404

**Dependencies**:
- `Abuvi.API.Common.Models`
- `Abuvi.API.Common.Filters`

**Implementation Notes**:
- Uses `MapGroup()` for /api/users prefix
- `WithTags("Users")` for Swagger grouping
- `WithOpenApi()` enables OpenAPI documentation
- Validation filters applied to POST and PUT
- `[FromRoute]` and `[FromBody]` attributes for clarity
- Returns appropriate HTTP status codes
- Location header in 201 Created responses
- Catches duplicate email exception for 409 Conflict

---

### Step 10: Register Services and Endpoints

**File**: `src/Abuvi.API/Program.cs`

**Action**: Register DI services and map endpoints

**Implementation Steps**:

1. **Read existing Program.cs** to understand structure

2. **Add FluentValidation registration** (if not already present):
   - Location: Before `var app = builder.Build();`
   - Code: `builder.Services.AddValidatorsFromAssemblyContaining<Program>();`

3. **Add User feature services**:
   - Location: Before `var app = builder.Build();`
   - Code:
```csharp
// User feature services
builder.Services.AddScoped<IUsersRepository, UsersRepository>();
builder.Services.AddScoped<UsersService>();
```

4. **Map endpoints**:
   - Location: After middleware pipeline, before `app.Run();`
   - Code: `app.MapUsersEndpoints();`

**Expected additions**:

```csharp
// In builder configuration section
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// User feature services
builder.Services.AddScoped<IUsersRepository, UsersRepository>();
builder.Services.AddScoped<UsersService>();

// After var app = builder.Build() and middleware configuration
app.MapUsersEndpoints();
```

**Dependencies**:
- `FluentValidation.DependencyInjectionExtensions`
- `Abuvi.API.Features.Users` namespace

**Implementation Notes**:
- Use `AddScoped` for per-request lifetime
- Register interface and implementation separately
- Service is registered directly (not interface-based)
- Validators discovered automatically by assembly scanning

---

### Step 11: Create and Apply EF Core Migration

**Action**: Generate and apply database migration for User entity

**Implementation Steps**:

1. **Verify EF Core tools installed**:
   ```bash
   dotnet tool list --global
   ```
   - If `dotnet-ef` not listed: `dotnet tool install --global dotnet-ef`

2. **Create migration**:
   ```bash
   dotnet ef migrations add AddUserEntity --project src/Abuvi.API
   ```

3. **Review generated migration**:
   - Location: `src/Abuvi.API/Data/Migrations/[timestamp]_AddUserEntity.cs`
   - Verify:
     - Table name is `users`
     - All columns present with correct types
     - Column names use snake_case
     - Unique index on email
     - Default values for is_active, created_at, updated_at
     - Role stored as string (varchar), not integer

4. **Apply migration**:
   ```bash
   dotnet ef database update --project src/Abuvi.API
   ```

5. **Verify table created in PostgreSQL**:
   ```bash
   docker exec -it abuvi-postgres psql -U abuvi_user -d abuvi -c "\d users"
   ```

**Expected output**:
```
                                       Table "public.users"
     Column      |            Type             | Collation | Nullable |      Default
-----------------+-----------------------------+-----------+----------+-------------------
 id              | uuid                        |           | not null |
 email           | character varying(255)      |           | not null |
 password_hash   | text                        |           | not null |
 first_name      | character varying(100)      |           | not null |
 last_name       | character varying(100)      |           | not null |
 phone           | character varying(20)       |           |          |
 role            | character varying(20)       |           | not null |
 family_unit_id  | uuid                        |           |          |
 is_active       | boolean                     |           | not null | true
 created_at      | timestamp without time zone |           | not null | now()
 updated_at      | timestamp without time zone |           | not null | now()
Indexes:
    "PK_users" PRIMARY KEY, btree (id)
    "IX_Users_Email" UNIQUE, btree (email)
```

**Dependencies**:
- `dotnet-ef` global tool
- PostgreSQL Docker container running
- Connection string in appsettings or user-secrets

**Implementation Notes**:
- Migration name is descriptive
- Review migration before applying
- Keep migrations in version control
- Test rollback: `dotnet ef database update [previous-migration]`

---

### Step 12: Write Unit Tests

**Files**:
- `src/Abuvi.Tests/Unit/Features/Users/UsersServiceTests.cs`
- `src/Abuvi.Tests/Unit/Features/Users/CreateUserValidatorTests.cs`
- `src/Abuvi.Tests/Unit/Features/Users/UpdateUserValidatorTests.cs`

**Action**: Create comprehensive unit tests for service and validators

**Implementation Steps**:

1. **Create directory structure**:
   ```bash
   mkdir -p src/Abuvi.Tests/Unit/Features/Users
   ```

2. **Create UsersServiceTests.cs** with test cases:
   - `CreateAsync_WithValidData_ReturnsCreatedUser`
   - `CreateAsync_WithDuplicateEmail_ThrowsException`
   - `CreateAsync_NormalizesEmail_ToLowerCase`
   - `CreateAsync_TrimsWhitespace_FromFields`
   - `CreateAsync_HashesPassword_UsingSHA256`
   - `GetByIdAsync_WithExistingUser_ReturnsUser`
   - `GetByIdAsync_WithNonExistentUser_ReturnsNull`
   - `UpdateAsync_WithValidData_UpdatesUser`
   - `UpdateAsync_WithNonExistentUser_ReturnsNull`
   - `GetAllAsync_ReturnsAllUsers`

3. **Create CreateUserValidatorTests.cs** with test cases:
   - `Validate_WithValidData_Passes`
   - `Validate_WithEmptyEmail_Fails`
   - `Validate_WithInvalidEmailFormat_Fails`
   - `Validate_WithShortPassword_Fails`
   - `Validate_WithEmptyFirstName_Fails`
   - `Validate_WithWhitespaceOnlyFirstName_Fails`
   - `Validate_WithTooLongPhone_Fails`
   - `Validate_WithInvalidPhoneCharacters_Fails`
   - `Validate_WithInvalidRole_Fails`
   - `Validate_WithNullPhone_Passes`
   - `Validate_WithValidPhoneFormats_Passes` (Theory with multiple formats)

4. **Create UpdateUserValidatorTests.cs** with similar test cases for UpdateUserRequest

**Dependencies**:
- `xUnit`
- `FluentAssertions`
- `NSubstitute`

**Implementation Notes**:
- Use AAA pattern (Arrange-Act-Assert)
- Mock IUsersRepository with NSubstitute
- Test naming: `MethodName_StateUnderTest_ExpectedBehavior`
- Cover happy path, validation errors, edge cases
- Use `[Theory]` for parameterized tests
- See enriched spec for complete test implementations

---

### Step 13: Write Integration Tests

**File**: `src/Abuvi.Tests/Integration/Features/UsersIntegrationTests.cs`

**Action**: Create integration tests for full HTTP pipeline

**Implementation Steps**:

1. **Create directory structure**:
   ```bash
   mkdir -p src/Abuvi.Tests/Integration/Features
   ```

2. **Create UsersIntegrationTests.cs** with test cases:
   - `GetAllUsers_ReturnsEmptyList_WhenNoUsers`
   - `CreateUser_ReturnsCreated_WithValidData`
   - `CreateUser_ReturnsBadRequest_WithInvalidEmail`
   - `CreateUser_ReturnsBadRequest_WithShortPassword`
   - `CreateUser_ReturnsConflict_WithDuplicateEmail`
   - `GetUserById_ReturnsUser_WhenExists`
   - `GetUserById_ReturnsNotFound_WhenNotExists`
   - `UpdateUser_ReturnsUpdated_WithValidData`
   - `UpdateUser_ReturnsNotFound_WhenNotExists`
   - `CreateUser_NormalizesEmailToLowerCase`
   - `CreateUser_WithAllRoles_Succeeds`

**Dependencies**:
- `Microsoft.AspNetCore.Mvc.Testing`
- `xUnit`
- `FluentAssertions`

**Implementation Notes**:
- Use `WebApplicationFactory<Program>`
- Use unique emails per test (Guid-based)
- Test actual HTTP status codes and response format
- Verify ApiResponse wrapper structure
- Clean up test data in `DisposeAsync` if needed
- Use `IClassFixture` for test isolation
- See enriched spec for complete implementations

---

### Step 14: Update Technical Documentation

**Action**: Update project documentation to reflect User entity implementation

**Implementation Steps**:

1. **Review changes made**:
   - New User entity with full CRUD
   - New API endpoints: GET, POST, PUT for /api/users
   - New database table: users
   - New shared models: ApiResponse, ValidationFilter

2. **Identify documentation files to update**:
   - `ai-specs/specs/data-model.md` - Already has User entity, verify accuracy
   - Auto-generated OpenAPI docs via Swagger - No manual update needed
   - Consider creating API usage examples

3. **Update data-model.md** if needed:
   - Verify User entity specification matches implementation
   - Confirm field types, validation rules, relationships
   - Update if any changes were made during implementation

4. **Verify Swagger documentation**:
   - Run application: `dotnet run --project src/Abuvi.API`
   - Visit: http://localhost:5079/swagger
   - Confirm Users endpoints appear with correct schemas
   - Test endpoints via Swagger UI

5. **Document any deviations**:
   - If implementation differs from spec, document why
   - Update enriched spec or create addendum

**References**:
- `ai-specs/specs/documentation-standards.mdc`
- All documentation in English
- Maintain consistency with existing structure

**Notes**:
- This is MANDATORY before considering implementation complete
- Swagger provides auto-documentation for endpoints
- Manual documentation updates should be minimal

---

## Implementation Order

Execute steps in this exact sequence:

1. **Step 0**: Create Feature Branch (`feature/phase1-user-crud-backend`)
2. **Step 1**: Create Shared ApiResponse Models
3. **Step 2**: Create Validation Filter
4. **Step 3**: Create User Entity and DTOs
5. **Step 4**: Create EF Core Entity Configuration
6. **Step 5**: Update DbContext
7. **Step 6**: Create Users Repository
8. **Step 7**: Create Users Service
9. **Step 8**: Create FluentValidation Validators
10. **Step 9**: Create Minimal API Endpoints
11. **Step 10**: Register Services and Endpoints in Program.cs
12. **Step 11**: Create and Apply EF Core Migration
13. **Step 12**: Write Unit Tests
14. **Step 13**: Write Integration Tests
15. **Step 14**: Update Technical Documentation

**Why this order?**
- Shared models first (ApiResponse, ValidationFilter)
- Domain model next (entity, DTOs, validators)
- Data access layer (EF Core config, repository)
- Business logic layer (service)
- Presentation layer (endpoints)
- Infrastructure (DI registration, migrations)
- Testing (unit, then integration)
- Documentation last (reflects actual implementation)

---

## Testing Checklist

After implementation, verify:

### Unit Tests
- [ ] UsersServiceTests: All 10 test cases pass
- [ ] CreateUserValidatorTests: All 11 test cases pass
- [ ] UpdateUserValidatorTests: All validator test cases pass
- [ ] Test coverage >= 90% for Users feature
- [ ] All tests use AAA pattern
- [ ] Test names follow convention: `MethodName_StateUnderTest_ExpectedBehavior`
- [ ] NSubstitute mocks used correctly
- [ ] No hard-coded values (use Guid.NewGuid() for IDs)

### Integration Tests
- [ ] UsersIntegrationTests: All 11 test cases pass
- [ ] Tests use unique emails (no conflicts)
- [ ] HTTP status codes verified (200, 201, 400, 404, 409)
- [ ] ApiResponse wrapper format verified
- [ ] Location header present in 201 Created
- [ ] Validation errors return correct structure
- [ ] Tests are isolated (no dependencies between tests)

### Manual Testing (Swagger)
- [ ] GET /api/users returns empty array initially (200 OK)
- [ ] POST /api/users creates user (201 Created)
- [ ] POST with invalid email returns 400 with validation errors
- [ ] POST with duplicate email returns 409 Conflict
- [ ] GET /api/users/{id} returns user (200 OK)
- [ ] GET /api/users/{id} with non-existent ID returns 404
- [ ] PUT /api/users/{id} updates user (200 OK)
- [ ] PUT with non-existent ID returns 404

### Run All Tests
```bash
# Run all tests
dotnet test

# Run only Users feature tests
dotnet test --filter FullyQualifiedName~Users

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## Error Response Format

All API errors follow this structure:

### Validation Error (400 Bad Request)
```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "Validation failed",
    "code": "VALIDATION_ERROR",
    "details": [
      {
        "field": "Email",
        "message": "Email must be a valid email address"
      },
      {
        "field": "Password",
        "message": "Password must be at least 8 characters long"
      }
    ]
  }
}
```

### Not Found (404)
```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "User not found",
    "code": "NOT_FOUND",
    "details": null
  }
}
```

### Conflict (409)
```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "User with email 'test@example.com' already exists",
    "code": "USER_ALREADY_EXISTS",
    "details": null
  }
}
```

### Success Response (200/201)
```json
{
  "success": true,
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "john.doe@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "phone": "+34 123 456 789",
    "role": "Member",
    "isActive": true,
    "createdAt": "2026-02-07T10:00:00Z",
    "updatedAt": "2026-02-07T10:00:00Z"
  },
  "error": null
}
```

---

## Dependencies

### NuGet Packages Required
- `Microsoft.EntityFrameworkCore` (should already be in project)
- `Microsoft.EntityFrameworkCore.Design` (for migrations)
- `Npgsql.EntityFrameworkCore.PostgreSQL` (PostgreSQL provider)
- `FluentValidation.DependencyInjectionExtensions`

### Global Tools Required
- `dotnet-ef` (for migrations): `dotnet tool install --global dotnet-ef`

### Test Dependencies
- `xUnit`
- `FluentAssertions`
- `NSubstitute`
- `Microsoft.AspNetCore.Mvc.Testing`

### Verify Dependencies
```bash
# Check project references
dotnet list src/Abuvi.API/Abuvi.API.csproj package

# Check global tools
dotnet tool list --global
```

---

## Notes

### Important Reminders

1. **Password Hashing**:
   - SHA-256 is a PLACEHOLDER only
   - NOT suitable for production
   - Will be replaced with BCrypt in Phase 2
   - Do NOT use in real authentication

2. **Email Handling**:
   - Always normalized to lowercase
   - Unique constraint enforced at database level
   - Case-insensitive comparison in queries

3. **Timestamps**:
   - Always use UTC (DateTime.UtcNow)
   - Database defaults ensure values even if code forgets
   - UpdatedAt refreshed on every update

4. **Validation**:
   - FluentValidation runs before service layer
   - ValidationFilter returns 400 automatically
   - Service layer should not re-validate

5. **Transaction Boundaries**:
   - Repository calls SaveChangesAsync
   - Each repository operation is atomic
   - No explicit transactions needed in Phase 1

6. **Testing**:
   - Target 90% code coverage
   - Use unique emails in integration tests
   - Mock repository in unit tests
   - Follow AAA pattern consistently

7. **RGPD Compliance**:
   - User data is personal data
   - Audit trail via timestamps
   - Consider data retention policies

### Business Rules

- Email must be unique (case-insensitive)
- Password minimum 8 characters
- FirstName and LastName cannot be whitespace only
- Phone is optional but validated if provided
- Role must be valid enum value (Admin, Board, Member)
- IsActive defaults to true
- FamilyUnitId is nullable (no FK constraint yet)

### Performance Considerations

- AsNoTracking() for read operations
- Email index for fast uniqueness checks
- Target <200ms for GET, <500ms for POST/PUT
- No pagination in Phase 1 (add when users >100)

### Security Considerations

- Input validation via FluentValidation
- SQL injection prevented by EF Core parameterized queries
- Passwords never stored in plain text
- Email normalization prevents bypass attempts

---

## Next Steps After Implementation

After completing all steps and verification:

1. **Commit changes**:
   ```bash
   git add .
   git commit -m "feat: implement User entity with CRUD operations

   - Add User entity with EF Core configuration
   - Implement repository and service layers
   - Create Minimal API endpoints
   - Add FluentValidation validators
   - Create shared ApiResponse and ValidationFilter
   - Add comprehensive unit and integration tests
   - Apply database migration

   Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
   ```

2. **Push to remote**:
   ```bash
   git push -u origin feature/phase1-user-crud-backend
   ```

3. **Create pull request**:
   - Target branch: `main`
   - Title: "Phase 1: User Entity + Basic CRUD (Backend)"
   - Description: Reference `ai-specs/changes/phase1_user_entity_crud_enriched.md`
   - Reviewers: Assign appropriate team members

4. **Demo endpoints**:
   - Start application: `dotnet run --project src/Abuvi.API`
   - Open Swagger: http://localhost:5079/swagger
   - Test all endpoints via Swagger UI
   - Demonstrate CRUD operations

5. **Review patterns**:
   - Evaluate Vertical Slice Architecture implementation
   - Identify improvements for next phases
   - Document lessons learned

6. **Proceed to Phase 2**:
   - Begin Phase 2: Authentication Layer
   - Replace SHA-256 with BCrypt
   - Implement JWT authentication
   - Add protected endpoints

---

## Implementation Verification

Before considering Phase 1 complete, verify ALL of the following:

### Code Quality
- [ ] No compiler warnings or errors
- [ ] Nullable reference types respected
- [ ] All async methods use CancellationToken
- [ ] XML documentation on public APIs
- [ ] Follows Vertical Slice Architecture (all user code in Features/Users/)
- [ ] Repository pattern implemented with interface
- [ ] Service layer has business logic (not in repository or endpoints)

### Functionality
- [ ] Database table created successfully
- [ ] Unique constraint on email
- [ ] All endpoints return correct status codes
- [ ] ApiResponse wrapper used consistently
- [ ] Validation errors have field-level details
- [ ] Email normalized to lowercase
- [ ] Whitespace trimmed from fields
- [ ] Password hashed (not plain text)
- [ ] Timestamps in UTC

### Testing
- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] Test coverage >= 90%
- [ ] Tests follow AAA pattern
- [ ] Test names descriptive
- [ ] No flaky tests (run multiple times to verify)

### Integration
- [ ] EF Core migration applied successfully
- [ ] PostgreSQL table structure matches spec
- [ ] Services registered in DI container
- [ ] Endpoints mapped in Program.cs
- [ ] Swagger documentation generated
- [ ] Application starts without errors

### Documentation
- [ ] Technical documentation updated
- [ ] Swagger UI shows all endpoints
- [ ] API examples accessible
- [ ] No outdated information

### Manual Verification
- [ ] Create user via Swagger
- [ ] Retrieve user by ID
- [ ] List all users
- [ ] Update user details
- [ ] Test validation errors
- [ ] Test duplicate email
- [ ] Verify database records

**Run verification commands**:
```bash
# Build project
dotnet build src/Abuvi.API

# Run tests
dotnet test

# Check migration status
dotnet ef migrations list --project src/Abuvi.API

# Start application
dotnet run --project src/Abuvi.API
```

---

## Summary

This implementation plan provides step-by-step instructions to implement Phase 1 User Entity CRUD following Vertical Slice Architecture. The plan emphasizes:

- **Architectural consistency**: Vertical slices, Minimal APIs, repository pattern
- **Code quality**: Type safety, validation, error handling
- **Testing**: 90% coverage with unit and integration tests
- **Documentation**: Clear, maintainable code with Swagger docs

Follow the implementation order exactly, verify at each step, and ensure all verification criteria are met before proceeding to Phase 2.

**Complete implementation time estimate**: 4-6 hours for experienced .NET developer

---

**Plan saved to**: `ai-specs/changes/phase1_user_entity_crud_backend.md`
