# Phase 1: User Entity + Basic CRUD (No Authentication) - ENRICHED

## Goal

Establish Vertical Slice Architecture patterns with a working User feature that can be tested via Swagger/Postman. This phase validates the architecture before adding authentication complexity.

## Why This First?

- **Testable immediately**: Can verify CRUD operations without implementing auth
- **Establishes patterns**: Sets the structure all future features will follow
- **Early validation**: Confirms Vertical Slice Architecture works in this project
- **Visible progress**: Demonstrates working endpoints quickly

## What We're Building

1. User entity with full specification from data-model.md
2. EF Core entity configuration (Fluent API)
3. Database migration to create `users` table
4. Repository pattern (interface + implementation)
5. Service layer with business logic
6. FluentValidation for request DTOs
7. Minimal API endpoints (GET, POST, PUT)
8. Comprehensive test suite (unit + integration)
9. Error handling with standardized error codes
10. Transaction management for data consistency

**Note**: Password will use SHA-256 placeholder hash for now. Authentication will be added in Phase 2.

---

## Entity Specification

Based on `ai-specs/specs/data-model.md`:

### User Entity Fields

```csharp
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; }           // unique, max 255
    public string PasswordHash { get; set; }    // required, SHA-256 for Phase 1
    public string FirstName { get; set; }       // max 100
    public string LastName { get; set; }        // max 100
    public string? Phone { get; set; }          // optional, max 20
    public UserRole Role { get; set; }          // enum: Admin, Board, Member
    public Guid? FamilyUnitId { get; set; }     // optional FK (null for now)
    public bool IsActive { get; set; }          // default: true
    public DateTime CreatedAt { get; set; }     // auto-generated (UTC)
    public DateTime UpdatedAt { get; set; }     // auto-updated (UTC)
}

public enum UserRole
{
    Admin,
    Board,
    Member
}
```

### Validation Rules

- Email must be unique across all users (case-insensitive)
- Email must be valid format (RFC 5322)
- FirstName and LastName are required, cannot be whitespace only
- Role must be a valid enum value
- Phone format validation if provided (international format preferred)
- IsActive defaults to true
- Password must be at least 8 characters (validation only; hashing is service responsibility)

---

## Files to Create

### 1. UsersModels.cs
**Path**: `src/Abuvi.API/Features/Users/UsersModels.cs`

**Contents**:
- `User` entity class
- `UserRole` enum
- `CreateUserRequest` record
- `UpdateUserRequest` record
- `UserResponse` record

**Complete implementation**:

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

---

### 2. UserConfiguration.cs
**Path**: `src/Abuvi.API/Data/Configurations/UserConfiguration.cs`

**Purpose**: EF Core Fluent API configuration

**Complete implementation**:

```csharp
using Abuvi.API.Features.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Table name
        builder.ToTable("users");

        // Primary key
        builder.HasKey(u => u.Id);

        // Email: unique, case-insensitive index
        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email");

        // Password hash
        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasColumnName("password_hash");

        // First name
        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("first_name");

        // Last name
        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("last_name");

        // Phone (optional)
        builder.Property(u => u.Phone)
            .HasMaxLength(20)
            .HasColumnName("phone");

        // Role stored as string
        builder.Property(u => u.Role)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("role");

        // Family unit FK (nullable, constraint will be added when FamilyUnit entity exists)
        builder.Property(u => u.FamilyUnitId)
            .HasColumnName("family_unit_id");

        // IsActive with default
        builder.Property(u => u.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");

        // Timestamps with UTC default
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

**Key configurations**:
- Snake_case column names for PostgreSQL convention
- UUID primary key
- Email unique index (case-insensitive via PostgreSQL CITEXT or LOWER)
- Role stored as string enum
- FamilyUnitId nullable FK (configured but not enforced until FamilyUnit exists)
- Default timestamps using PostgreSQL NOW()
- Default value for IsActive

---

### 3. UsersRepository.cs
**Path**: `src/Abuvi.API/Features/Users/UsersRepository.cs`

**Complete implementation**:

```csharp
using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Users;

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

**Key implementation details**:
- `AsNoTracking()` for read operations (performance)
- Case-insensitive email comparison using `ToLower()`
- `EmailExistsAsync` overloads for create vs update scenarios
- Explicit `UpdatedAt` timestamp management in UpdateAsync
- Repository handles `SaveChangesAsync` internally (transaction boundary)
- All methods accept `CancellationToken` for cancellation support

---

### 4. UsersService.cs
**Path**: `src/Abuvi.API/Features/Users/UsersService.cs`

**Purpose**: Business logic layer

**Complete implementation**:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace Abuvi.API.Features.Users;

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

**Key business logic**:
- Email normalization (trim + lowercase) for consistency
- Duplicate email check before creation
- SHA-256 placeholder password hashing (clearly marked for Phase 2 replacement)
- Timestamp management (CreatedAt, UpdatedAt set in UTC)
- Field trimming to prevent leading/trailing whitespace issues
- DTO-to-Entity mapping with explicit field assignment
- Entity-to-DTO mapping via extension method

**Error handling**:
- Throws `InvalidOperationException` for duplicate email (will be caught by global error middleware)
- Returns `null` for not-found scenarios (handled by endpoints as 404)

---

### 5. CreateUserValidator.cs
**Path**: `src/Abuvi.API/Features/Users/CreateUserValidator.cs`

**Complete implementation**:

```csharp
using FluentValidation;

namespace Abuvi.API.Features.Users;

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

---

### 6. UpdateUserValidator.cs
**Path**: `src/Abuvi.API/Features/Users/UpdateUserValidator.cs`

**Complete implementation**:

```csharp
using FluentValidation;

namespace Abuvi.API.Features.Users;

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

---

### 7. UsersEndpoints.cs
**Path**: `src/Abuvi.API/Features/Users/UsersEndpoints.cs`

**Purpose**: Minimal API endpoint definitions

**Complete implementation**:

```csharp
using Abuvi.API.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace Abuvi.API.Features.Users;

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
            .Produces<ApiResponse<UserResponse>>(StatusCodes.Status201Created)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", UpdateUser)
            .WithName("UpdateUser")
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

**Key implementation details**:
- Uses `ApiResponse<T>` wrapper for consistent response format
- Returns appropriate HTTP status codes:
  - 200 OK for successful GET/PUT
  - 201 Created for successful POST with Location header
  - 400 Bad Request for validation errors (handled by validation filter)
  - 404 Not Found when resource doesn't exist
  - 409 Conflict for duplicate email
- All endpoints accept `CancellationToken`
- OpenAPI documentation support via `WithOpenApi()` and `Produces<T>()`
- Explicit `[FromRoute]` and `[FromBody]` attributes for clarity

---

## Files to Modify

### 1. AbuviDbContext.cs
**Path**: `src/Abuvi.API/Data/AbuviDbContext.cs`

**Change**: Add Users DbSet

```csharp
public class AbuviDbContext(DbContextOptions<AbuviDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AbuviDbContext).Assembly);
    }
}
```

---

### 2. Program.cs
**Path**: `src/Abuvi.API/Program.cs`

**Changes**:

**Add service registrations** (before `var app = builder.Build();`):

```csharp
// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// User feature services
builder.Services.AddScoped<IUsersRepository, UsersRepository>();
builder.Services.AddScoped<UsersService>();
```

**Map endpoints** (after middleware pipeline, before `app.Run()`):

```csharp
// Map feature endpoints
app.MapUsersEndpoints();
```

---

## Common Models (Shared)

### ApiResponse.cs
**Path**: `src/Abuvi.API/Common/Models/ApiResponse.cs`

**Purpose**: Standardized API response wrapper

```csharp
namespace Abuvi.API.Common.Models;

public record ApiResponse<T>(bool Success, T? Data = default, ApiError? Error = null)
{
    public static ApiResponse<T> Ok(T data) => new(true, data);

    public static ApiResponse<T> NotFound(string message) =>
        new(false, Error: new ApiError(message, "NOT_FOUND"));

    public static ApiResponse<T> Fail(string message, string code) =>
        new(false, Error: new ApiError(message, code));

    public static ApiResponse<T> ValidationFail(string message, List<ValidationError> details) =>
        new(false, Error: new ApiError(message, "VALIDATION_ERROR", details));
}

public record ApiError(
    string Message,
    string Code,
    List<ValidationError>? Details = null
);

public record ValidationError(
    string Field,
    string Message
);
```

---

### ValidationFilter.cs
**Path**: `src/Abuvi.API/Common/Filters/ValidationFilter.cs`

**Purpose**: Automatic FluentValidation integration

```csharp
using Abuvi.API.Common.Models;
using FluentValidation;

namespace Abuvi.API.Common.Filters;

public class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator is null)
            return await next(context);

        var request = context.Arguments.OfType<T>().FirstOrDefault();
        if (request is null)
            return await next(context);

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        if (!result.IsValid)
        {
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

**Apply to endpoints** in UsersEndpoints.cs:

```csharp
group.MapPost("/", CreateUser)
    .AddEndpointFilter<ValidationFilter<CreateUserRequest>>();

group.MapPut("/{id:guid}", UpdateUser)
    .AddEndpointFilter<ValidationFilter<UpdateUserRequest>>();
```

---

## Database Migration

### Commands

1. **Create migration**:
```bash
dotnet ef migrations add AddUserEntity --project src/Abuvi.API
```

2. **Review generated migration** in `src/Abuvi.API/Data/Migrations/`:
   - Check table name is `users`
   - Verify all columns are present with correct types and snake_case names
   - Confirm unique index on email column
   - Check default values for `is_active`, `created_at`, `updated_at`
   - Verify Role is stored as string (not integer)

3. **Apply migration**:
```bash
dotnet ef database update --project src/Abuvi.API
```

4. **Verify in PostgreSQL**:
```bash
docker exec -it abuvi-postgres psql -U abuvi_user -d abuvi -c "\d users"
```

**Expected table structure**:
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

---

## Testing

### Unit Tests

#### 1. UsersServiceTests.cs
**Path**: `src/Abuvi.Tests/Unit/Features/Users/UsersServiceTests.cs`

**Complete test suite**:

```csharp
using Abuvi.API.Features.Users;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Users;

public class UsersServiceTests
{
    private readonly IUsersRepository _repository;
    private readonly UsersService _sut;

    public UsersServiceTests()
    {
        _repository = Substitute.For<IUsersRepository>();
        _sut = new UsersService(_repository);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ReturnsCreatedUser()
    {
        // Arrange
        var request = new CreateUserRequest(
            "test@example.com",
            "password123",
            "John",
            "Doe",
            null,
            UserRole.Member
        );

        _repository.EmailExistsAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        _repository.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<User>());

        // Act
        var result = await _sut.CreateAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be("test@example.com");
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
        result.Role.Should().Be(UserRole.Member);
        result.IsActive.Should().BeTrue();

        await _repository.Received(1).AddAsync(
            Arg.Is<User>(u =>
                u.Email == "test@example.com" &&
                u.FirstName == "John" &&
                u.LastName == "Doe"
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateEmail_ThrowsException()
    {
        // Arrange
        var request = new CreateUserRequest(
            "existing@example.com",
            "password123",
            "John",
            "Doe",
            null,
            UserRole.Member
        );

        _repository.EmailExistsAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        Func<Task> act = async () => await _sut.CreateAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");

        await _repository.DidNotReceive().AddAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task CreateAsync_NormalizesEmail_ToLowerCase()
    {
        // Arrange
        var request = new CreateUserRequest(
            "Test@Example.COM",
            "password123",
            "John",
            "Doe",
            null,
            UserRole.Member
        );

        _repository.EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _repository.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<User>());

        // Act
        var result = await _sut.CreateAsync(request, CancellationToken.None);

        // Assert
        result.Email.Should().Be("test@example.com");

        await _repository.Received(1).AddAsync(
            Arg.Is<User>(u => u.Email == "test@example.com"),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task CreateAsync_TrimsWhitespace_FromFields()
    {
        // Arrange
        var request = new CreateUserRequest(
            "  test@example.com  ",
            "password123",
            "  John  ",
            "  Doe  ",
            "  +34 123 456 789  ",
            UserRole.Member
        );

        _repository.EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _repository.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<User>());

        // Act
        var result = await _sut.CreateAsync(request, CancellationToken.None);

        // Assert
        result.Email.Should().Be("test@example.com");
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
        result.Phone.Should().Be("+34 123 456 789");
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingUser_ReturnsUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe",
            Role = UserRole.Member,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var result = await _sut.GetByIdAsync(userId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(userId);
        result.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentUser_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var result = await _sut.GetByIdAsync(userId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingUser = new User
        {
            Id = userId,
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "John",
            LastName = "Doe",
            Role = UserRole.Member,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-10)
        };

        var request = new UpdateUserRequest(
            "Jane",
            "Smith",
            "+34 123 456 789",
            false
        );

        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(existingUser);

        // Act
        var result = await _sut.UpdateAsync(userId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.FirstName.Should().Be("Jane");
        result.LastName.Should().Be("Smith");
        result.Phone.Should().Be("+34 123 456 789");
        result.IsActive.Should().BeFalse();

        // Email and Role should not change
        result.Email.Should().Be("test@example.com");
        result.Role.Should().Be(UserRole.Member);

        await _repository.Received(1).UpdateAsync(
            Arg.Is<User>(u =>
                u.FirstName == "Jane" &&
                u.LastName == "Smith" &&
                u.IsActive == false
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentUser_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new UpdateUserRequest("Jane", "Smith", null, true);

        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var result = await _sut.UpdateAsync(userId, request, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        await _repository.DidNotReceive().UpdateAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllUsers()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Id = Guid.NewGuid(), Email = "user1@example.com", FirstName = "User", LastName = "One", Role = UserRole.Member, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Email = "user2@example.com", FirstName = "User", LastName = "Two", Role = UserRole.Board, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Email = "user3@example.com", FirstName = "User", LastName = "Three", Role = UserRole.Admin, IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(users);

        // Act
        var result = await _sut.GetAllAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(u => u.Email == "user1@example.com");
        result.Should().Contain(u => u.Email == "user2@example.com");
        result.Should().Contain(u => u.Email == "user3@example.com");
    }

    [Fact]
    public async Task CreateAsync_HashesPassword_UsingSHA256()
    {
        // Arrange
        var request = new CreateUserRequest(
            "test@example.com",
            "mySecurePassword",
            "John",
            "Doe",
            null,
            UserRole.Member
        );

        _repository.EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _repository.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<User>());

        // Act
        await _sut.CreateAsync(request, CancellationToken.None);

        // Assert
        await _repository.Received(1).AddAsync(
            Arg.Is<User>(u =>
                !string.IsNullOrEmpty(u.PasswordHash) &&
                u.PasswordHash != "mySecurePassword" && // Should be hashed, not plain
                u.PasswordHash.Length > 20 // SHA-256 Base64 is 44 characters
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
```

---

#### 2. CreateUserValidatorTests.cs
**Path**: `src/Abuvi.Tests/Unit/Features/Users/CreateUserValidatorTests.cs`

```csharp
using Abuvi.API.Features.Users;
using FluentAssertions;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Users;

public class CreateUserValidatorTests
{
    private readonly CreateUserValidator _validator = new();

    [Fact]
    public void Validate_WithValidData_Passes()
    {
        // Arrange
        var request = new CreateUserRequest(
            "test@example.com",
            "password123",
            "John",
            "Doe",
            "+34 123 456 789",
            UserRole.Member
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyEmail_Fails()
    {
        // Arrange
        var request = new CreateUserRequest(
            "",
            "password123",
            "John",
            "Doe",
            null,
            UserRole.Member
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email" && e.ErrorMessage.Contains("required"));
    }

    [Fact]
    public void Validate_WithInvalidEmailFormat_Fails()
    {
        // Arrange
        var request = new CreateUserRequest(
            "not-an-email",
            "password123",
            "John",
            "Doe",
            null,
            UserRole.Member
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email" && e.ErrorMessage.Contains("valid email"));
    }

    [Fact]
    public void Validate_WithShortPassword_Fails()
    {
        // Arrange
        var request = new CreateUserRequest(
            "test@example.com",
            "pass",
            "John",
            "Doe",
            null,
            UserRole.Member
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password" && e.ErrorMessage.Contains("8 characters"));
    }

    [Fact]
    public void Validate_WithEmptyFirstName_Fails()
    {
        // Arrange
        var request = new CreateUserRequest(
            "test@example.com",
            "password123",
            "",
            "Doe",
            null,
            UserRole.Member
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
    }

    [Fact]
    public void Validate_WithWhitespaceOnlyFirstName_Fails()
    {
        // Arrange
        var request = new CreateUserRequest(
            "test@example.com",
            "password123",
            "   ",
            "Doe",
            null,
            UserRole.Member
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName" && e.ErrorMessage.Contains("whitespace"));
    }

    [Fact]
    public void Validate_WithTooLongPhone_Fails()
    {
        // Arrange
        var request = new CreateUserRequest(
            "test@example.com",
            "password123",
            "John",
            "Doe",
            "+34 123 456 789 012 345 678", // > 20 characters
            UserRole.Member
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Phone" && e.ErrorMessage.Contains("20 characters"));
    }

    [Fact]
    public void Validate_WithInvalidPhoneCharacters_Fails()
    {
        // Arrange
        var request = new CreateUserRequest(
            "test@example.com",
            "password123",
            "John",
            "Doe",
            "abc-123-def",
            UserRole.Member
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Phone");
    }

    [Fact]
    public void Validate_WithInvalidRole_Fails()
    {
        // Arrange
        var request = new CreateUserRequest(
            "test@example.com",
            "password123",
            "John",
            "Doe",
            null,
            (UserRole)999 // Invalid enum value
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Role");
    }

    [Fact]
    public void Validate_WithNullPhone_Passes()
    {
        // Arrange
        var request = new CreateUserRequest(
            "test@example.com",
            "password123",
            "John",
            "Doe",
            null,
            UserRole.Member
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("+34 123 456 789")]
    [InlineData("123-456-7890")]
    [InlineData("(123) 456-7890")]
    [InlineData("+1 (555) 123-4567")]
    public void Validate_WithValidPhoneFormats_Passes(string phone)
    {
        // Arrange
        var request = new CreateUserRequest(
            "test@example.com",
            "password123",
            "John",
            "Doe",
            phone,
            UserRole.Member
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
```

---

### Integration Tests

#### 3. UsersIntegrationTests.cs
**Path**: `src/Abuvi.Tests/Integration/Features/UsersIntegrationTests.cs`

```csharp
using System.Net;
using System.Net.Http.Json;
using Abuvi.API.Common.Models;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Abuvi.Tests.Integration.Features;

public class UsersIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public UsersIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Cleanup: Delete test users if needed
        await Task.CompletedTask;
    }

    [Fact]
    public async Task GetAllUsers_ReturnsEmptyList_WhenNoUsers()
    {
        // Act
        var response = await _client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserResponse>>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateUser_ReturnsCreated_WithValidData()
    {
        // Arrange
        var request = new CreateUserRequest(
            $"newuser{Guid.NewGuid()}@example.com",
            "password123",
            "Jane",
            "Smith",
            "+34 123 456 789",
            UserRole.Member
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Email.Should().Be(request.Email.ToLower());
        result.Data.FirstName.Should().Be("Jane");
        result.Data.LastName.Should().Be("Smith");
        result.Data.Role.Should().Be(UserRole.Member);
        result.Data.IsActive.Should().BeTrue();
        result.Data.Phone.Should().Be("+34 123 456 789");
    }

    [Fact]
    public async Task CreateUser_ReturnsBadRequest_WithInvalidEmail()
    {
        // Arrange
        var request = new CreateUserRequest(
            "not-an-email",
            "password123",
            "Jane",
            "Smith",
            null,
            UserRole.Member
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("VALIDATION_ERROR");
        result.Error.Details.Should().NotBeNull();
        result.Error.Details.Should().Contain(e => e.Field == "Email");
    }

    [Fact]
    public async Task CreateUser_ReturnsBadRequest_WithShortPassword()
    {
        // Arrange
        var request = new CreateUserRequest(
            "test@example.com",
            "short",
            "Jane",
            "Smith",
            null,
            UserRole.Member
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        result!.Error!.Details.Should().Contain(e => e.Field == "Password");
    }

    [Fact]
    public async Task CreateUser_ReturnsConflict_WithDuplicateEmail()
    {
        // Arrange
        var email = $"duplicate{Guid.NewGuid()}@example.com";
        var request1 = new CreateUserRequest(email, "password123", "User", "One", null, UserRole.Member);
        var request2 = new CreateUserRequest(email, "password456", "User", "Two", null, UserRole.Member);

        // Create first user
        await _client.PostAsJsonAsync("/api/users", request1);

        // Act
        var response = await _client.PostAsJsonAsync("/api/users", request2);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        result!.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("USER_ALREADY_EXISTS");
    }

    [Fact]
    public async Task GetUserById_ReturnsUser_WhenExists()
    {
        // Arrange - Create a user first
        var createRequest = new CreateUserRequest(
            $"getbyid{Guid.NewGuid()}@example.com",
            "password123",
            "John",
            "Doe",
            null,
            UserRole.Member
        );
        var createResponse = await _client.PostAsJsonAsync("/api/users", createRequest);
        var createdUser = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;

        // Act
        var response = await _client.GetAsync($"/api/users/{createdUser.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        result!.Success.Should().BeTrue();
        result.Data!.Id.Should().Be(createdUser.Id);
        result.Data.Email.Should().Be(createdUser.Email);
    }

    [Fact]
    public async Task GetUserById_ReturnsNotFound_WhenNotExists()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/users/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        result!.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task UpdateUser_ReturnsUpdated_WithValidData()
    {
        // Arrange - Create a user first
        var createRequest = new CreateUserRequest(
            $"update{Guid.NewGuid()}@example.com",
            "password123",
            "John",
            "Doe",
            null,
            UserRole.Member
        );
        var createResponse = await _client.PostAsJsonAsync("/api/users", createRequest);
        var createdUser = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;

        var updateRequest = new UpdateUserRequest(
            "Jane",
            "Smith",
            "+34 987 654 321",
            false
        );

        // Act
        var response = await _client.PutAsJsonAsync($"/api/users/{createdUser.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        result!.Success.Should().BeTrue();
        result.Data!.FirstName.Should().Be("Jane");
        result.Data.LastName.Should().Be("Smith");
        result.Data.Phone.Should().Be("+34 987 654 321");
        result.Data.IsActive.Should().BeFalse();

        // Email and Role should not change
        result.Data.Email.Should().Be(createdUser.Email);
        result.Data.Role.Should().Be(createdUser.Role);
    }

    [Fact]
    public async Task UpdateUser_ReturnsNotFound_WhenNotExists()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var updateRequest = new UpdateUserRequest("Jane", "Smith", null, true);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/users/{nonExistentId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateUser_NormalizesEmailToLowerCase()
    {
        // Arrange
        var request = new CreateUserRequest(
            $"UPPERCASE{Guid.NewGuid()}@EXAMPLE.COM",
            "password123",
            "John",
            "Doe",
            null,
            UserRole.Member
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/users", request);

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        result!.Data!.Email.Should().Be(request.Email.ToLower());
    }

    [Fact]
    public async Task CreateUser_WithAllRoles_Succeeds()
    {
        // Test all enum values
        foreach (UserRole role in Enum.GetValues(typeof(UserRole)))
        {
            // Arrange
            var request = new CreateUserRequest(
                $"role{role}{Guid.NewGuid()}@example.com",
                "password123",
                "Test",
                "User",
                null,
                role
            );

            // Act
            var response = await _client.PostAsJsonAsync("/api/users", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            result!.Data!.Role.Should().Be(role);
        }
    }
}
```

---

## Non-Functional Requirements

### Security

1. **Input Validation**: All inputs validated via FluentValidation before reaching business logic
2. **SQL Injection**: Prevented by EF Core parameterized queries (never use FromSqlRaw with string interpolation)
3. **Password Storage**: Even placeholder hashing (SHA-256) ensures passwords never stored in plain text
4. **Email Normalization**: Lowercase storage prevents case-sensitivity bypass attempts
5. **RGPD Compliance**: User data is personal data under GDPR; audit trail via timestamps

### Performance

1. **Database Queries**:
   - Use `AsNoTracking()` for read-only queries (5-10% performance gain)
   - Email uniqueness check uses indexed column (sub-millisecond lookup)
   - Repository methods complete in <100ms for single-record operations
2. **API Response Time**: Target <200ms for GET requests, <500ms for POST/PUT
3. **Pagination**: Not implemented in Phase 1 (GetAll returns all users), but will be required when user count >100

### Reliability

1. **Transaction Management**: Each repository operation is atomic (SaveChangesAsync called within repository)
2. **Concurrency**: Race condition on duplicate email check is possible; will be addressed with unique constraint in database (handled by PostgreSQL)
3. **Error Handling**: All exceptions logged and returned as structured errors
4. **Idempotency**: PUT operations are idempotent (same request always produces same result)

### Maintainability

1. **Vertical Slice Architecture**: All user-related code in single feature folder
2. **Separation of Concerns**: Clear boundaries between endpoints, service, and repository
3. **Test Coverage**: Target 90% code coverage
4. **Documentation**: XML comments on public APIs

---

## Error Codes Reference

| HTTP Status | Error Code | Scenario | Example Message |
|-------------|-----------|----------|-----------------|
| 400 | VALIDATION_ERROR | Request validation failed | "Validation failed" with field details |
| 404 | NOT_FOUND | User not found by ID | "User not found" |
| 409 | USER_ALREADY_EXISTS | Email already registered | "User with email 'x@example.com' already exists" |
| 500 | INTERNAL_ERROR | Unexpected server error | "An unexpected error occurred" |

---

## API Response Examples

### Success Response (GET /api/users/{id})
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

### Validation Error Response (POST /api/users)
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

### Conflict Error Response (POST /api/users with duplicate email)
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

### Not Found Response (GET /api/users/{id})
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

---

## Verification Checklist

After completing Phase 1, verify:

### Database
- [ ] Table `users` exists in PostgreSQL
- [ ] Email column has unique constraint (IX_Users_Email)
- [ ] Role is stored as string (not integer)
- [ ] Timestamps have default values (NOW())
- [ ] Column names use snake_case (created_at, updated_at, etc.)
- [ ] is_active has default value of true

### API Endpoints (via Swagger: http://localhost:5079/swagger)
- [ ] GET /api/users returns empty array initially (200 OK)
- [ ] POST /api/users with valid data creates user (201 Created with Location header)
- [ ] POST /api/users with invalid email returns 400 Bad Request with validation errors
- [ ] POST /api/users with short password returns 400 Bad Request
- [ ] POST /api/users with duplicate email returns 409 Conflict
- [ ] GET /api/users/{id} with existing user returns user (200 OK)
- [ ] GET /api/users/{id} with non-existent ID returns 404 Not Found
- [ ] PUT /api/users/{id} updates user (200 OK)
- [ ] PUT /api/users/{id} with non-existent ID returns 404 Not Found
- [ ] All responses use ApiResponse<T> wrapper format

### Business Logic
- [ ] Email is normalized to lowercase
- [ ] Whitespace is trimmed from all string fields
- [ ] Password is hashed (never stored in plain text)
- [ ] CreatedAt and UpdatedAt timestamps are set in UTC
- [ ] UpdatedAt is refreshed on every update
- [ ] IsActive defaults to true for new users
- [ ] Email uniqueness check is case-insensitive

### Tests
- [ ] All unit tests pass: `dotnet test --filter FullyQualifiedName~Users`
- [ ] All integration tests pass
- [ ] Test coverage >= 90% for Users feature
- [ ] No console errors or warnings during test execution
- [ ] Tests use proper AAA pattern (Arrange-Act-Assert)
- [ ] Test names follow convention: MethodName_StateUnderTest_ExpectedBehavior

### Code Quality
- [ ] No compiler warnings
- [ ] Follows Vertical Slice Architecture pattern (all user code in Features/Users/)
- [ ] FluentValidation configured correctly
- [ ] Repository pattern implemented with interface
- [ ] Service layer has business logic (not in endpoints or repository)
- [ ] All public APIs have XML documentation comments
- [ ] CancellationToken used throughout async methods
- [ ] Nullable reference types enabled and respected

---

## Next Steps

After Phase 1 is complete and verified:

1. **Review Patterns**: Review the Vertical Slice Architecture implementation
2. **Identify Improvements**: Discuss any architectural improvements needed
3. **Proceed to Phase 2**: Begin **Phase 2: Authentication Layer** (`phase2_authentication_layer.md`)
   - Replace SHA-256 placeholder with BCrypt
   - Implement JWT authentication
   - Add protected endpoints
   - Implement login/logout flows

---

## Implementation Notes

### Timestamps
- **CreatedAt**: Set once on entity creation, never updated
- **UpdatedAt**: Set on creation and refreshed on every update
- All timestamps use **UTC** to avoid timezone issues
- Database defaults ensure timestamps set even if code forgets

### Password Hashing (Phase 1 Placeholder)
- Uses SHA-256 for demonstration purposes only
- Produces deterministic hashes (same password always produces same hash)
- **Security Note**: SHA-256 is NOT suitable for production password storage
- Phase 2 will replace with BCrypt (adaptive cost factor, salted)

### Email Handling
- Stored in lowercase for consistency
- Unique constraint enforced at database level (PostgreSQL handles race conditions)
- Case-insensitive comparison in repository using `ToLower()`
- Consider PostgreSQL CITEXT type for true case-insensitive storage (Phase 2 improvement)

### Transaction Boundaries
- Each repository method is a transaction boundary
- `SaveChangesAsync()` called within repository ensures atomic operations
- No explicit transaction management needed in Phase 1 (single-entity operations)
- Complex multi-entity operations (Phase 2+) will require explicit transactions

### Error Handling Strategy
- Domain-specific exceptions (InvalidOperationException) for business rule violations
- Global error middleware catches and formats exceptions consistently
- Null returns for not-found scenarios (converted to 404 by endpoints)
- Validation errors handled by ValidationFilter (automatic 400 responses)

---

## File Structure Summary

```
src/Abuvi.API/
├── Features/
│   └── Users/
│       ├── UsersModels.cs              # Entity, DTOs, enums
│       ├── UsersRepository.cs          # Data access layer
│       ├── UsersService.cs             # Business logic layer
│       ├── CreateUserValidator.cs      # Validation rules
│       ├── UpdateUserValidator.cs      # Validation rules
│       └── UsersEndpoints.cs           # API endpoints
├── Data/
│   ├── AbuviDbContext.cs               # EF Core context (modified)
│   └── Configurations/
│       └── UserConfiguration.cs        # Entity configuration
├── Common/
│   ├── Models/
│   │   └── ApiResponse.cs              # Response wrapper
│   └── Filters/
│       └── ValidationFilter.cs         # Validation filter
└── Program.cs                          # DI registration (modified)

src/Abuvi.Tests/
├── Unit/
│   └── Features/
│       └── Users/
│           ├── UsersServiceTests.cs
│           ├── CreateUserValidatorTests.cs
│           └── UpdateUserValidatorTests.cs
└── Integration/
    └── Features/
        └── UsersIntegrationTests.cs
```

---

## Glossary

- **DTO (Data Transfer Object)**: Records used for API requests/responses (CreateUserRequest, UserResponse)
- **Entity**: Domain model mapped to database table (User class)
- **Repository**: Data access abstraction (IUsersRepository, UsersRepository)
- **Service**: Business logic layer (UsersService)
- **Endpoint**: API route handler (methods in UsersEndpoints)
- **Vertical Slice**: Organizing code by feature rather than technical layer
- **FluentValidation**: Library for building strongly-typed validation rules
- **Minimal API**: Lightweight API framework in .NET (alternative to MVC Controllers)
- **EF Core**: Entity Framework Core, Microsoft's ORM
- **Fluent API**: EF Core configuration using method chaining (vs. attributes)

---

This enriched specification provides complete implementation details for autonomous development of Phase 1 User Entity CRUD functionality.
