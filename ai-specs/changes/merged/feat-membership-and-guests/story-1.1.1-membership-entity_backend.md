# Backend Implementation Plan: Story 1.1.1 - Create Membership Entity and Configuration

**Source:** [feat-membership-and-guests_enriched.md](./feat-membership-and-guests_enriched.md) - Epic 1.1, Story 1.1.1
**Status:** Ready for Implementation
**Priority:** High
**Estimated Duration:** 2-3 hours
**Architecture:** Vertical Slice Architecture
**Approach:** Test-Driven Development (TDD)

---

## Overview

This story implements the foundational database schema for the membership system. We will create:
- `Membership` entity to track member status for FamilyMembers
- `MembershipFee` entity to track annual membership fees
- `FeeStatus` enum for fee payment states
- EF Core configurations for both entities
- Database migration to apply schema changes

This follows **Vertical Slice Architecture** principles by creating a new `Memberships` feature slice that encapsulates all membership-related code.

---

## Architecture Context

### Feature Slice

**Location:** `src/Abuvi.API/Features/Memberships/`

### Files to Create

1. `src/Abuvi.API/Features/Memberships/MembershipsModels.cs` - Domain entities, enums, and DTOs
2. `src/Abuvi.API/Data/Configurations/MembershipConfiguration.cs` - EF Core entity configuration
3. `src/Abuvi.API/Data/Configurations/MembershipFeeConfiguration.cs` - EF Core entity configuration

### Files to Modify

1. `src/Abuvi.API/Data/AbuviDbContext.cs` - Add DbSets for new entities

### Cross-Cutting Concerns

- **Database Schema**: New tables `memberships` and `membership_fees`
- **Data Model**: Extends FamilyMember with membership capability
- **Audit Fields**: CreatedAt, UpdatedAt for all entities

---

## Implementation Steps

### Step 0: Create Feature Branch

**Action**: Create and switch to a new feature branch following the development workflow.

**Branch Naming**: `feature/story-1.1.1-membership-entity-backend`

**Implementation Steps**:
1. Verify current branch: `git branch`
2. Ensure on base branch (likely `feat-family-units` or `main`): `git checkout feat-family-units`
3. Pull latest changes: `git pull origin feat-family-units`
4. Create new branch: `git checkout -b feature/story-1.1.1-membership-entity-backend`
5. Verify branch creation: `git branch` (should show * on new branch)

**Bash Commands**:
```bash
git checkout feat-family-units
git pull origin feat-family-units
git checkout -b feature/story-1.1.1-membership-entity-backend
git branch
```

**Notes**:
- This MUST be the first step before any code changes
- Follow the branch naming convention: `feature/[ticket-id]-backend`
- This separates backend work from frontend concerns
- Reference: `ai-specs/specs/backend-standards.mdc` section "Development Workflow"

---

### Step 1: Create Membership Feature Slice Folder

**Action**: Create the directory structure for the Memberships feature slice.

**Implementation Steps**:
1. Navigate to the Features directory: `cd src/Abuvi.API/Features`
2. Create Memberships directory: `mkdir Memberships`
3. Verify creation: `ls -la Features/`

**Bash Commands**:
```bash
mkdir -p src/Abuvi.API/Features/Memberships
ls -la src/Abuvi.API/Features/
```

**Notes**:
- This establishes the Memberships feature slice location
- All membership-related code will reside here (endpoints, models, service, repository, validators)
- Follows Vertical Slice Architecture principles

---

### Step 2: Create Membership Domain Entities

**File**: `src/Abuvi.API/Features/Memberships/MembershipsModels.cs`

**Action**: Create the `Membership`, `MembershipFee` entities and `FeeStatus` enum.

**Implementation**:

```csharp
namespace Abuvi.API.Features.Memberships;

/// <summary>
/// Represents an active membership for a family member
/// </summary>
public class Membership
{
    public Guid Id { get; set; }
    public Guid FamilyMemberId { get; set; }  // FK to FamilyMember
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }    // Nullable for active memberships
    public bool IsActive { get; set; }

    // Navigation
    public FamilyMember FamilyMember { get; set; } = null!;
    public ICollection<MembershipFee> Fees { get; set; } = new List<MembershipFee>();

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Represents an annual membership fee
/// </summary>
public class MembershipFee
{
    public Guid Id { get; set; }
    public Guid MembershipId { get; set; }
    public int Year { get; set; }
    public decimal Amount { get; set; }
    public FeeStatus Status { get; set; }
    public DateTime? PaidDate { get; set; }
    public string? PaymentReference { get; set; }

    // Navigation
    public Membership Membership { get; set; } = null!;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Status of a membership fee payment
/// </summary>
public enum FeeStatus
{
    Pending,    // Waiting for payment
    Paid,       // Payment received
    Overdue     // Payment deadline passed
}
```

**Implementation Notes**:
1. **Membership Entity**:
   - `Id`: UUID primary key
   - `FamilyMemberId`: Foreign key to `FamilyMember` (one-to-one relationship)
   - `StartDate`: When membership became active
   - `EndDate`: Nullable - only set when membership is deactivated
   - `IsActive`: Boolean flag for quick queries
   - Navigation to `FamilyMember` and collection of `Fees`

2. **MembershipFee Entity**:
   - `Id`: UUID primary key
   - `MembershipId`: Foreign key to `Membership` (one-to-many)
   - `Year`: Calendar year for the fee (e.g., 2026)
   - `Amount`: Fee amount in euros (decimal for precision)
   - `Status`: Payment status (Pending/Paid/Overdue)
   - `PaidDate`: Nullable - only set when payment received
   - `PaymentReference`: Optional external payment reference

3. **FeeStatus Enum**:
   - Will be stored as string in database for readability
   - Easier to query and debug than numeric values

4. **Audit Fields**:
   - Both entities include `CreatedAt` and `UpdatedAt`
   - These are required for all domain entities per project standards

**Dependencies**:
- Using statement: `using Abuvi.API.Features.FamilyUnits;` (for FamilyMember navigation property)

---

### Step 3: Create Membership EF Core Configuration

**File**: `src/Abuvi.API/Data/Configurations/MembershipConfiguration.cs`

**Action**: Configure the `Membership` entity using Fluent API.

**Implementation**:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Abuvi.API.Features.Memberships;

namespace Abuvi.API.Data.Configurations;

public class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()");

        // Unique constraint: one active membership per family member
        builder.HasIndex(m => m.FamilyMemberId).IsUnique();

        builder.Property(m => m.StartDate).IsRequired();
        builder.Property(m => m.IsActive).IsRequired();

        // Relationship to FamilyMember (one-to-one)
        builder.HasOne(m => m.FamilyMember)
            .WithOne()  // No back-reference on FamilyMember
            .HasForeignKey<Membership>(m => m.FamilyMemberId)
            .OnDelete(DeleteBehavior.Restrict);  // Don't cascade delete

        // Relationship to Fees (one-to-many)
        builder.HasMany(m => m.Fees)
            .WithOne(f => f.Membership)
            .HasForeignKey(f => f.MembershipId)
            .OnDelete(DeleteBehavior.Cascade);  // Delete fees when membership deleted

        // Audit fields
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.UpdatedAt).IsRequired();

        // Table name
        builder.ToTable("memberships");
    }
}
```

**Implementation Notes**:
1. **Primary Key**:
   - UUID auto-generated via PostgreSQL function `gen_random_uuid()`

2. **Unique Index**:
   - `FamilyMemberId` has unique constraint
   - Ensures only one membership record per family member
   - Database will enforce this at the data level

3. **Relationships**:
   - **FamilyMember**: One-to-one, Restrict delete (prevent deleting family member with active membership)
   - **Fees**: One-to-many, Cascade delete (remove all fees when membership deleted)

4. **Delete Behaviors**:
   - `Restrict` on FamilyMember: Business rule - can't delete a family member who has a membership
   - `Cascade` on Fees: Orphaned fees have no meaning without their membership

5. **Table Naming**:
   - PostgreSQL convention: lowercase with underscores
   - Table name: `memberships`

**Dependencies**:
- `using Microsoft.EntityFrameworkCore;`
- `using Microsoft.EntityFrameworkCore.Metadata.Builders;`
- `using Abuvi.API.Features.Memberships;`

---

### Step 4: Create MembershipFee EF Core Configuration

**File**: `src/Abuvi.API/Data/Configurations/MembershipFeeConfiguration.cs`

**Action**: Configure the `MembershipFee` entity using Fluent API.

**Implementation**:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Abuvi.API.Features.Memberships;

namespace Abuvi.API.Data.Configurations;

public class MembershipFeeConfiguration : IEntityTypeConfiguration<MembershipFee>
{
    public void Configure(EntityTypeBuilder<MembershipFee> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasDefaultValueSql("gen_random_uuid()");

        // Unique constraint: one fee per membership per year
        builder.HasIndex(f => new { f.MembershipId, f.Year }).IsUnique();

        builder.Property(f => f.Year).IsRequired();

        builder.Property(f => f.Amount)
            .HasPrecision(10, 2)  // Max 10 digits, 2 decimal places (e.g., 99999999.99)
            .IsRequired();

        builder.Property(f => f.Status)
            .HasConversion<string>()  // Store enum as string
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(f => f.PaymentReference)
            .HasMaxLength(100);  // Optional external payment reference

        // Audit fields
        builder.Property(f => f.CreatedAt).IsRequired();
        builder.Property(f => f.UpdatedAt).IsRequired();

        // Table name
        builder.ToTable("membership_fees");
    }
}
```

**Implementation Notes**:
1. **Composite Unique Index**:
   - `(MembershipId, Year)` must be unique
   - Prevents creating duplicate fees for the same membership and year
   - Database-level enforcement

2. **Amount Precision**:
   - `decimal(10, 2)` - up to 8 digits before decimal, 2 after
   - Sufficient for euro amounts (e.g., €99,999,999.99)
   - PostgreSQL `NUMERIC(10,2)` type

3. **Enum Storage**:
   - `Status` stored as `VARCHAR(20)` in database
   - Values: "Pending", "Paid", "Overdue"
   - More readable than numeric codes when querying database directly

4. **Optional Fields**:
   - `PaidDate` is nullable (only set when paid)
   - `PaymentReference` is nullable and max 100 characters

5. **Table Naming**:
   - PostgreSQL convention: lowercase with underscores
   - Table name: `membership_fees`

**Dependencies**:
- `using Microsoft.EntityFrameworkCore;`
- `using Microsoft.EntityFrameworkCore.Metadata.Builders;`
- `using Abuvi.API.Features.Memberships;`

---

### Step 5: Register Entities in DbContext

**File**: `src/Abuvi.API/Data/AbuviDbContext.cs`

**Action**: Add DbSet properties for `Membership` and `MembershipFee` entities.

**Implementation Steps**:
1. Read the existing `AbuviDbContext.cs` file
2. Add two new DbSet properties after existing DbSets
3. The configurations will be auto-discovered via `modelBuilder.ApplyConfigurationsFromAssembly()`

**Code to Add**:

```csharp
// Add these lines to the DbContext class, after existing DbSets

public DbSet<Membership> Memberships => Set<Membership>();
public DbSet<MembershipFee> MembershipFees => Set<MembershipFee>();
```

**Full Context** (for reference):

```csharp
using Microsoft.EntityFrameworkCore;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Memberships;  // ADD THIS USING
using Abuvi.API.Features.Users;
// ... other usings ...

namespace Abuvi.API.Data;

public class AbuviDbContext(DbContextOptions<AbuviDbContext> options) : DbContext(options)
{
    public DbSet<Camp> Camps => Set<Camp>();
    public DbSet<CampEdition> CampEditions => Set<CampEdition>();
    public DbSet<User> Users => Set<User>();
    public DbSet<FamilyUnit> FamilyUnits => Set<FamilyUnit>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();

    // ADD THESE TWO LINES:
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<MembershipFee> MembershipFees => Set<MembershipFee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // This automatically discovers and applies all IEntityTypeConfiguration implementations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AbuviDbContext).Assembly);
    }
}
```

**Implementation Notes**:
1. **DbSet Property Pattern**:
   - Use expression-bodied property with `Set<T>()`
   - This is the modern .NET pattern for DbSets

2. **Auto-Configuration Discovery**:
   - The `ApplyConfigurationsFromAssembly()` call already exists
   - It will automatically find and apply our new configurations
   - No need to manually register `MembershipConfiguration` and `MembershipFeeConfiguration`

3. **Using Statement**:
   - Add `using Abuvi.API.Features.Memberships;` at the top of the file

---

### Step 6: Create EF Core Migration

**Action**: Generate and apply a database migration for the new entities.

**Implementation Steps**:
1. Navigate to the API project directory
2. Create migration with descriptive name
3. Review the generated migration file
4. Apply migration to local database

**Bash Commands**:
```bash
# Navigate to project root
cd D:/Repos/feat-family-units

# Create migration
dotnet ef migrations add AddMembershipEntities --project src/Abuvi.API

# Review the migration file (it will be in src/Abuvi.API/Data/Migrations/)
# Look for the file: YYYYMMDDHHMMSS_AddMembershipEntities.cs

# Apply migration to local database
dotnet ef database update --project src/Abuvi.API
```

**Expected Migration Output**:

The migration should create:
- Table `memberships` with columns: `id`, `family_member_id`, `start_date`, `end_date`, `is_active`, `created_at`, `updated_at`
- Table `membership_fees` with columns: `id`, `membership_id`, `year`, `amount`, `status`, `paid_date`, `payment_reference`, `created_at`, `updated_at`
- Unique index on `memberships.family_member_id`
- Composite unique index on `membership_fees(membership_id, year)`
- Foreign key from `memberships.family_member_id` to `family_members.id` (Restrict)
- Foreign key from `membership_fees.membership_id` to `memberships.id` (Cascade)

**Verification Steps**:
1. **Check migration file**: Open the generated migration file and verify it creates the expected tables and indexes
2. **Check database**: Connect to PostgreSQL and verify tables exist:
   ```sql
   \dt memberships
   \dt membership_fees
   \d memberships
   \d membership_fees
   ```

**Implementation Notes**:
1. **Migration Naming**:
   - Use descriptive names: `AddMembershipEntities`
   - Follows pattern: `Add[Entity]` for new entities

2. **Review Before Apply**:
   - Always review the generated migration code
   - Ensure no unexpected changes to existing tables
   - Verify constraint names are reasonable

3. **Rollback Plan**:
   - If migration fails or is incorrect: `dotnet ef database update [PreviousMigration]`
   - Then delete the migration file and regenerate

4. **Team Considerations**:
   - Commit the migration file to git
   - Other developers will apply it via `dotnet ef database update`

---

### Step 7: Write Unit Tests for Entity Validation

**File**: `src/Abuvi.Tests/Unit/Features/Memberships/MembershipEntityTests.cs`

**Action**: Create unit tests to verify entity properties and relationships.

**Implementation**:

```csharp
using Abuvi.API.Features.Memberships;
using FluentAssertions;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Memberships;

public class MembershipEntityTests
{
    [Fact]
    public void Membership_WhenCreated_HasExpectedProperties()
    {
        // Arrange & Act
        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            FamilyMemberId = Guid.NewGuid(),
            StartDate = DateTime.UtcNow,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        membership.Id.Should().NotBeEmpty();
        membership.FamilyMemberId.Should().NotBeEmpty();
        membership.StartDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        membership.EndDate.Should().BeNull();
        membership.IsActive.Should().BeTrue();
        membership.Fees.Should().NotBeNull().And.BeEmpty();
        membership.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        membership.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MembershipFee_WhenCreated_HasExpectedProperties()
    {
        // Arrange & Act
        var fee = new MembershipFee
        {
            Id = Guid.NewGuid(),
            MembershipId = Guid.NewGuid(),
            Year = 2026,
            Amount = 50.00m,
            Status = FeeStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        fee.Id.Should().NotBeEmpty();
        fee.MembershipId.Should().NotBeEmpty();
        fee.Year.Should().Be(2026);
        fee.Amount.Should().Be(50.00m);
        fee.Status.Should().Be(FeeStatus.Pending);
        fee.PaidDate.Should().BeNull();
        fee.PaymentReference.Should().BeNull();
        fee.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        fee.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Membership_CanHaveMultipleFees()
    {
        // Arrange
        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            FamilyMemberId = Guid.NewGuid(),
            StartDate = DateTime.UtcNow.AddYears(-2),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var fee2024 = new MembershipFee
        {
            Id = Guid.NewGuid(),
            MembershipId = membership.Id,
            Year = 2024,
            Amount = 45.00m,
            Status = FeeStatus.Paid,
            PaidDate = new DateTime(2024, 2, 15),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var fee2025 = new MembershipFee
        {
            Id = Guid.NewGuid(),
            MembershipId = membership.Id,
            Year = 2025,
            Amount = 48.00m,
            Status = FeeStatus.Paid,
            PaidDate = new DateTime(2025, 3, 1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var fee2026 = new MembershipFee
        {
            Id = Guid.NewGuid(),
            MembershipId = membership.Id,
            Year = 2026,
            Amount = 50.00m,
            Status = FeeStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        membership.Fees.Add(fee2024);
        membership.Fees.Add(fee2025);
        membership.Fees.Add(fee2026);

        // Assert
        membership.Fees.Should().HaveCount(3);
        membership.Fees.Should().Contain(f => f.Year == 2024 && f.Status == FeeStatus.Paid);
        membership.Fees.Should().Contain(f => f.Year == 2025 && f.Status == FeeStatus.Paid);
        membership.Fees.Should().Contain(f => f.Year == 2026 && f.Status == FeeStatus.Pending);
    }

    [Theory]
    [InlineData(FeeStatus.Pending)]
    [InlineData(FeeStatus.Paid)]
    [InlineData(FeeStatus.Overdue)]
    public void FeeStatus_AllEnumValues_AreValid(FeeStatus status)
    {
        // Arrange & Act
        var fee = new MembershipFee
        {
            Id = Guid.NewGuid(),
            MembershipId = Guid.NewGuid(),
            Year = 2026,
            Amount = 50.00m,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        fee.Status.Should().Be(status);
        Enum.IsDefined(typeof(FeeStatus), status).Should().BeTrue();
    }
}
```

**Implementation Notes**:
1. **Test Organization**:
   - Create test file in mirror structure: `Tests/Unit/Features/Memberships/`
   - Test class name matches entity name + "Tests" suffix

2. **Test Coverage**:
   - Basic property assignment
   - Nullable properties (EndDate, PaidDate, PaymentReference)
   - Collection initialization (Fees)
   - Multiple fees per membership
   - All enum values

3. **FluentAssertions**:
   - Use readable assertion syntax: `.Should().Be()`, `.Should().NotBeNull()`
   - `BeCloseTo()` for DateTime comparisons (handles minor timing differences)

4. **Test Naming**:
   - Pattern: `EntityName_StateUnderTest_ExpectedBehavior`
   - Example: `Membership_WhenCreated_HasExpectedProperties`

---

### Step 8: Write Integration Tests for Database Constraints

**File**: `src/Abuvi.Tests/Integration/Features/Memberships/MembershipDatabaseTests.cs`

**Action**: Create integration tests to verify EF Core configuration and database constraints.

**Implementation**:

```csharp
using Abuvi.API.Data;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Memberships;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Abuvi.Tests.Integration.Features.Memberships;

public class MembershipDatabaseTests : IDisposable
{
    private readonly AbuviDbContext _dbContext;

    public MembershipDatabaseTests()
    {
        var options = new DbContextOptionsBuilder<AbuviDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new AbuviDbContext(options);
    }

    [Fact]
    public async Task Membership_WhenSaved_IsPersisted()
    {
        // Arrange
        var user = CreateTestUser();
        var familyUnit = CreateTestFamilyUnit(user.Id);
        var familyMember = CreateTestFamilyMember(familyUnit.Id);

        _dbContext.Users.Add(user);
        _dbContext.FamilyUnits.Add(familyUnit);
        _dbContext.FamilyMembers.Add(familyMember);
        await _dbContext.SaveChangesAsync();

        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            FamilyMemberId = familyMember.Id,
            StartDate = DateTime.UtcNow,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        _dbContext.Memberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        // Assert
        var savedMembership = await _dbContext.Memberships
            .FirstOrDefaultAsync(m => m.Id == membership.Id);

        savedMembership.Should().NotBeNull();
        savedMembership!.FamilyMemberId.Should().Be(familyMember.Id);
        savedMembership.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task MembershipFee_WhenSaved_IsPersisted()
    {
        // Arrange
        var user = CreateTestUser();
        var familyUnit = CreateTestFamilyUnit(user.Id);
        var familyMember = CreateTestFamilyMember(familyUnit.Id);
        var membership = CreateTestMembership(familyMember.Id);

        _dbContext.Users.Add(user);
        _dbContext.FamilyUnits.Add(familyUnit);
        _dbContext.FamilyMembers.Add(familyMember);
        _dbContext.Memberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        var fee = new MembershipFee
        {
            Id = Guid.NewGuid(),
            MembershipId = membership.Id,
            Year = 2026,
            Amount = 50.00m,
            Status = FeeStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        _dbContext.MembershipFees.Add(fee);
        await _dbContext.SaveChangesAsync();

        // Assert
        var savedFee = await _dbContext.MembershipFees
            .FirstOrDefaultAsync(f => f.Id == fee.Id);

        savedFee.Should().NotBeNull();
        savedFee!.MembershipId.Should().Be(membership.Id);
        savedFee.Year.Should().Be(2026);
        savedFee.Amount.Should().Be(50.00m);
        savedFee.Status.Should().Be(FeeStatus.Pending);
    }

    [Fact]
    public async Task Membership_CanLoadFamilyMemberNavigation()
    {
        // Arrange
        var user = CreateTestUser();
        var familyUnit = CreateTestFamilyUnit(user.Id);
        var familyMember = CreateTestFamilyMember(familyUnit.Id);
        var membership = CreateTestMembership(familyMember.Id);

        _dbContext.Users.Add(user);
        _dbContext.FamilyUnits.Add(familyUnit);
        _dbContext.FamilyMembers.Add(familyMember);
        _dbContext.Memberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        // Act
        var loadedMembership = await _dbContext.Memberships
            .Include(m => m.FamilyMember)
            .FirstOrDefaultAsync(m => m.Id == membership.Id);

        // Assert
        loadedMembership.Should().NotBeNull();
        loadedMembership!.FamilyMember.Should().NotBeNull();
        loadedMembership.FamilyMember.Id.Should().Be(familyMember.Id);
        loadedMembership.FamilyMember.FirstName.Should().Be(familyMember.FirstName);
    }

    [Fact]
    public async Task Membership_CanLoadFeesCollection()
    {
        // Arrange
        var user = CreateTestUser();
        var familyUnit = CreateTestFamilyUnit(user.Id);
        var familyMember = CreateTestFamilyMember(familyUnit.Id);
        var membership = CreateTestMembership(familyMember.Id);

        var fee2025 = new MembershipFee
        {
            Id = Guid.NewGuid(),
            MembershipId = membership.Id,
            Year = 2025,
            Amount = 48.00m,
            Status = FeeStatus.Paid,
            PaidDate = new DateTime(2025, 2, 1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var fee2026 = new MembershipFee
        {
            Id = Guid.NewGuid(),
            MembershipId = membership.Id,
            Year = 2026,
            Amount = 50.00m,
            Status = FeeStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        _dbContext.FamilyUnits.Add(familyUnit);
        _dbContext.FamilyMembers.Add(familyMember);
        _dbContext.Memberships.Add(membership);
        _dbContext.MembershipFees.Add(fee2025);
        _dbContext.MembershipFees.Add(fee2026);
        await _dbContext.SaveChangesAsync();

        // Act
        var loadedMembership = await _dbContext.Memberships
            .Include(m => m.Fees)
            .FirstOrDefaultAsync(m => m.Id == membership.Id);

        // Assert
        loadedMembership.Should().NotBeNull();
        loadedMembership!.Fees.Should().HaveCount(2);
        loadedMembership.Fees.Should().Contain(f => f.Year == 2025 && f.Status == FeeStatus.Paid);
        loadedMembership.Fees.Should().Contain(f => f.Year == 2026 && f.Status == FeeStatus.Pending);
    }

    [Fact]
    public async Task MembershipFee_WhenMembershipDeleted_IsAlsoDele ted()
    {
        // Arrange
        var user = CreateTestUser();
        var familyUnit = CreateTestFamilyUnit(user.Id);
        var familyMember = CreateTestFamilyMember(familyUnit.Id);
        var membership = CreateTestMembership(familyMember.Id);

        var fee = new MembershipFee
        {
            Id = Guid.NewGuid(),
            MembershipId = membership.Id,
            Year = 2026,
            Amount = 50.00m,
            Status = FeeStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        _dbContext.FamilyUnits.Add(familyUnit);
        _dbContext.FamilyMembers.Add(familyMember);
        _dbContext.Memberships.Add(membership);
        _dbContext.MembershipFees.Add(fee);
        await _dbContext.SaveChangesAsync();

        // Act - Delete membership
        _dbContext.Memberships.Remove(membership);
        await _dbContext.SaveChangesAsync();

        // Assert - Fee should be cascade deleted
        var deletedFee = await _dbContext.MembershipFees
            .FirstOrDefaultAsync(f => f.Id == fee.Id);

        deletedFee.Should().BeNull();
    }

    // Helper methods to create test data
    private User CreateTestUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = $"test{Guid.NewGuid()}@example.com",
        FirstName = "Test",
        LastName = "User",
        PasswordHash = "hashedpassword",
        Role = UserRole.Representative,
        EmailVerified = true,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private FamilyUnit CreateTestFamilyUnit(Guid representativeUserId) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Family",
        RepresentativeUserId = representativeUserId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private FamilyMember CreateTestFamilyMember(Guid familyUnitId) => new()
    {
        Id = Guid.NewGuid(),
        FamilyUnitId = familyUnitId,
        FirstName = "John",
        LastName = "Doe",
        DateOfBirth = new DateOnly(1990, 1, 1),
        Relationship = FamilyRelationship.Parent,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private Membership CreateTestMembership(Guid familyMemberId) => new()
    {
        Id = Guid.NewGuid(),
        FamilyMemberId = familyMemberId,
        StartDate = DateTime.UtcNow,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
```

**Implementation Notes**:
1. **In-Memory Database**:
   - Each test gets a fresh database with unique name
   - Fast execution, no external dependencies
   - Use for testing EF Core behavior and relationships

2. **Test Coverage**:
   - Entity persistence (save and retrieve)
   - Navigation property loading (`Include()`)
   - Collection loading
   - Cascade delete behavior
   - Foreign key constraints (implicit in relationships)

3. **Helper Methods**:
   - `CreateTestUser()`, `CreateTestFamilyUnit()`, etc.
   - Reduce test code duplication
   - Make tests more readable

4. **Cleanup**:
   - Implement `IDisposable` to dispose DbContext after each test
   - Prevents memory leaks in test runner

---

### Step 9: Update Technical Documentation

**Action**: Update the data model documentation to reflect the new entities.

**File**: `ai-specs/specs/data-model.md`

**Implementation Steps**:
1. Read the existing `data-model.md` file
2. Add a new section for "Membership System"
3. Document the `Membership` and `MembershipFee` entities
4. Update the entity relationship diagram (ERD) if present

**Content to Add**:

```markdown
### Membership System

#### Membership

Represents an active membership for a family member.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| id | UUID | No | Primary key |
| family_member_id | UUID | No | Foreign key to family_members (unique) |
| start_date | TIMESTAMP | No | When membership became active |
| end_date | TIMESTAMP | Yes | When membership ended (null if active) |
| is_active | BOOLEAN | No | Whether membership is currently active |
| created_at | TIMESTAMP | No | Record creation timestamp |
| updated_at | TIMESTAMP | No | Last update timestamp |

**Relationships:**
- One-to-one with `FamilyMember` (Restrict delete)
- One-to-many with `MembershipFee` (Cascade delete)

**Business Rules:**
- Each family member can have at most one membership record (unique constraint on family_member_id)
- Cannot delete a family member with an active membership (Restrict delete)
- When membership is deleted, all associated fees are deleted (Cascade delete)

#### MembershipFee

Represents an annual membership fee payment.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| id | UUID | No | Primary key |
| membership_id | UUID | No | Foreign key to memberships |
| year | INTEGER | No | Year of the fee (e.g., 2026) |
| amount | NUMERIC(10,2) | No | Fee amount in euros |
| status | VARCHAR(20) | No | Payment status (Pending, Paid, Overdue) |
| paid_date | TIMESTAMP | Yes | When fee was paid |
| payment_reference | VARCHAR(100) | Yes | External payment reference |
| created_at | TIMESTAMP | No | Record creation timestamp |
| updated_at | TIMESTAMP | No | Last update timestamp |

**Relationships:**
- Many-to-one with `Membership` (Cascade delete from parent)

**Business Rules:**
- Each membership can have at most one fee per year (unique constraint on (membership_id, year))
- Fee status transitions: Pending → Paid or Pending → Overdue
- paid_date must be set when status is Paid

#### FeeStatus Enum

| Value | Description |
|-------|-------------|
| Pending | Fee has been generated but not yet paid |
| Paid | Fee has been paid |
| Overdue | Fee payment deadline has passed without payment |
```

**Notes**:
- Update the document in English (per documentation standards)
- Include table schemas, relationships, and business rules
- If there's an ERD diagram, add relationships between FamilyMember, Membership, and MembershipFee

---

## Implementation Order

Follow these steps in sequence:

1. **Step 0**: Create Feature Branch (`feature/story-1.1.1-membership-entity-backend`)
2. **Step 1**: Create Membership Feature Slice Folder
3. **Step 2**: Create Membership Domain Entities (`MembershipsModels.cs`)
4. **Step 3**: Create Membership EF Core Configuration (`MembershipConfiguration.cs`)
5. **Step 4**: Create MembershipFee EF Core Configuration (`MembershipFeeConfiguration.cs`)
6. **Step 5**: Register Entities in DbContext (`AbuviDbContext.cs`)
7. **Step 6**: Create EF Core Migration (`AddMembershipEntities`)
8. **Step 7**: Write Unit Tests for Entity Validation
9. **Step 8**: Write Integration Tests for Database Constraints
10. **Step 9**: Update Technical Documentation (`data-model.md`)

---

## Testing Checklist

After implementation, verify:

### Unit Tests (Step 7)
- [ ] `Membership_WhenCreated_HasExpectedProperties` passes
- [ ] `MembershipFee_WhenCreated_HasExpectedProperties` passes
- [ ] `Membership_CanHaveMultipleFees` passes
- [ ] `FeeStatus_AllEnumValues_AreValid` passes
- [ ] All tests in `MembershipEntityTests` pass

### Integration Tests (Step 8)
- [ ] `Membership_WhenSaved_IsPersisted` passes
- [ ] `MembershipFee_WhenSaved_IsPersisted` passes
- [ ] `Membership_CanLoadFamilyMemberNavigation` passes
- [ ] `Membership_CanLoadFeesCollection` passes
- [ ] `MembershipFee_WhenMembershipDeleted_IsAlsoDeleted` passes
- [ ] All tests in `MembershipDatabaseTests` pass

### Database Verification
- [ ] Migration creates `memberships` table with correct schema
- [ ] Migration creates `membership_fees` table with correct schema
- [ ] Unique index exists on `memberships.family_member_id`
- [ ] Composite unique index exists on `membership_fees(membership_id, year)`
- [ ] Foreign key constraint from `memberships` to `family_members` (Restrict)
- [ ] Foreign key constraint from `membership_fees` to `memberships` (Cascade)

### Documentation
- [ ] `data-model.md` updated with Membership and MembershipFee entities
- [ ] All relationships documented
- [ ] Business rules documented

---

## Error Response Format

This story doesn't include API endpoints, so no error responses yet. This will be covered in Story 1.2.3 (Create Membership API Endpoints).

---

## Dependencies

### NuGet Packages (Already Installed)
- `Microsoft.EntityFrameworkCore` (9.x)
- `Npgsql.EntityFrameworkCore.PostgreSQL` (9.x)
- `xUnit` (2.x)
- `FluentAssertions` (6.x)

### EF Core Commands
```bash
# Create migration
dotnet ef migrations add AddMembershipEntities --project src/Abuvi.API

# Apply migration
dotnet ef database update --project src/Abuvi.API

# Rollback (if needed)
dotnet ef database update [PreviousMigrationName] --project src/Abuvi.API

# Remove last migration (if not applied)
dotnet ef migrations remove --project src/Abuvi.API
```

---

## Notes

### Important Reminders

1. **Vertical Slice Architecture**:
   - All membership code lives in `Features/Memberships/`
   - Don't create separate "Models", "Services", "Repositories" folders
   - Each feature is self-contained

2. **Database Constraints**:
   - Unique constraint on `memberships.family_member_id` ensures one membership per family member
   - Composite unique constraint on `membership_fees(membership_id, year)` prevents duplicate fees
   - Cascade delete on fees prevents orphaned fee records
   - Restrict delete on family member protects against data loss

3. **Nullable Reference Types**:
   - Enable in all C# files: `<Nullable>enable</Nullable>` in .csproj
   - Use `string?` for nullable strings
   - Use `= null!` for EF Core navigation properties

4. **Audit Fields**:
   - All entities must have `CreatedAt` and `UpdatedAt`
   - These are required by project standards
   - Repository layer will manage updates to `UpdatedAt`

5. **Language Requirements**:
   - All code, comments, and documentation in English
   - User-facing messages (validation, errors) will be in Spanish (added in later stories)

### Business Rules

1. **One Membership Per Family Member**:
   - Database enforces via unique constraint
   - Application layer will validate before creation (Story 1.2.1)

2. **One Fee Per Year**:
   - Database enforces via composite unique constraint
   - Background service will generate annual fees (Story 1.4.1)

3. **Fee Status Lifecycle**:
   - Created as `Pending`
   - Can transition to `Paid` (when payment received)
   - Can transition to `Overdue` (when deadline passes)
   - Cannot go from `Paid` back to `Pending` or `Overdue`

4. **Cascade vs Restrict Deletes**:
   - **Membership → FamilyMember**: Restrict (protect family member data)
   - **Membership → Fees**: Cascade (fees have no meaning without membership)

### RGPD/GDPR Considerations

- Membership data is not considered sensitive personal data
- No encryption required for membership or fee data
- Audit fields track when records were created/updated
- Future: May need to implement data retention policies

---

## Next Steps After Implementation

1. **Commit Changes**:
   ```bash
   git add .
   git commit -m "feat(membership): Add Membership and MembershipFee entities

   - Create Membership entity with one-to-one relationship to FamilyMember
   - Create MembershipFee entity with one-to-many relationship to Membership
   - Add FeeStatus enum (Pending, Paid, Overdue)
   - Configure EF Core relationships and constraints
   - Add database migration
   - Add unit and integration tests

   Implements Story 1.1.1 from Epic 1.1 Membership Entities

   Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
   ```

2. **Push Branch**:
   ```bash
   git push -u origin feature/story-1.1.1-membership-entity-backend
   ```

3. **Create Pull Request** (Optional):
   - Can wait until Story 1.1.2 (Repository) is also complete
   - Or create PR now for early review

4. **Next Story**: Story 1.1.2 - Create Membership Repository
   - Implement `IMembershipsRepository` interface
   - Implement `MembershipsRepository` with EF Core queries
   - Add repository unit and integration tests

---

## Implementation Verification

Before marking this story as complete, verify:

### Code Quality
- [ ] C# analyzers pass with no warnings
- [ ] Nullable reference types enabled and properly used
- [ ] No compilation errors or warnings
- [ ] Code follows project naming conventions (PascalCase for classes, camelCase for parameters)

### Functionality
- [ ] Entities can be instantiated and properties set
- [ ] EF Core configurations are valid (no runtime errors)
- [ ] Migration successfully creates tables in database
- [ ] Database constraints work as expected (tested via integration tests)

### Testing
- [ ] All unit tests pass (4 tests in `MembershipEntityTests`)
- [ ] All integration tests pass (5 tests in `MembershipDatabaseTests`)
- [ ] Test coverage ≥ 90% for new code (entity classes are simple, so this should be easy to achieve)
- [ ] Tests follow AAA pattern (Arrange-Act-Assert)
- [ ] Test names follow convention: `MethodName_StateUnderTest_ExpectedBehavior`

### Integration
- [ ] EF Core migration applied successfully to local database
- [ ] Tables `memberships` and `membership_fees` exist in PostgreSQL
- [ ] Indexes and constraints created correctly
- [ ] No conflicts with existing migrations

### Documentation
- [ ] `data-model.md` updated with new entities
- [ ] Entity relationships documented
- [ ] Business rules documented
- [ ] No broken links or formatting issues

---

**Estimated Time**: 2-3 hours
**Dependencies**: None (first story in Epic 1.1)
**Blocks**: Story 1.1.2 (Membership Repository)
**Status**: Ready for Implementation

