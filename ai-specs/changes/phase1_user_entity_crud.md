# Phase 1: User Entity + Basic CRUD (No Authentication)

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

**Note**: Password will use a placeholder hash for now. Authentication will be added in Phase 2.

## Entity Specification

Based on `ai-specs/specs/data-model.md`:

### User Entity Fields

```csharp
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; }           // unique, max 255
    public string PasswordHash { get; set; }    // required
    public string FirstName { get; set; }       // max 100
    public string LastName { get; set; }        // max 100
    public string? Phone { get; set; }          // optional, max 20
    public UserRole Role { get; set; }          // enum: Admin, Board, Member
    public Guid? FamilyUnitId { get; set; }     // optional FK (null for now)
    public bool IsActive { get; set; }          // default: true
    public DateTime CreatedAt { get; set; }     // auto-generated
    public DateTime UpdatedAt { get; set; }     // auto-updated
}

public enum UserRole
{
    Admin,
    Board,
    Member
}
```

### Validation Rules

- Email must be unique across all users
- Email must be valid format
- FirstName and LastName are required
- Role must be a valid enum value
- Phone format validation if provided
- IsActive defaults to true

## Files to Create

### 1. UsersModels.cs
**Path**: `src/Abuvi.API/Features/Users/UsersModels.cs`

**Contents**:
- `User` entity class
- `UserRole` enum
- `CreateUserRequest` record
- `UpdateUserRequest` record
- `UserResponse` record

**Example DTOs**:
```csharp
public record CreateUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone,
    UserRole Role
);

public record UpdateUserRequest(
    string FirstName,
    string LastName,
    string? Phone,
    bool IsActive
);

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

### 2. UserConfiguration.cs
**Path**: `src/Abuvi.API/Data/Configurations/UserConfiguration.cs`

**Purpose**: EF Core Fluent API configuration

**Key configurations**:
- UUID primary key
- Email unique index: `builder.HasIndex(u => u.Email).IsUnique()`
- Role stored as string: `builder.Property(u => u.Role).HasConversion<string>()`
- FamilyUnitId nullable FK (configured but not enforced until FamilyUnit exists)
- Default timestamps

**Example**:
```csharp
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .IsRequired();

        // ... more configuration
    }
}
```

### 3. UsersRepository.cs
**Path**: `src/Abuvi.API/Features/Users/UsersRepository.cs`

**Contents**:
- `IUsersRepository` interface
- `UsersRepository` implementation

**Interface methods**:
```csharp
public interface IUsersRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<List<User>> GetAllAsync();
    Task<User> AddAsync(User user);
    Task UpdateAsync(User user);
}
```

**Implementation uses**:
- `AbuviDbContext`
- Async/await with EF Core
- `AsNoTracking()` for read operations

### 4. UsersService.cs
**Path**: `src/Abuvi.API/Features/Users/UsersService.cs`

**Purpose**: Business logic layer

**Responsibilities**:
- Validate email uniqueness before creation
- Set timestamps (CreatedAt, UpdatedAt)
- Map between entity and DTOs
- Apply business rules

**Key methods**:
```csharp
public class UsersService
{
    Task<UserResponse?> GetByIdAsync(Guid id);
    Task<UserResponse?> GetByEmailAsync(string email);
    Task<List<UserResponse>> GetAllAsync();
    Task<UserResponse> CreateAsync(CreateUserRequest request);
    Task<UserResponse?> UpdateAsync(Guid id, UpdateUserRequest request);
}
```

**Note**: For Phase 1, password will be hashed with a simple placeholder (e.g., SHA256). Real BCrypt hashing comes in Phase 2.

### 5. CreateUserValidator.cs
**Path**: `src/Abuvi.API/Features/Users/CreateUserValidator.cs`

**Purpose**: FluentValidation for CreateUserRequest

**Validation rules**:
```csharp
public class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8);

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .When(x => x.Phone != null);

        RuleFor(x => x.Role)
            .IsInEnum();
    }
}
```

### 6. UpdateUserValidator.cs
**Path**: `src/Abuvi.API/Features/Users/UpdateUserValidator.cs`

Similar validation for UpdateUserRequest.

### 7. UsersEndpoints.cs
**Path**: `src/Abuvi.API/Features/Users/UsersEndpoints.cs`

**Purpose**: Minimal API endpoint definitions

**Endpoints**:
- `GET /api/users` - List all users (will be protected in Phase 2)
- `GET /api/users/{id}` - Get user by ID
- `POST /api/users` - Create new user
- `PUT /api/users/{id}` - Update user

**Example structure**:
```csharp
public static class UsersEndpoints
{
    public static void MapUsersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users");

        group.MapGet("/", GetAllUsers);
        group.MapGet("/{id}", GetUserById);
        group.MapPost("/", CreateUser);
        group.MapPut("/{id}", UpdateUser);
    }

    private static async Task<IResult> GetAllUsers(UsersService service)
    {
        var users = await service.GetAllAsync();
        return Results.Ok(ApiResponse<List<UserResponse>>.Ok(users));
    }

    // ... other handlers
}
```

## Files to Modify

### 1. AbuviDbContext.cs
**Path**: `src/Abuvi.API/Data/AbuviDbContext.cs`

**Change**: Add Users DbSet
```csharp
public DbSet<User> Users => Set<User>();
```

### 2. Program.cs
**Path**: `src/Abuvi.API/Program.cs`

**Changes**:

**Add service registrations** (before `var app = builder.Build();`):
```csharp
// User feature services
builder.Services.AddScoped<IUsersRepository, UsersRepository>();
builder.Services.AddScoped<UsersService>();
```

**Map endpoints** (after middleware pipeline, before `app.Run()`):
```csharp
// Map feature endpoints
app.MapUsersEndpoints();
```

## Database Migration

### Commands

1. **Create migration**:
```bash
dotnet ef migrations add AddUserEntity --project src/Abuvi.API
```

2. **Review generated migration** in `src/Abuvi.API/Data/Migrations/`:
   - Check table name is `users`
   - Verify all columns are present with correct types
   - Confirm unique index on email
   - Check default values for IsActive, CreatedAt, UpdatedAt

3. **Apply migration**:
```bash
dotnet ef database update --project src/Abuvi.API
```

4. **Verify in PostgreSQL**:
```bash
docker exec -it abuvi-postgres psql -U abuvi_user -d abuvi -c "\d users"
```

Expected table structure:
- id (uuid, PK)
- email (varchar(255), unique)
- password_hash (text)
- first_name (varchar(100))
- last_name (varchar(100))
- phone (varchar(20), nullable)
- role (text)
- family_unit_id (uuid, nullable)
- is_active (boolean, default true)
- created_at (timestamp)
- updated_at (timestamp)

## Testing

### Unit Tests

#### 1. UsersServiceTests.cs
**Path**: `src/Abuvi.Tests/Unit/Features/Users/UsersServiceTests.cs`

**Test cases**:
- CreateAsync_WithValidData_ReturnsCreatedUser
- CreateAsync_WithDuplicateEmail_ThrowsException
- GetByIdAsync_WithExistingUser_ReturnsUser
- GetByIdAsync_WithNonExistentUser_ReturnsNull
- UpdateAsync_WithValidData_UpdatesUser
- GetAllAsync_ReturnsAllUsers

**Mocking**: Use NSubstitute to mock IUsersRepository

**Example**:
```csharp
[Fact]
public async Task CreateAsync_WithValidData_ReturnsCreatedUser()
{
    // Arrange
    var repository = Substitute.For<IUsersRepository>();
    var service = new UsersService(repository);
    var request = new CreateUserRequest(
        "test@example.com",
        "password123",
        "John",
        "Doe",
        null,
        UserRole.Member
    );

    repository.GetByEmailAsync(request.Email).Returns((User?)null);
    repository.AddAsync(Arg.Any<User>()).Returns(callInfo => callInfo.Arg<User>());

    // Act
    var result = await service.CreateAsync(request);

    // Assert
    result.Should().NotBeNull();
    result.Email.Should().Be("test@example.com");
    result.FirstName.Should().Be("John");
    await repository.Received(1).AddAsync(Arg.Any<User>());
}
```

#### 2. CreateUserValidatorTests.cs
**Path**: `src/Abuvi.Tests/Unit/Features/Users/CreateUserValidatorTests.cs`

**Test cases**:
- Validate_WithValidData_Passes
- Validate_WithEmptyEmail_Fails
- Validate_WithInvalidEmailFormat_Fails
- Validate_WithShortPassword_Fails
- Validate_WithEmptyFirstName_Fails
- Validate_WithTooLongPhone_Fails
- Validate_WithInvalidRole_Fails

**Example**:
```csharp
[Fact]
public void Validate_WithInvalidEmailFormat_Fails()
{
    // Arrange
    var validator = new CreateUserValidator();
    var request = new CreateUserRequest(
        "not-an-email",
        "password123",
        "John",
        "Doe",
        null,
        UserRole.Member
    );

    // Act
    var result = validator.Validate(request);

    // Assert
    result.IsValid.Should().BeFalse();
    result.Errors.Should().Contain(e => e.PropertyName == "Email");
}
```

### Integration Tests

#### 3. UsersIntegrationTests.cs
**Path**: `src/Abuvi.Tests/Integration/Features/UsersIntegrationTests.cs`

**Test cases**:
- GetAllUsers_ReturnsEmptyList_WhenNoUsers
- CreateUser_ReturnsCreated_WithValidData
- CreateUser_ReturnsBadRequest_WithInvalidEmail
- GetUserById_ReturnsUser_WhenExists
- GetUserById_ReturnsNotFound_WhenNotExists
- UpdateUser_ReturnsUpdated_WithValidData
- CreateUser_ReturnsBadRequest_WithDuplicateEmail

**Setup**: Use WebApplicationFactory with test database

**Example**:
```csharp
public class UsersIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public UsersIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateUser_ReturnsCreated_WithValidData()
    {
        // Arrange
        var request = new CreateUserRequest(
            "newuser@example.com",
            "password123",
            "Jane",
            "Smith",
            null,
            UserRole.Member
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        user.Should().NotBeNull();
        user!.Success.Should().BeTrue();
        user.Data!.Email.Should().Be("newuser@example.com");
    }
}
```

## Verification Checklist

After completing Phase 1, verify:

### Database
- [ ] Table `users` exists in PostgreSQL
- [ ] Email column has unique constraint
- [ ] Role is stored as string
- [ ] Timestamps have default values

### API Endpoints (via Swagger: http://localhost:5079/swagger)
- [ ] GET /api/users returns empty array (200 OK)
- [ ] POST /api/users with valid data creates user (201 Created)
- [ ] POST /api/users with invalid email returns 400 Bad Request with validation errors
- [ ] POST /api/users with duplicate email returns 400 Bad Request
- [ ] GET /api/users/{id} with existing user returns user (200 OK)
- [ ] GET /api/users/{id} with non-existent ID returns 404 Not Found
- [ ] PUT /api/users/{id} updates user (200 OK)

### Tests
- [ ] All unit tests pass: `dotnet test --filter FullyQualifiedName~Users`
- [ ] All integration tests pass
- [ ] Test coverage >= 90%
- [ ] No console errors or warnings

### Code Quality
- [ ] No compiler warnings
- [ ] Follows Vertical Slice Architecture pattern
- [ ] FluentValidation configured correctly
- [ ] Repository pattern implemented
- [ ] Service layer has business logic

## Next Steps

After Phase 1 is complete and verified:
1. Review the Vertical Slice pattern with the team
2. Identify any architectural improvements needed
3. Proceed to **Phase 2: Authentication Layer** (`phase2_authentication_layer.md`)

## Notes

- FamilyUnitId is nullable and will remain null until FamilyUnit entity is implemented
- Password uses placeholder hashing in Phase 1; BCrypt will be added in Phase 2
- No authentication required yet; all endpoints are public
- This establishes the pattern for all future features (Camps, Registrations, etc.)
