# Backend Implementation Plan: Family Units and Members Definition

## Overview

This plan implements a comprehensive family unit management system following **Test-Driven Development (TDD)** and **Vertical Slice Architecture**. The feature allows authenticated users to create and manage family units with family members, preparing the foundation for future camp registration functionality.

**Architecture Principles:**

- **Vertical Slice Architecture**: All feature code grouped by functionality in `Features/FamilyUnits/`
- **TDD Approach**: Write failing tests first, then implement minimum code to pass
- **Repository Pattern**: Abstract data access through interfaces
- **FluentValidation**: Centralized validation rules
- **Minimal APIs**: Lightweight endpoint definitions with `MapGroup()`

**Key Business Rules:**

- One family unit per user
- Representative is automatically created as first family member
- Medical notes and allergies encrypted at rest (RGPD compliance)
- Authorization: Representative-only access for modifications, Admin/Board can view all

---

## Architecture Context

### Feature Slice Location

```
src/Abuvi.API/Features/FamilyUnits/
```

### Files to Create

```
Features/FamilyUnits/
├── FamilyUnitsEndpoints.cs          # Minimal API endpoint definitions (10 endpoints)
├── FamilyUnitsModels.cs             # Request/Response DTOs, domain entities
├── FamilyUnitsService.cs            # Business logic
├── FamilyUnitsRepository.cs         # Data access interface + implementation
├── CreateFamilyUnitValidator.cs     # FluentValidation for CreateFamilyUnitRequest
├── UpdateFamilyUnitValidator.cs     # FluentValidation for UpdateFamilyUnitRequest
├── CreateFamilyMemberValidator.cs   # FluentValidation for CreateFamilyMemberRequest
└── UpdateFamilyMemberValidator.cs   # FluentValidation for UpdateFamilyMemberRequest
```

### Files to Modify

```
src/Abuvi.API/
├── Program.cs                                    # Register endpoints and services
└── Data/
    ├── Configurations/
    │   └── FamilyMemberConfiguration.cs          # Add new fields to entity config
    └── Migrations/
        └── [timestamp]_AddFamilyMemberAdditionalFields.cs  # New migration
```

### Test Files to Create

```
tests/Abuvi.Tests/Unit/Features/FamilyUnits/
├── FamilyUnitsServiceTests.cs                    # Service unit tests (36+ tests)
├── CreateFamilyUnitValidatorTests.cs             # Validator tests
├── UpdateFamilyUnitValidatorTests.cs             # Validator tests
├── CreateFamilyMemberValidatorTests.cs           # Validator tests
└── UpdateFamilyMemberValidatorTests.cs           # Validator tests
```

### Cross-Cutting Concerns

- **Encryption Service**: AES-256 encryption for medical notes and allergies
- **Authorization**: ClaimsPrincipal-based role and representative checks
- **Error Handling**: Global exception middleware (already exists)
- **Logging**: Structured logging with `ILogger<T>`

---

## Implementation Steps

### Step 0: Create Feature Branch

**Action**: Create and switch to feature branch following development workflow

**Branch Naming**: `feature/feat-family-units-definition-backend`

**Implementation Steps**:

1. Ensure you're on the latest `main` branch
2. Pull latest changes: `git pull origin main`
3. Create new branch: `git checkout -b feature/feat-family-units-definition-backend`
4. Verify branch creation: `git branch`

**Notes**: This must be the FIRST step before any code changes. Never work directly on `main` branch.

---

### Step 1: Update Entity Configuration and Create Migration

**File**: `src/Abuvi.API/Data/Configurations/FamilyMemberConfiguration.cs`

**Action**: Add new fields to FamilyMember entity configuration

**Implementation Steps**:

1. **Read existing FamilyMemberConfiguration.cs**
2. **Add new property configurations**:

   ```csharp
   // New fields
   builder.Property(m => m.DocumentNumber)
       .HasMaxLength(50);

   builder.Property(m => m.Email)
       .HasMaxLength(255);

   builder.Property(m => m.Phone)
       .HasMaxLength(20);
   ```

3. **Verify existing configurations** for:
   - `MedicalNotes` (max 2000, encrypted)
   - `Allergies` (max 1000, encrypted)
   - `Relationship` enum (string conversion, max 20)

4. **Update Relationship enum** in entity:

   ```csharp
   public enum FamilyRelationship
   {
       Parent,
       Child,
       Sibling,    // NEW
       Spouse,     // NEW
       Other
   }
   ```

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsModels.cs` (create this file first for enum)

**Action**: Define FamilyRelationship enum

**Implementation Steps**:

1. **Create new file**: `FamilyUnitsModels.cs`
2. **Add namespace**: `namespace Abuvi.API.Features.FamilyUnits;`
3. **Define enum**:

   ```csharp
   public enum FamilyRelationship
   {
       Parent,
       Child,
       Sibling,
       Spouse,
       Other
   }
   ```

**File**: `src/Abuvi.API/Data/Migrations/[timestamp]_AddFamilyMemberAdditionalFields.cs`

**Action**: Create EF Core migration for new fields

**Implementation Steps**:

1. **Run migration command**:

   ```bash
   dotnet ef migrations add AddFamilyMemberAdditionalFields --project src/Abuvi.API
   ```

2. **Review generated migration** to ensure it adds:
   - `DocumentNumber` VARCHAR(50) NULL
   - `Email` VARCHAR(255) NULL
   - `Phone` VARCHAR(20) NULL

3. **DO NOT apply migration yet** (will apply in Step 8 after all code is ready)

**Dependencies**:

- EF Core Tools: `dotnet tool install --global dotnet-ef` (already installed)

**Implementation Notes**:

- Migration will add nullable columns (no data migration needed)
- Existing FamilyMember records will have NULL values for new fields
- Relationship enum values stored as strings in database

---

### Step 2: Create Request/Response DTOs

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsModels.cs`

**Action**: Define all DTOs for family units and members

**Implementation Steps**:

1. **Add FamilyUnit DTOs**:

   ```csharp
   // Request DTOs
   public record CreateFamilyUnitRequest(string Name);

   public record UpdateFamilyUnitRequest(string Name);

   // Response DTO
   public record FamilyUnitResponse(
       Guid Id,
       string Name,
       Guid RepresentativeUserId,
       DateTime CreatedAt,
       DateTime UpdatedAt
   );
   ```

2. **Add FamilyMember DTOs**:

   ```csharp
   // Request DTOs
   public record CreateFamilyMemberRequest(
       string FirstName,
       string LastName,
       DateOnly DateOfBirth,
       FamilyRelationship Relationship,
       string? DocumentNumber = null,
       string? Email = null,
       string? Phone = null,
       string? MedicalNotes = null,
       string? Allergies = null
   );

   public record UpdateFamilyMemberRequest(
       string FirstName,
       string LastName,
       DateOnly DateOfBirth,
       FamilyRelationship Relationship,
       string? DocumentNumber = null,
       string? Email = null,
       string? Phone = null,
       string? MedicalNotes = null,
       string? Allergies = null
   );

   // Response DTO (NEVER expose encrypted fields)
   public record FamilyMemberResponse(
       Guid Id,
       Guid FamilyUnitId,
       Guid? UserId,
       string FirstName,
       string LastName,
       DateOnly DateOfBirth,
       FamilyRelationship Relationship,
       string? DocumentNumber,
       string? Email,
       string? Phone,
       bool HasMedicalNotes,    // Boolean flag only
       bool HasAllergies,       // Boolean flag only
       DateTime CreatedAt,
       DateTime UpdatedAt
   );
   ```

3. **Add extension methods for mapping**:

   ```csharp
   public static class FamilyUnitExtensions
   {
       public static FamilyUnitResponse ToResponse(this FamilyUnit unit)
           => new(
               unit.Id,
               unit.Name,
               unit.RepresentativeUserId,
               unit.CreatedAt,
               unit.UpdatedAt
           );
   }

   public static class FamilyMemberExtensions
   {
       public static FamilyMemberResponse ToResponse(this FamilyMember member)
           => new(
               member.Id,
               member.FamilyUnitId,
               member.UserId,
               member.FirstName,
               member.LastName,
               member.DateOfBirth,
               member.Relationship,
               member.DocumentNumber,
               member.Email,
               member.Phone,
               !string.IsNullOrEmpty(member.MedicalNotes),
               !string.IsNullOrEmpty(member.Allergies),
               member.CreatedAt,
               member.UpdatedAt
           );
   }
   ```

**Dependencies**:

- None (uses built-in C# record types)

**Implementation Notes**:

- Use `record` types for DTOs (immutable, value-based equality)
- Optional parameters with `= null` for nullable fields
- `DateOnly` for dates (not `DateTime`)
- Extension methods for clean mapping

---

### Step 3: Create FluentValidation Validators

#### Step 3.1: CreateFamilyUnitValidator

**File**: `src/Abuvi.API/Features/FamilyUnits/CreateFamilyUnitValidator.cs`

**Action**: Implement validation for CreateFamilyUnitRequest

**Implementation Steps**:

1. **Create validator class**:

   ```csharp
   namespace Abuvi.API.Features.FamilyUnits;

   using FluentValidation;

   public class CreateFamilyUnitValidator : AbstractValidator<CreateFamilyUnitRequest>
   {
       public CreateFamilyUnitValidator()
       {
           RuleFor(x => x.Name)
               .NotEmpty()
               .WithMessage("El nombre de la unidad familiar es obligatorio")
               .MaximumLength(200)
               .WithMessage("El nombre de la unidad familiar no puede exceder 200 caracteres");
       }
   }
   ```

**Implementation Notes**:

- **Spanish messages** for user-facing validation (as per backend-standards.mdc)
- Masculine gender: "El nombre es obligatorio" (el nombre = masculine)

#### Step 3.2: UpdateFamilyUnitValidator

**File**: `src/Abuvi.API/Features/FamilyUnits/UpdateFamilyUnitValidator.cs`

**Action**: Same validation as CreateFamilyUnitValidator

**Implementation Steps**: (same as CreateFamilyUnitValidator)

#### Step 3.3: CreateFamilyMemberValidator

**File**: `src/Abuvi.API/Features/FamilyUnits/CreateFamilyMemberValidator.cs`

**Action**: Implement comprehensive validation for CreateFamilyMemberRequest

**Implementation Steps**:

1. **Create validator class**:

   ```csharp
   namespace Abuvi.API.Features.FamilyUnits;

   using FluentValidation;

   public class CreateFamilyMemberValidator : AbstractValidator<CreateFamilyMemberRequest>
   {
       public CreateFamilyMemberValidator()
       {
           RuleFor(x => x.FirstName)
               .NotEmpty()
               .WithMessage("El nombre es obligatorio")
               .MaximumLength(100)
               .WithMessage("El nombre no puede exceder 100 caracteres");

           RuleFor(x => x.LastName)
               .NotEmpty()
               .WithMessage("Los apellidos son obligatorios")
               .MaximumLength(100)
               .WithMessage("Los apellidos no pueden exceder 100 caracteres");

           RuleFor(x => x.DateOfBirth)
               .NotEmpty()
               .WithMessage("La fecha de nacimiento es obligatoria")
               .LessThan(DateOnly.FromDateTime(DateTime.UtcNow))
               .WithMessage("La fecha de nacimiento debe ser una fecha pasada");

           RuleFor(x => x.Relationship)
               .IsInEnum()
               .WithMessage("Tipo de relación inválido");

           When(x => !string.IsNullOrEmpty(x.DocumentNumber), () =>
           {
               RuleFor(x => x.DocumentNumber)
                   .MaximumLength(50)
                   .WithMessage("El número de documento no puede exceder 50 caracteres")
                   .Matches("^[A-Z0-9]+$")
                   .WithMessage("El número de documento debe contener solo letras mayúsculas y números");
           });

           When(x => !string.IsNullOrEmpty(x.Email), () =>
           {
               RuleFor(x => x.Email)
                   .EmailAddress()
                   .WithMessage("Formato de correo electrónico inválido")
                   .MaximumLength(255)
                   .WithMessage("El correo electrónico no puede exceder 255 caracteres");
           });

           When(x => !string.IsNullOrEmpty(x.Phone), () =>
           {
               RuleFor(x => x.Phone)
                   .Matches(@"^\+[1-9]\d{1,14}$")
                   .WithMessage("El teléfono debe estar en formato E.164 (ej. +34612345678)")
                   .MaximumLength(20)
                   .WithMessage("El teléfono no puede exceder 20 caracteres");
           });

           When(x => !string.IsNullOrEmpty(x.MedicalNotes), () =>
           {
               RuleFor(x => x.MedicalNotes)
                   .MaximumLength(2000)
                   .WithMessage("Las notas médicas no pueden exceder 2000 caracteres");
           });

           When(x => !string.IsNullOrEmpty(x.Allergies), () =>
           {
               RuleFor(x => x.Allergies)
                   .MaximumLength(1000)
                   .WithMessage("Las alergias no pueden exceder 1000 caracteres");
           });
       }
   }
   ```

**Implementation Notes**:

- **Spanish messages** with correct gender agreement:
  - "El nombre es obligatorio" (masculine)
  - "La fecha es obligatoria" (feminine)
  - "Los apellidos son obligatorios" (plural)
- **Conditional validation**: Only validate optional fields when provided
- **Regex patterns**:
  - Document number: `^[A-Z0-9]+$` (uppercase alphanumeric)
  - Phone: `^\+[1-9]\d{1,14}$` (E.164 format)

#### Step 3.4: UpdateFamilyMemberValidator

**File**: `src/Abuvi.API/Features/FamilyUnits/UpdateFamilyMemberValidator.cs`

**Action**: Same validation as CreateFamilyMemberValidator

**Dependencies**:

- FluentValidation NuGet package (already installed)

---

### Step 4: TDD Phase 1 - Write Tests for FamilyUnitsService (Family Unit CRUD)

**CRITICAL**: Following TDD, we write tests BEFORE implementing the service.

**File**: `tests/Abuvi.Tests/Unit/Features/FamilyUnits/FamilyUnitsServiceTests.cs`

**Action**: Write comprehensive unit tests for family unit operations

**Test Structure** (AAA Pattern: Arrange, Act, Assert):

**Implementation Steps**:

1. **Create test class with setup**:

   ```csharp
   namespace Abuvi.Tests.Unit.Features.FamilyUnits;

   using Abuvi.API.Features.FamilyUnits;
   using FluentAssertions;
   using NSubstitute;
   using Xunit;

   public class FamilyUnitsServiceTests
   {
       private readonly IFamilyUnitsRepository _repository;
       private readonly ILogger<FamilyUnitsService> _logger;
       private readonly FamilyUnitsService _sut;  // System Under Test

       public FamilyUnitsServiceTests()
       {
           _repository = Substitute.For<IFamilyUnitsRepository>();
           _logger = Substitute.For<ILogger<FamilyUnitsService>>();
           _sut = new FamilyUnitsService(_repository, _logger);
       }
   }
   ```

2. **Write tests for CreateFamilyUnitAsync**:

```csharp
[Fact]
public async Task CreateFamilyUnitAsync_WhenValidRequest_CreatesFamilyUnitAndRepresentativeMember()
{
    // Arrange
    var userId = Guid.NewGuid();
    var user = new User { Id = userId, FirstName = "Juan", LastName = "Garcia", FamilyUnitId = null };
    var request = new CreateFamilyUnitRequest("Garcia Family");

    _repository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
        .Returns(user);

    // Act
    var result = await _sut.CreateFamilyUnitAsync(userId, request, CancellationToken.None);

    // Assert
    result.Should().NotBeNull();
    result.Name.Should().Be("Garcia Family");
    result.RepresentativeUserId.Should().Be(userId);

    // Verify repository calls
    await _repository.Received(1).CreateFamilyUnitAsync(
        Arg.Is<FamilyUnit>(fu => fu.Name == "Garcia Family" && fu.RepresentativeUserId == userId),
        Arg.Any<CancellationToken>());

    await _repository.Received(1).CreateFamilyMemberAsync(
        Arg.Is<FamilyMember>(fm =>
            fm.FirstName == "Juan" &&
            fm.LastName == "Garcia" &&
            fm.UserId == userId &&
            fm.Relationship == FamilyRelationship.Parent),
        Arg.Any<CancellationToken>());

    await _repository.Received(1).UpdateUserFamilyUnitIdAsync(userId, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
}

[Fact]
public async Task CreateFamilyUnitAsync_WhenUserAlreadyHasFamilyUnit_ThrowsBusinessRuleException()
{
    // Arrange
    var userId = Guid.NewGuid();
    var existingFamilyUnitId = Guid.NewGuid();
    var user = new User { Id = userId, FirstName = "Juan", LastName = "Garcia", FamilyUnitId = existingFamilyUnitId };
    var request = new CreateFamilyUnitRequest("Garcia Family");

    _repository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
        .Returns(user);

    // Act
    var act = async () => await _sut.CreateFamilyUnitAsync(userId, request, CancellationToken.None);

    // Assert
    await act.Should().ThrowAsync<BusinessRuleException>()
        .WithMessage("Ya tienes una unidad familiar");
}

[Fact]
public async Task CreateFamilyUnitAsync_WhenUserNotFound_ThrowsNotFoundException()
{
    // Arrange
    var userId = Guid.NewGuid();
    var request = new CreateFamilyUnitRequest("Garcia Family");

    _repository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
        .Returns((User?)null);

    // Act
    var act = async () => await _sut.CreateFamilyUnitAsync(userId, request, CancellationToken.None);

    // Assert
    await act.Should().ThrowAsync<NotFoundException>()
        .WithMessage($"No se encontró Usuario con ID '{userId}'");
}
```

1. **Write tests for GetFamilyUnitByIdAsync**:

```csharp
[Fact]
public async Task GetFamilyUnitByIdAsync_WhenFamilyUnitExists_ReturnsFamilyUnit()
{
    // Arrange
    var familyUnitId = Guid.NewGuid();
    var userId = Guid.NewGuid();
    var familyUnit = new FamilyUnit
    {
        Id = familyUnitId,
        Name = "Garcia Family",
        RepresentativeUserId = userId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
        .Returns(familyUnit);

    // Act
    var result = await _sut.GetFamilyUnitByIdAsync(familyUnitId, CancellationToken.None);

    // Assert
    result.Should().NotBeNull();
    result.Id.Should().Be(familyUnitId);
    result.Name.Should().Be("Garcia Family");
    result.RepresentativeUserId.Should().Be(userId);
}

[Fact]
public async Task GetFamilyUnitByIdAsync_WhenFamilyUnitNotFound_ThrowsNotFoundException()
{
    // Arrange
    var familyUnitId = Guid.NewGuid();

    _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
        .Returns((FamilyUnit?)null);

    // Act
    var act = async () => await _sut.GetFamilyUnitByIdAsync(familyUnitId, CancellationToken.None);

    // Assert
    await act.Should().ThrowAsync<NotFoundException>()
        .WithMessage($"No se encontró Unidad Familiar con ID '{familyUnitId}'");
}
```

1. **Write tests for UpdateFamilyUnitAsync, DeleteFamilyUnitAsync** (similar pattern)

2. **Write tests for authorization helpers**:

```csharp
[Fact]
public async Task IsRepresentativeAsync_WhenUserIsRepresentative_ReturnsTrue()
{
    // Arrange
    var userId = Guid.NewGuid();
    var familyUnitId = Guid.NewGuid();
    var familyUnit = new FamilyUnit { Id = familyUnitId, RepresentativeUserId = userId };

    _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
        .Returns(familyUnit);

    // Act
    var result = await _sut.IsRepresentativeAsync(familyUnitId, userId, CancellationToken.None);

    // Assert
    result.Should().BeTrue();
}

[Fact]
public async Task IsRepresentativeAsync_WhenUserIsNotRepresentative_ReturnsFalse()
{
    // Arrange
    var userId = Guid.NewGuid();
    var otherUserId = Guid.NewGuid();
    var familyUnitId = Guid.NewGuid();
    var familyUnit = new FamilyUnit { Id = familyUnitId, RepresentativeUserId = otherUserId };

    _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
        .Returns(familyUnit);

    // Act
    var result = await _sut.IsRepresentativeAsync(familyUnitId, userId, CancellationToken.None);

    // Assert
    result.Should().BeFalse();
}
```

**Test Count (Family Unit CRUD)**: ~10-12 tests

**Dependencies**:

- xUnit
- FluentAssertions
- NSubstitute
- (All already installed)

---

### Step 5: Implement FamilyUnitsRepository Interface and Implementation

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsRepository.cs`

**Action**: Define repository interface and EF Core implementation

**Implementation Steps**:

1. **Define repository interface**:

   ```csharp
   namespace Abuvi.API.Features.FamilyUnits;

   using Abuvi.API.Data;
   using Microsoft.EntityFrameworkCore;

   public interface IFamilyUnitsRepository
   {
       // Family Unit operations
       Task<FamilyUnit?> GetFamilyUnitByIdAsync(Guid id, CancellationToken ct);
       Task<FamilyUnit?> GetFamilyUnitByRepresentativeIdAsync(Guid userId, CancellationToken ct);
       Task CreateFamilyUnitAsync(FamilyUnit familyUnit, CancellationToken ct);
       Task UpdateFamilyUnitAsync(FamilyUnit familyUnit, CancellationToken ct);
       Task DeleteFamilyUnitAsync(Guid id, CancellationToken ct);

       // Family Member operations
       Task<FamilyMember?> GetFamilyMemberByIdAsync(Guid id, CancellationToken ct);
       Task<IReadOnlyList<FamilyMember>> GetFamilyMembersByFamilyUnitIdAsync(Guid familyUnitId, CancellationToken ct);
       Task CreateFamilyMemberAsync(FamilyMember member, CancellationToken ct);
       Task UpdateFamilyMemberAsync(FamilyMember member, CancellationToken ct);
       Task DeleteFamilyMemberAsync(Guid id, CancellationToken ct);

       // User operations
       Task<User?> GetUserByIdAsync(Guid id, CancellationToken ct);
       Task UpdateUserFamilyUnitIdAsync(Guid userId, Guid? familyUnitId, CancellationToken ct);
   }
   ```

2. **Implement repository**:

   ```csharp
   public class FamilyUnitsRepository(AbuviDbContext db) : IFamilyUnitsRepository
   {
       // Family Unit operations
       public async Task<FamilyUnit?> GetFamilyUnitByIdAsync(Guid id, CancellationToken ct)
           => await db.FamilyUnits
               .AsNoTracking()
               .FirstOrDefaultAsync(fu => fu.Id == id, ct);

       public async Task<FamilyUnit?> GetFamilyUnitByRepresentativeIdAsync(Guid userId, CancellationToken ct)
           => await db.FamilyUnits
               .AsNoTracking()
               .FirstOrDefaultAsync(fu => fu.RepresentativeUserId == userId, ct);

       public async Task CreateFamilyUnitAsync(FamilyUnit familyUnit, CancellationToken ct)
       {
           db.FamilyUnits.Add(familyUnit);
           await db.SaveChangesAsync(ct);
       }

       public async Task UpdateFamilyUnitAsync(FamilyUnit familyUnit, CancellationToken ct)
       {
           familyUnit.UpdatedAt = DateTime.UtcNow;
           db.FamilyUnits.Update(familyUnit);
           await db.SaveChangesAsync(ct);
       }

       public async Task DeleteFamilyUnitAsync(Guid id, CancellationToken ct)
       {
           await db.FamilyUnits.Where(fu => fu.Id == id).ExecuteDeleteAsync(ct);
       }

       // Family Member operations
       public async Task<FamilyMember?> GetFamilyMemberByIdAsync(Guid id, CancellationToken ct)
           => await db.FamilyMembers
               .AsNoTracking()
               .FirstOrDefaultAsync(fm => fm.Id == id, ct);

       public async Task<IReadOnlyList<FamilyMember>> GetFamilyMembersByFamilyUnitIdAsync(
           Guid familyUnitId, CancellationToken ct)
           => await db.FamilyMembers
               .AsNoTracking()
               .Where(fm => fm.FamilyUnitId == familyUnitId)
               .OrderBy(fm => fm.CreatedAt)
               .ToListAsync(ct);

       public async Task CreateFamilyMemberAsync(FamilyMember member, CancellationToken ct)
       {
           db.FamilyMembers.Add(member);
           await db.SaveChangesAsync(ct);
       }

       public async Task UpdateFamilyMemberAsync(FamilyMember member, CancellationToken ct)
       {
           member.UpdatedAt = DateTime.UtcNow;
           db.FamilyMembers.Update(member);
           await db.SaveChangesAsync(ct);
       }

       public async Task DeleteFamilyMemberAsync(Guid id, CancellationToken ct)
       {
           await db.FamilyMembers.Where(fm => fm.Id == id).ExecuteDeleteAsync(ct);
       }

       // User operations
       public async Task<User?> GetUserByIdAsync(Guid id, CancellationToken ct)
           => await db.Users
               .AsNoTracking()
               .FirstOrDefaultAsync(u => u.Id == id, ct);

       public async Task UpdateUserFamilyUnitIdAsync(Guid userId, Guid? familyUnitId, CancellationToken ct)
       {
           await db.Users
               .Where(u => u.Id == userId)
               .ExecuteUpdateAsync(setters => setters
                   .SetProperty(u => u.FamilyUnitId, familyUnitId)
                   .SetProperty(u => u.UpdatedAt, DateTime.UtcNow), ct);
       }
   }
   ```

**Implementation Notes**:

- Use `AsNoTracking()` for read-only operations (performance)
- Use `ExecuteDeleteAsync()` for efficient deletes (EF Core 7+)
- Use `ExecuteUpdateAsync()` for efficient updates without loading entity
- Primary constructor pattern: `(AbuviDbContext db)`
- Automatic `UpdatedAt` timestamp on updates

**Dependencies**:

- EF Core 9.0 (already installed)
- Npgsql.EntityFrameworkCore.PostgreSQL (already installed)

---

### Step 6: Implement FamilyUnitsService with Business Logic

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs`

**Action**: Implement business logic to make tests pass

**Implementation Steps**:

1. **Create service class**:

   ```csharp
   namespace Abuvi.API.Features.FamilyUnits;

   using Abuvi.API.Common.Exceptions;
   using Microsoft.Extensions.Logging;

   public class FamilyUnitsService(
       IFamilyUnitsRepository repository,
       IEncryptionService encryptionService,
       ILogger<FamilyUnitsService> logger)
   {
       // Family Unit CRUD
       public async Task<FamilyUnitResponse> CreateFamilyUnitAsync(
           Guid userId, CreateFamilyUnitRequest request, CancellationToken ct)
       {
           // Get user
           var user = await repository.GetUserByIdAsync(userId, ct)
               ?? throw new NotFoundException("Usuario", userId);

           // Check if user already has a family unit
           if (user.FamilyUnitId is not null)
           {
               logger.LogWarning("User {UserId} attempted to create second family unit", userId);
               throw new BusinessRuleException("Ya tienes una unidad familiar");
           }

           // Create family unit
           var familyUnit = new FamilyUnit
           {
               Id = Guid.NewGuid(),
               Name = request.Name,
               RepresentativeUserId = userId,
               CreatedAt = DateTime.UtcNow,
               UpdatedAt = DateTime.UtcNow
           };

           await repository.CreateFamilyUnitAsync(familyUnit, ct);

           // Update user's familyUnitId
           await repository.UpdateUserFamilyUnitIdAsync(userId, familyUnit.Id, ct);

           // Automatically create representative as family member
           var representativeMember = new FamilyMember
           {
               Id = Guid.NewGuid(),
               FamilyUnitId = familyUnit.Id,
               UserId = userId,
               FirstName = user.FirstName,
               LastName = user.LastName,
               DateOfBirth = DateOnly.MinValue, // User should update
               Relationship = FamilyRelationship.Parent,
               CreatedAt = DateTime.UtcNow,
               UpdatedAt = DateTime.UtcNow
           };

           await repository.CreateFamilyMemberAsync(representativeMember, ct);

           logger.LogInformation(
               "Family unit {FamilyUnitId} created by user {UserId} with representative member {MemberId}",
               familyUnit.Id, userId, representativeMember.Id);

           return familyUnit.ToResponse();
       }

       public async Task<FamilyUnitResponse> GetFamilyUnitByIdAsync(Guid id, CancellationToken ct)
       {
           var familyUnit = await repository.GetFamilyUnitByIdAsync(id, ct)
               ?? throw new NotFoundException("Unidad Familiar", id);

           return familyUnit.ToResponse();
       }

       public async Task<FamilyUnitResponse> GetCurrentUserFamilyUnitAsync(Guid userId, CancellationToken ct)
       {
           var familyUnit = await repository.GetFamilyUnitByRepresentativeIdAsync(userId, ct)
               ?? throw new NotFoundException("No se encontró unidad familiar para el usuario actual");

           return familyUnit.ToResponse();
       }

       public async Task<FamilyUnitResponse> UpdateFamilyUnitAsync(
           Guid id, UpdateFamilyUnitRequest request, CancellationToken ct)
       {
           var familyUnit = await repository.GetFamilyUnitByIdAsync(id, ct)
               ?? throw new NotFoundException("Unidad Familiar", id);

           familyUnit.Name = request.Name;
           await repository.UpdateFamilyUnitAsync(familyUnit, ct);

           logger.LogInformation("Family unit {FamilyUnitId} updated", id);

           return familyUnit.ToResponse();
       }

       public async Task DeleteFamilyUnitAsync(Guid id, CancellationToken ct)
       {
           var familyUnit = await repository.GetFamilyUnitByIdAsync(id, ct)
               ?? throw new NotFoundException("Unidad Familiar", id);

           // Delete family unit (cascade deletes members)
           await repository.DeleteFamilyUnitAsync(id, ct);

           // Clear user's familyUnitId
           await repository.UpdateUserFamilyUnitIdAsync(familyUnit.RepresentativeUserId, null, ct);

           logger.LogInformation("Family unit {FamilyUnitId} deleted", id);
       }

       // Authorization helpers
       public async Task<bool> IsRepresentativeAsync(Guid familyUnitId, Guid userId, CancellationToken ct)
       {
           var familyUnit = await repository.GetFamilyUnitByIdAsync(familyUnitId, ct);
           return familyUnit?.RepresentativeUserId == userId;
       }

       // Family Member CRUD (implement similarly, see full implementation below)
   }
   ```

2. **Add Family Member operations** (similar pattern):
   - `CreateFamilyMemberAsync`: Encrypt medical notes/allergies before saving
   - `GetFamilyMembersByFamilyUnitIdAsync`: Return list with boolean flags
   - `GetFamilyMemberByIdAsync`: Return single member
   - `UpdateFamilyMemberAsync`: Encrypt medical notes/allergies before saving
   - `DeleteFamilyMemberAsync`: Check not deleting representative's own record

3. **Implement encryption handling**:

   ```csharp
   public async Task<FamilyMemberResponse> CreateFamilyMemberAsync(
       Guid familyUnitId, CreateFamilyMemberRequest request, CancellationToken ct)
   {
       // Verify family unit exists
       var familyUnit = await repository.GetFamilyUnitByIdAsync(familyUnitId, ct)
           ?? throw new NotFoundException("Unidad Familiar", familyUnitId);

       // Encrypt sensitive data
       var encryptedMedicalNotes = !string.IsNullOrEmpty(request.MedicalNotes)
           ? encryptionService.Encrypt(request.MedicalNotes)
           : null;

       var encryptedAllergies = !string.IsNullOrEmpty(request.Allergies)
           ? encryptionService.Encrypt(request.Allergies)
           : null;

       // Create family member
       var member = new FamilyMember
       {
           Id = Guid.NewGuid(),
           FamilyUnitId = familyUnitId,
           UserId = null,
           FirstName = request.FirstName,
           LastName = request.LastName,
           DateOfBirth = request.DateOfBirth,
           Relationship = request.Relationship,
           DocumentNumber = request.DocumentNumber?.ToUpperInvariant(),
           Email = request.Email,
           Phone = request.Phone,
           MedicalNotes = encryptedMedicalNotes,
           Allergies = encryptedAllergies,
           CreatedAt = DateTime.UtcNow,
           UpdatedAt = DateTime.UtcNow
       };

       await repository.CreateFamilyMemberAsync(member, ct);

       logger.LogInformation(
           "Family member {MemberId} created in family unit {FamilyUnitId}",
           member.Id, familyUnitId);

       return member.ToResponse();
   }
   ```

**Implementation Notes**:

- **Encryption**: Use `IEncryptionService` for medical notes and allergies
- **Uppercase transformation**: DocumentNumber always uppercase
- **Authorization**: Check representative before delete
- **Logging**: Structured logging with parameters
- **Spanish error messages**: All user-facing exceptions in Spanish

**Dependencies**:

- `IEncryptionService` (create in Common/Services/)

---

### Step 7: Create Encryption Service

**File**: `src/Abuvi.API/Common/Services/EncryptionService.cs`

**Action**: Implement AES-256 encryption for sensitive data

**Implementation Steps**:

1. **Create interface**:

   ```csharp
   namespace Abuvi.API.Common.Services;

   public interface IEncryptionService
   {
       string Encrypt(string plainText);
       string Decrypt(string cipherText);
   }
   ```

2. **Implement service**:

   ```csharp
   using System.Security.Cryptography;
   using System.Text;

   public class EncryptionService : IEncryptionService
   {
       private readonly byte[] _key;
       private readonly byte[] _iv;

       public EncryptionService(IConfiguration configuration)
       {
           var encryptionKey = configuration["Encryption:Key"]
               ?? throw new InvalidOperationException("Encryption key not configured");

           // Derive 256-bit key from configuration string
           using var sha256 = SHA256.Create();
           _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(encryptionKey));
           _iv = new byte[16]; // 128-bit IV (zeros for simplicity, should be random in production)
       }

       public string Encrypt(string plainText)
       {
           if (string.IsNullOrEmpty(plainText))
               return plainText;

           using var aes = Aes.Create();
           aes.Key = _key;
           aes.IV = _iv;

           using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
           using var ms = new MemoryStream();
           using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
           using (var sw = new StreamWriter(cs))
           {
               sw.Write(plainText);
           }

           return Convert.ToBase64String(ms.ToArray());
       }

       public string Decrypt(string cipherText)
       {
           if (string.IsNullOrEmpty(cipherText))
               return cipherText;

           using var aes = Aes.Create();
           aes.Key = _key;
           aes.IV = _iv;

           using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
           using var ms = new MemoryStream(Convert.FromBase64String(cipherText));
           using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
           using var sr = new StreamReader(cs);

           return sr.ReadToEnd();
       }
   }
   ```

3. **Add configuration** (user secrets for development):

   ```bash
   dotnet user-secrets set "Encryption:Key" "your-secret-encryption-key-min-32-chars" --project src/Abuvi.API
   ```

**Implementation Notes**:

- **AES-256**: Industry standard encryption
- **Key Management**: Store key in Azure Key Vault for production
- **IV**: Use zeros for simplicity (should be random + stored with ciphertext in production)
- **Base64 encoding**: For database storage

**Dependencies**:

- System.Security.Cryptography (built-in)

---

### Step 8: TDD Phase 2 - Write Tests for Family Member Operations

**File**: `tests/Abuvi.Tests/Unit/Features/FamilyUnits/FamilyUnitsServiceTests.cs`

**Action**: Add tests for family member CRUD operations

**Implementation Steps**:

1. **Add encryption service mock to test setup**:

   ```csharp
   private readonly IEncryptionService _encryptionService;

   public FamilyUnitsServiceTests()
   {
       _repository = Substitute.For<IFamilyUnitsRepository>();
       _encryptionService = Substitute.For<IEncryptionService>();
       _logger = Substitute.For<ILogger<FamilyUnitsService>>();
       _sut = new FamilyUnitsService(_repository, _encryptionService, _logger);

       // Setup encryption mock
       _encryptionService.Encrypt(Arg.Any<string>())
           .Returns(x => $"ENCRYPTED_{x[0]}");
       _encryptionService.Decrypt(Arg.Any<string>())
           .Returns(x => x.ToString()!.Replace("ENCRYPTED_", ""));
   }
   ```

2. **Write tests for CreateFamilyMemberAsync**:

```csharp
[Fact]
public async Task CreateFamilyMemberAsync_WhenValidRequest_CreatesFamilyMember()
{
    // Arrange
    var familyUnitId = Guid.NewGuid();
    var familyUnit = new FamilyUnit { Id = familyUnitId, Name = "Test Family" };
    var request = new CreateFamilyMemberRequest(
        "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child,
        "12345678A", "maria@example.com", "+34612345678",
        "Asthma", "Peanuts");

    _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
        .Returns(familyUnit);

    // Act
    var result = await _sut.CreateFamilyMemberAsync(familyUnitId, request, CancellationToken.None);

    // Assert
    result.Should().NotBeNull();
    result.FirstName.Should().Be("Maria");
    result.LastName.Should().Be("Garcia");
    result.DocumentNumber.Should().Be("12345678A");
    result.HasMedicalNotes.Should().BeTrue();
    result.HasAllergies.Should().BeTrue();

    // Verify encryption was called
    _encryptionService.Received(1).Encrypt("Asthma");
    _encryptionService.Received(1).Encrypt("Peanuts");

    // Verify repository call
    await _repository.Received(1).CreateFamilyMemberAsync(
        Arg.Is<FamilyMember>(fm =>
            fm.FirstName == "Maria" &&
            fm.MedicalNotes == "ENCRYPTED_Asthma" &&
            fm.Allergies == "ENCRYPTED_Peanuts"),
        Arg.Any<CancellationToken>());
}

[Fact]
public async Task CreateFamilyMemberAsync_WhenFamilyUnitNotFound_ThrowsNotFoundException()
{
    // Arrange
    var familyUnitId = Guid.NewGuid();
    var request = new CreateFamilyMemberRequest(
        "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child);

    _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
        .Returns((FamilyUnit?)null);

    // Act
    var act = async () => await _sut.CreateFamilyMemberAsync(familyUnitId, request, CancellationToken.None);

    // Assert
    await act.Should().ThrowAsync<NotFoundException>();
}

[Fact]
public async Task CreateFamilyMemberAsync_WhenDocumentNumberProvided_ConvertsToUppercase()
{
    // Arrange
    var familyUnitId = Guid.NewGuid();
    var familyUnit = new FamilyUnit { Id = familyUnitId };
    var request = new CreateFamilyMemberRequest(
        "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child,
        DocumentNumber: "abc123xyz"); // lowercase

    _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
        .Returns(familyUnit);

    // Act
    var result = await _sut.CreateFamilyMemberAsync(familyUnitId, request, CancellationToken.None);

    // Assert
    result.DocumentNumber.Should().Be("ABC123XYZ"); // uppercase

    await _repository.Received(1).CreateFamilyMemberAsync(
        Arg.Is<FamilyMember>(fm => fm.DocumentNumber == "ABC123XYZ"),
        Arg.Any<CancellationToken>());
}
```

1. **Write tests for GetFamilyMembersByFamilyUnitIdAsync, UpdateFamilyMemberAsync, DeleteFamilyMemberAsync**

2. **Write test for preventing representative deletion**:

```csharp
[Fact]
public async Task DeleteFamilyMemberAsync_WhenDeletingRepresentativeOwnRecord_ThrowsBusinessRuleException()
{
    // Arrange
    var userId = Guid.NewGuid();
    var familyUnitId = Guid.NewGuid();
    var memberId = Guid.NewGuid();

    var member = new FamilyMember
    {
        Id = memberId,
        FamilyUnitId = familyUnitId,
        UserId = userId
    };

    var familyUnit = new FamilyUnit
    {
        Id = familyUnitId,
        RepresentativeUserId = userId
    };

    _repository.GetFamilyMemberByIdAsync(memberId, Arg.Any<CancellationToken>())
        .Returns(member);
    _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
        .Returns(familyUnit);

    // Act
    var act = async () => await _sut.DeleteFamilyMemberAsync(memberId, CancellationToken.None);

    // Assert
    await act.Should().ThrowAsync<BusinessRuleException>()
        .WithMessage("No puedes eliminar tu propio perfil mientras seas representante");
}
```

**Test Count (Family Member CRUD)**: ~15-20 tests

---

### Step 9: Create Minimal API Endpoints

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs`

**Action**: Define all 10 API endpoints with authorization

**Implementation Steps**:

1. **Create endpoint class**:

   ```csharp
   namespace Abuvi.API.Features.FamilyUnits;

   using Abuvi.API.Common.Models;
   using Microsoft.AspNetCore.Http.HttpResults;
   using System.Security.Claims;

   public static class FamilyUnitsEndpoints
   {
       public static void MapFamilyUnitsEndpoints(this IEndpointRouteBuilder app)
       {
           var group = app.MapGroup("/api/family-units")
               .WithTags("Family Units")
               .RequireAuthorization();

           // Family Unit endpoints
           group.MapPost("/", CreateFamilyUnit)
               .WithName("CreateFamilyUnit")
               .Produces<ApiResponse<FamilyUnitResponse>>(StatusCodes.Status201Created)
               .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
               .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

           group.MapGet("/me", GetCurrentUserFamilyUnit)
               .WithName("GetCurrentUserFamilyUnit")
               .Produces<ApiResponse<FamilyUnitResponse>>()
               .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

           group.MapGet("/{id:guid}", GetFamilyUnitById)
               .WithName("GetFamilyUnitById")
               .Produces<ApiResponse<FamilyUnitResponse>>()
               .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
               .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

           group.MapPut("/{id:guid}", UpdateFamilyUnit)
               .WithName("UpdateFamilyUnit")
               .Produces<ApiResponse<FamilyUnitResponse>>()
               .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
               .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
               .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

           group.MapDelete("/{id:guid}", DeleteFamilyUnit)
               .WithName("DeleteFamilyUnit")
               .Produces(StatusCodes.Status204NoContent)
               .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
               .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

           // Family Member endpoints
           group.MapPost("/{familyUnitId:guid}/members", CreateFamilyMember)
               .WithName("CreateFamilyMember")
               .Produces<ApiResponse<FamilyMemberResponse>>(StatusCodes.Status201Created)
               .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
               .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden);

           group.MapGet("/{familyUnitId:guid}/members", GetFamilyMembers)
               .WithName("GetFamilyMembers")
               .Produces<ApiResponse<IReadOnlyList<FamilyMemberResponse>>>()
               .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
               .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

           group.MapGet("/{familyUnitId:guid}/members/{memberId:guid}", GetFamilyMemberById)
               .WithName("GetFamilyMemberById")
               .Produces<ApiResponse<FamilyMemberResponse>>()
               .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
               .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

           group.MapPut("/{familyUnitId:guid}/members/{memberId:guid}", UpdateFamilyMember)
               .WithName("UpdateFamilyMember")
               .Produces<ApiResponse<FamilyMemberResponse>>()
               .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
               .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
               .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

           group.MapDelete("/{familyUnitId:guid}/members/{memberId:guid}", DeleteFamilyMember)
               .WithName("DeleteFamilyMember")
               .Produces(StatusCodes.Status204NoContent)
               .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
               .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
               .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);
       }
   }
   ```

2. **Implement endpoint handlers with authorization**:

```csharp
private static async Task<Results<Created<ApiResponse<FamilyUnitResponse>>, BadRequest<ApiResponse<object>>, Conflict<ApiResponse<object>>>>
    CreateFamilyUnit(
        CreateFamilyUnitRequest request,
        FamilyUnitsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
{
    var userId = Guid.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    try
    {
        var result = await service.CreateFamilyUnitAsync(userId, request, ct);
        return TypedResults.Created(
            $"/api/family-units/{result.Id}",
            ApiResponse<FamilyUnitResponse>.Ok(result));
    }
    catch (BusinessRuleException ex)
    {
        return TypedResults.Conflict(
            ApiResponse<object>.Fail(ex.Message, "FAMILY_UNIT_EXISTS"));
    }
}

private static async Task<Results<Ok<ApiResponse<FamilyUnitResponse>>, NotFound<ApiResponse<object>>>>
    GetCurrentUserFamilyUnit(
        FamilyUnitsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
{
    var userId = Guid.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    try
    {
        var result = await service.GetCurrentUserFamilyUnitAsync(userId, ct);
        return TypedResults.Ok(ApiResponse<FamilyUnitResponse>.Ok(result));
    }
    catch (NotFoundException ex)
    {
        return TypedResults.NotFound(
            ApiResponse<object>.NotFound(ex.Message));
    }
}

private static async Task<Results<Ok<ApiResponse<FamilyUnitResponse>>, Forbidden<ApiResponse<object>>, NotFound<ApiResponse<object>>>>
    GetFamilyUnitById(
        Guid id,
        FamilyUnitsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
{
    var userId = Guid.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var userRole = user.FindFirst(ClaimTypes.Role)!.Value;

    try
    {
        var result = await service.GetFamilyUnitByIdAsync(id, ct);

        // Authorization: Representative OR Admin/Board
        var isRepresentative = result.RepresentativeUserId == userId;
        var isAdminOrBoard = userRole == "Admin" || userRole == "Board";

        if (!isRepresentative && !isAdminOrBoard)
        {
            return TypedResults.Forbidden(
                ApiResponse<object>.Fail(
                    "No tienes permiso para realizar esta acción",
                    "FORBIDDEN"));
        }

        return TypedResults.Ok(ApiResponse<FamilyUnitResponse>.Ok(result));
    }
    catch (NotFoundException ex)
    {
        return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
}

// Similar pattern for Update, Delete, and all Family Member endpoints
```

**Implementation Notes**:

- **ClaimsPrincipal**: Extract userId from JWT claims
- **Authorization**: Check representative OR admin/board role
- **Spanish error messages**: All user-facing messages in Spanish
- **TypedResults**: Strongly-typed HTTP results (ASP.NET Core 7+)
- **ApiResponse wrapper**: Consistent response format

---

### Step 10: Register Services and Endpoints in Program.cs

**File**: `src/Abuvi.API/Program.cs`

**Action**: Register all services and endpoints

**Implementation Steps**:

1. **Register repository and service**:

   ```csharp
   // In the services section (after builder.Services.AddDbContext...)

   // Family Units feature
   builder.Services.AddScoped<IFamilyUnitsRepository, FamilyUnitsRepository>();
   builder.Services.AddScoped<FamilyUnitsService>();

   // Encryption service
   builder.Services.AddSingleton<IEncryptionService, EncryptionService>();

   // FluentValidation validators (auto-registered from assembly)
   builder.Services.AddValidatorsFromAssemblyContaining<Program>();
   ```

2. **Map endpoints**:

   ```csharp
   // After app.UseAuthorization();

   app.MapFamilyUnitsEndpoints();
   ```

**Implementation Notes**:

- **Scoped**: Repository and Service (per-request lifetime)
- **Singleton**: EncryptionService (single instance)
- **Auto-registration**: FluentValidation finds all validators in assembly

---

### Step 11: Apply EF Core Migration

**Action**: Apply database migration to add new fields

**Implementation Steps**:

1. **Verify migration exists**:

   ```bash
   dotnet ef migrations list --project src/Abuvi.API
   ```

2. **Apply migration**:

   ```bash
   dotnet ef database update --project src/Abuvi.API
   ```

3. **Verify schema changes**:
   - Connect to PostgreSQL
   - Check `FamilyMembers` table has new columns: `DocumentNumber`, `Email`, `Phone`

**Implementation Notes**:

- Migration was created in Step 1
- Applies schema changes to database
- Idempotent (safe to run multiple times)

---

### Step 12: Write Validator Unit Tests

**File**: `tests/Abuvi.Tests/Unit/Features/FamilyUnits/CreateFamilyMemberValidatorTests.cs`

**Action**: Test all validation rules

**Implementation Steps**:

1. **Create validator test class**:

   ```csharp
   namespace Abuvi.Tests.Unit.Features.FamilyUnits;

   using Abuvi.API.Features.FamilyUnits;
   using FluentValidation.TestHelper;
   using Xunit;

   public class CreateFamilyMemberValidatorTests
   {
       private readonly CreateFamilyMemberValidator _validator = new();

       [Fact]
       public void Validate_WhenFirstNameEmpty_ShouldHaveValidationError()
       {
           var request = new CreateFamilyMemberRequest(
               "", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child);

           var result = _validator.TestValidate(request);

           result.ShouldHaveValidationErrorFor(x => x.FirstName)
               .WithErrorMessage("El nombre es obligatorio");
       }

       [Fact]
       public void Validate_WhenDateOfBirthInFuture_ShouldHaveValidationError()
       {
           var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
           var request = new CreateFamilyMemberRequest(
               "Maria", "Garcia", futureDate, FamilyRelationship.Child);

           var result = _validator.TestValidate(request);

           result.ShouldHaveValidationErrorFor(x => x.DateOfBirth)
               .WithErrorMessage("La fecha de nacimiento debe ser una fecha pasada");
       }

       [Fact]
       public void Validate_WhenDocumentNumberHasLowercase_ShouldHaveValidationError()
       {
           var request = new CreateFamilyMemberRequest(
               "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child,
               DocumentNumber: "abc123");

           var result = _validator.TestValidate(request);

           result.ShouldHaveValidationErrorFor(x => x.DocumentNumber);
       }

       [Fact]
       public void Validate_WhenDocumentNumberIsUppercaseAlphanumeric_ShouldNotHaveValidationError()
       {
           var request = new CreateFamilyMemberRequest(
               "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child,
               DocumentNumber: "ABC123");

           var result = _validator.TestValidate(request);

           result.ShouldNotHaveValidationErrorFor(x => x.DocumentNumber);
       }

       [Fact]
       public void Validate_WhenEmailInvalid_ShouldHaveValidationError()
       {
           var request = new CreateFamilyMemberRequest(
               "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child,
               Email: "invalid-email");

           var result = _validator.TestValidate(request);

           result.ShouldHaveValidationErrorFor(x => x.Email)
               .WithErrorMessage("Formato de correo electrónico inválido");
       }

       [Fact]
       public void Validate_WhenPhoneNotE164Format_ShouldHaveValidationError()
       {
           var request = new CreateFamilyMemberRequest(
               "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child,
               Phone: "612345678"); // Missing country code

           var result = _validator.TestValidate(request);

           result.ShouldHaveValidationErrorFor(x => x.Phone);
       }

       [Fact]
       public void Validate_WhenAllFieldsValid_ShouldNotHaveValidationErrors()
       {
           var request = new CreateFamilyMemberRequest(
               "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child,
               "ABC123", "maria@example.com", "+34612345678", "Asthma", "Peanuts");

           var result = _validator.TestValidate(request);

           result.ShouldNotHaveAnyValidationErrors();
       }
   }
   ```

2. **Create similar tests for other validators**:
   - `CreateFamilyUnitValidatorTests.cs`
   - `UpdateFamilyUnitValidatorTests.cs`
   - `UpdateFamilyMemberValidatorTests.cs`

**Test Count (Validators)**: ~25-30 tests

---

### Step 13: Manual Testing and Verification

**Action**: Test all endpoints manually using Swagger or curl

**Implementation Steps**:

1. **Run the application**:

   ```bash
   dotnet run --project src/Abuvi.API
   ```

2. **Open Swagger**: Navigate to `http://localhost:5079/swagger`

3. **Test Create Family Unit**:

   ```bash
   curl -X POST http://localhost:5079/api/family-units \
     -H "Authorization: Bearer YOUR_JWT_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{"name":"Garcia Family"}'
   ```

4. **Verify Response**:
   - Status: 201 Created
   - Body contains family unit with ID
   - Representative member created automatically

5. **Test Get Family Unit**:

   ```bash
   curl -X GET http://localhost:5079/api/family-units/me \
     -H "Authorization: Bearer YOUR_JWT_TOKEN"
   ```

6. **Test Create Family Member**:

   ```bash
   curl -X POST http://localhost:5079/api/family-units/{familyUnitId}/members \
     -H "Authorization: Bearer YOUR_JWT_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{
       "firstName":"Maria",
       "lastName":"Garcia",
       "dateOfBirth":"2015-06-15",
       "relationship":"Child",
       "documentNumber":"12345678A",
       "email":"maria@example.com",
       "phone":"+34612345678"
     }'
   ```

7. **Test Authorization**:
   - Try accessing another user's family unit → 403 Forbidden
   - Try deleting representative's own member → 409 Conflict

8. **Test Validation**:
   - Try creating member with future date of birth → 400 Bad Request
   - Try creating member with invalid email → 400 Bad Request
   - Try creating member with lowercase document number → 400 Bad Request

**Manual Testing Checklist**:

- ✅ Create family unit
- ✅ Get family unit (me endpoint)
- ✅ Get family unit by ID (representative)
- ✅ Get family unit by ID (non-representative) → 403
- ✅ Update family unit
- ✅ Delete family unit
- ✅ Create family member
- ✅ Get family members
- ✅ Update family member
- ✅ Delete family member
- ✅ Validation errors return 400
- ✅ Authorization checks work (403)
- ✅ Medical notes/allergies encrypted
- ✅ Sensitive data never in responses

---

### Step 14: Update Technical Documentation

**Action**: Update all affected documentation

**Implementation Steps**:

1. **Update `ai-specs/specs/data-model.md`**:
   - Add new fields to FamilyMember entity:
     - `DocumentNumber` (string, max 50 chars, nullable, uppercase alphanumeric)
     - `Email` (string, max 255 chars, nullable, valid email format)
     - `Phone` (string, max 20 chars, nullable, E.164 format)
   - Update Relationship enum to include `Sibling` and `Spouse`

2. **Update `ai-specs/specs/api-endpoints.md`**:
   - Add all 10 new family unit endpoints:
     - POST /api/family-units
     - GET /api/family-units/me
     - GET /api/family-units/{id}
     - PUT /api/family-units/{id}
     - DELETE /api/family-units/{id}
     - POST /api/family-units/{familyUnitId}/members
     - GET /api/family-units/{familyUnitId}/members
     - GET /api/family-units/{familyUnitId}/members/{memberId}
     - PUT /api/family-units/{familyUnitId}/members/{memberId}
     - DELETE /api/family-units/{familyUnitId}/members/{memberId}
   - Document request/response formats
   - Document error codes

3. **Verify Swagger/OpenAPI**:
   - Auto-generated OpenAPI spec includes all endpoints
   - Schemas for DTOs are correct

4. **Report Documentation Updates**:
   - List which files were updated
   - Summarize changes made

**Documentation Files to Update**:

- `ai-specs/specs/data-model.md` (add new FamilyMember fields)
- `ai-specs/specs/api-endpoints.md` (add 10 new endpoints)

**References**:

- Follow `ai-specs/specs/documentation-standards.mdc`
- All documentation in English (code and docs)

---

## Implementation Order

1. **Step 0**: Create feature branch (`feature/feat-family-units-definition-backend`)
2. **Step 1**: Update entity configuration and create migration
3. **Step 2**: Create Request/Response DTOs
4. **Step 3**: Create FluentValidation validators
5. **Step 4**: TDD Phase 1 - Write tests for FamilyUnitsService (Family Unit CRUD)
6. **Step 5**: Implement FamilyUnitsRepository
7. **Step 6**: Implement FamilyUnitsService (make tests pass)
8. **Step 7**: Create EncryptionService
9. **Step 8**: TDD Phase 2 - Write tests for Family Member operations
10. **Step 9**: Create Minimal API Endpoints
11. **Step 10**: Register services and endpoints in Program.cs
12. **Step 11**: Apply EF Core migration
13. **Step 12**: Write validator unit tests
14. **Step 13**: Manual testing and verification
15. **Step 14**: Update technical documentation

---

## Testing Checklist

### Unit Tests (90%+ Coverage)

**FamilyUnitsService Tests** (~36 tests):

**Family Unit CRUD**:

- ✅ CreateFamilyUnitAsync - success
- ✅ CreateFamilyUnitAsync - user already has family unit
- ✅ CreateFamilyUnitAsync - user not found
- ✅ CreateFamilyUnitAsync - automatically creates representative member
- ✅ GetFamilyUnitByIdAsync - success
- ✅ GetFamilyUnitByIdAsync - not found
- ✅ GetCurrentUserFamilyUnitAsync - success
- ✅ GetCurrentUserFamilyUnitAsync - not found
- ✅ UpdateFamilyUnitAsync - success
- ✅ UpdateFamilyUnitAsync - not found
- ✅ DeleteFamilyUnitAsync - success
- ✅ DeleteFamilyUnitAsync - not found
- ✅ IsRepresentativeAsync - returns true when user is representative
- ✅ IsRepresentativeAsync - returns false when user is not representative

**Family Member CRUD**:

- ✅ CreateFamilyMemberAsync - success
- ✅ CreateFamilyMemberAsync - family unit not found
- ✅ CreateFamilyMemberAsync - encrypts medical notes
- ✅ CreateFamilyMemberAsync - encrypts allergies
- ✅ CreateFamilyMemberAsync - converts document number to uppercase
- ✅ GetFamilyMembersByFamilyUnitIdAsync - returns list
- ✅ GetFamilyMembersByFamilyUnitIdAsync - returns empty list
- ✅ GetFamilyMemberByIdAsync - success
- ✅ GetFamilyMemberByIdAsync - not found
- ✅ UpdateFamilyMemberAsync - success
- ✅ UpdateFamilyMemberAsync - not found
- ✅ UpdateFamilyMemberAsync - encrypts medical notes
- ✅ UpdateFamilyMemberAsync - encrypts allergies
- ✅ DeleteFamilyMemberAsync - success
- ✅ DeleteFamilyMemberAsync - not found
- ✅ DeleteFamilyMemberAsync - prevents deleting representative's own record

**Security & Privacy**:

- ✅ Medical notes never in response (boolean flag only)
- ✅ Allergies never in response (boolean flag only)
- ✅ Encryption service called for sensitive data

**Validator Tests** (~25-30 tests):

**CreateFamilyMemberValidator**:

- ✅ FirstName - required
- ✅ FirstName - max length 100
- ✅ LastName - required
- ✅ LastName - max length 100
- ✅ DateOfBirth - required
- ✅ DateOfBirth - must be past date
- ✅ Relationship - must be valid enum
- ✅ DocumentNumber - max length 50
- ✅ DocumentNumber - uppercase alphanumeric only
- ✅ Email - valid format
- ✅ Email - max length 255
- ✅ Phone - E.164 format
- ✅ Phone - max length 20
- ✅ MedicalNotes - max length 2000
- ✅ Allergies - max length 1000
- ✅ All fields valid - no errors

**CreateFamilyUnitValidator**:

- ✅ Name - required
- ✅ Name - max length 200

**UpdateFamilyUnitValidator**: (same as create)

**UpdateFamilyMemberValidator**: (same as create)

### Manual Testing

- ✅ All 10 endpoints return correct status codes
- ✅ Authorization checks work (403 Forbidden)
- ✅ Validation errors return 400 with Spanish messages
- ✅ Representative auto-created as family member
- ✅ Medical notes/allergies encrypted in database
- ✅ Sensitive data never exposed in API responses
- ✅ Document number converted to uppercase
- ✅ Cannot delete representative's own member record

---

## Error Response Format

All errors use `ApiResponse<object>` envelope:

```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "Spanish user-friendly message",
    "code": "ERROR_CODE"
  }
}
```

### HTTP Status Code Mapping

| Status Code | When to Use | Example |
|-------------|-------------|---------|
| 200 OK | Successful GET/PUT | Get family unit |
| 201 Created | Successful POST | Create family unit |
| 204 No Content | Successful DELETE | Delete family unit |
| 400 Bad Request | Validation failed | Invalid email format |
| 403 Forbidden | Not authorized | Non-representative accessing family unit |
| 404 Not Found | Resource doesn't exist | Family unit not found |
| 409 Conflict | Business rule violation | User already has family unit |
| 500 Internal Server Error | Unexpected error | Database connection failed |

### Error Codes

| Code | Message (Spanish) | Scenario |
|------|-------------------|----------|
| `VALIDATION_ERROR` | "Por favor revisa los datos ingresados" | FluentValidation failure |
| `FAMILY_UNIT_EXISTS` | "Ya tienes una unidad familiar" | User already has family unit |
| `NOT_FOUND` | "No se encontró {entity} con ID '{id}'" | Resource not found |
| `FORBIDDEN` | "No tienes permiso para realizar esta acción" | Authorization failed |
| `CANNOT_DELETE_REPRESENTATIVE` | "No puedes eliminar tu propio perfil mientras seas representante" | Trying to delete own member record |
| `INTERNAL_ERROR` | "Ocurrió un error inesperado. Por favor intenta nuevamente." | Unexpected server error |

---

## Dependencies

### NuGet Packages (Already Installed)

- `Microsoft.EntityFrameworkCore` (9.0)
- `Npgsql.EntityFrameworkCore.PostgreSQL` (9.0)
- `FluentValidation` (11.x)
- `FluentValidation.DependencyInjectionExtensions` (11.x)
- `xUnit` (2.x)
- `FluentAssertions` (6.x)
- `NSubstitute` (5.x)

### EF Core Migration Commands

```bash
# Create migration
dotnet ef migrations add AddFamilyMemberAdditionalFields --project src/Abuvi.API

# Apply migration
dotnet ef database update --project src/Abuvi.API

# List migrations
dotnet ef migrations list --project src/Abuvi.API

# Generate SQL script (for production)
dotnet ef migrations script --idempotent --project src/Abuvi.API
```

---

## Notes

### CRITICAL Reminders

1. **TDD is MANDATORY**: Write tests BEFORE implementing service methods
2. **Spanish for user-facing messages**: All validation messages, error messages, and API responses in Spanish
3. **Encryption**: Medical notes and allergies MUST be encrypted at rest
4. **Never expose sensitive data**: API responses use boolean flags only
5. **Authorization**: Check representative OR Admin/Board role
6. **Document number**: Always uppercase before saving
7. **Representative auto-creation**: When creating family unit, automatically create representative as first family member
8. **Cascade delete**: Deleting family unit deletes all members
9. **User.familyUnitId**: Update when creating/deleting family unit
10. **Gender agreement**: Spanish validation messages must have correct gender

### Business Rules

- **One family unit per user**: Check `User.familyUnitId` is null before creating
- **Representative as member**: Automatically create representative as first family member with `relationship = Parent`
- **Cannot delete own record**: Representative cannot delete their own family member record
- **Authorization**: Only representative OR Admin/Board can view/modify family unit
- **Encryption key**: Store in Azure Key Vault for production (user secrets for dev)

### Language Requirements

**Spanish (User-Facing)**:

- Validation messages
- Error messages
- Business rule exceptions
- API error responses

**English (Developer-Facing)**:

- Code comments
- Variable names
- Log messages
- Documentation

**Gender Agreement Examples**:

- "El nombre es obligatorio" (masculine)
- "La contraseña es obligatoria" (feminine)
- "Los apellidos son obligatorios" (plural)
- "El correo electrónico es obligatorio" (masculine)
- "La fecha de nacimiento es obligatoria" (feminine)

### RGPD/GDPR Considerations

- **Sensitive Data**: Medical notes and allergies are sensitive health data
- **Encryption**: Use AES-256 encryption at rest
- **Access Control**: Only representative can view/edit sensitive data
- **Audit Logging**: Log all access to sensitive data
- **Data Minimization**: Only collect necessary data
- **Right to be Forgotten**: Soft delete (IsActive flag) for audit trail

---

## Next Steps After Implementation

1. **Frontend Integration**: Create Vue.js components for family unit management
2. **Photo Upload**: Implement blob storage for family unit/member photos
3. **Camp Registration**: Use family units for multi-member camp registrations
4. **Audit Log UI**: Admin interface to view access logs for sensitive data
5. **Export Functionality**: PDF/CSV export of family data
6. **Bulk Import**: CSV upload for family members
7. **Self-Registration**: Allow family members to link their own User accounts

---

## Implementation Verification

### Final Checklist

**Code Quality**:

- ✅ All C# analyzers passing (no warnings)
- ✅ Nullable reference types enabled
- ✅ Primary constructors used for DI
- ✅ File-scoped namespaces
- ✅ Record types for DTOs
- ✅ Extension methods for mapping

**Functionality**:

- ✅ All 10 endpoints return correct status codes
- ✅ Authorization checks work correctly
- ✅ Validation errors in Spanish
- ✅ Business rules enforced (one family unit per user, etc.)
- ✅ Representative auto-created as family member
- ✅ Cascade delete works
- ✅ User.familyUnitId updated correctly

**Testing**:

- ✅ 90%+ code coverage (FamilyUnitsService)
- ✅ All unit tests passing (36+ tests)
- ✅ All validator tests passing (25-30 tests)
- ✅ Manual testing completed

**Integration**:

- ✅ EF Core migration applied successfully
- ✅ Database schema updated (DocumentNumber, Email, Phone added)
- ✅ Relationship enum includes Sibling and Spouse
- ✅ Services registered in Program.cs
- ✅ Endpoints mapped in Program.cs

**Security & Privacy**:

- ✅ Medical notes encrypted
- ✅ Allergies encrypted
- ✅ Sensitive data never in API responses
- ✅ Authorization checks in place
- ✅ Audit logging implemented

**Documentation**:

- ✅ `ai-specs/specs/data-model.md` updated
- ✅ `ai-specs/specs/api-endpoints.md` updated
- ✅ Swagger/OpenAPI auto-generated correctly

---

**Document Version:** 1.0
**Created:** 2026-02-14
**Status:** Ready for Implementation

---

**IMPORTANT**: This is a TDD plan. Tests MUST be written before implementation code. Follow the Red-Green-Refactor cycle for each feature.
