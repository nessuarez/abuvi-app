# Backend Implementation Plan: Story 1.1.2 - Create Membership Repository

**Story ID:** 1.1.2
**Epic:** 1.1 Membership Entities and Database
**Type:** Backend Implementation
**Estimated Effort:** 2-3 hours
**Dependencies:** Story 1.1.1 (Membership Entity)

---

## Story Description

**As a** developer
**I want** to create the repository for membership data access
**So that** services can interact with membership data

---

## Acceptance Criteria

1. ✅ Define `IMembershipsRepository` interface
2. ✅ Implement `MembershipsRepository` with EF Core
3. ✅ Register repository in DI container
4. ✅ Write unit tests for all repository methods
5. ✅ Write integration tests with in-memory database

---

## Implementation Steps

### Step 0: Create Feature Branch

**Branch Name:** `feature/story-1.1.2-membership-repository-backend`

**Bash Command:**
```bash
git checkout feat-family-units
git pull origin feat-family-units
git checkout -b feature/story-1.1.2-membership-repository-backend
```

**Expected Result:** New branch created from latest feat-family-units

---

### Step 1: Create Repository Interface and Implementation

**File:** `src/Abuvi.API/Features/Memberships/MembershipsRepository.cs`

**Action:** Create new file

**Code:**
```csharp
using Microsoft.EntityFrameworkCore;
using Abuvi.API.Data;

namespace Abuvi.API.Features.Memberships;

public interface IMembershipsRepository
{
    Task<Membership?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Membership?> GetByFamilyMemberIdAsync(Guid familyMemberId, CancellationToken ct);
    Task<IReadOnlyList<Membership>> GetActiveAsync(CancellationToken ct);
    Task<IReadOnlyList<Membership>> GetOverdueAsync(CancellationToken ct);
    Task AddAsync(Membership membership, CancellationToken ct);
    Task UpdateAsync(Membership membership, CancellationToken ct);
    Task<MembershipFee?> GetFeeByIdAsync(Guid feeId, CancellationToken ct);
    Task<MembershipFee?> GetCurrentYearFeeAsync(Guid membershipId, CancellationToken ct);
    Task<IReadOnlyList<MembershipFee>> GetFeesByMembershipAsync(Guid membershipId, CancellationToken ct);
    Task AddFeeAsync(MembershipFee fee, CancellationToken ct);
    Task UpdateFeeAsync(MembershipFee fee, CancellationToken ct);
}

public class MembershipsRepository(AbuviDbContext db) : IMembershipsRepository
{
    public async Task<Membership?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.Memberships
            .AsNoTracking()
            .Include(m => m.Fees)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<Membership?> GetByFamilyMemberIdAsync(Guid familyMemberId, CancellationToken ct)
        => await db.Memberships
            .AsNoTracking()
            .Include(m => m.Fees)
            .FirstOrDefaultAsync(m => m.FamilyMemberId == familyMemberId && m.IsActive, ct);

    public async Task<IReadOnlyList<Membership>> GetActiveAsync(CancellationToken ct)
        => await db.Memberships
            .AsNoTracking()
            .Where(m => m.IsActive)
            .Include(m => m.FamilyMember)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Membership>> GetOverdueAsync(CancellationToken ct)
    {
        var currentYear = DateTime.UtcNow.Year;
        return await db.Memberships
            .AsNoTracking()
            .Where(m => m.IsActive)
            .Include(m => m.Fees)
            .Where(m => m.Fees.Any(f => f.Year == currentYear && f.Status == FeeStatus.Overdue))
            .ToListAsync(ct);
    }

    public async Task AddAsync(Membership membership, CancellationToken ct)
    {
        db.Memberships.Add(membership);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Membership membership, CancellationToken ct)
    {
        db.Memberships.Update(membership);
        await db.SaveChangesAsync(ct);
    }

    public async Task<MembershipFee?> GetFeeByIdAsync(Guid feeId, CancellationToken ct)
        => await db.MembershipFees
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == feeId, ct);

    public async Task<MembershipFee?> GetCurrentYearFeeAsync(Guid membershipId, CancellationToken ct)
    {
        var currentYear = DateTime.UtcNow.Year;
        return await db.MembershipFees
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.MembershipId == membershipId && f.Year == currentYear, ct);
    }

    public async Task<IReadOnlyList<MembershipFee>> GetFeesByMembershipAsync(Guid membershipId, CancellationToken ct)
        => await db.MembershipFees
            .AsNoTracking()
            .Where(f => f.MembershipId == membershipId)
            .OrderByDescending(f => f.Year)
            .ToListAsync(ct);

    public async Task AddFeeAsync(MembershipFee fee, CancellationToken ct)
    {
        db.MembershipFees.Add(fee);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateFeeAsync(MembershipFee fee, CancellationToken ct)
    {
        db.MembershipFees.Update(fee);
        await db.SaveChangesAsync(ct);
    }
}
```

**Implementation Notes:**
- Interface defines all required CRUD operations for Membership and MembershipFee
- Repository uses AsNoTracking() for read operations (performance optimization)
- Includes eager loading with `.Include()` for related entities
- GetOverdueAsync filters for current year overdue fees
- GetCurrentYearFeeAsync retrieves fee for current year
- Uses primary constructor pattern for DI

---

### Step 2: Register Repository in DI Container

**File:** `src/Abuvi.API/Program.cs`

**Action:** Modify existing file to add service registration

**Code to Add:**
```csharp
// Register Memberships repository
builder.Services.AddScoped<IMembershipsRepository, MembershipsRepository>();
```

**Location:** Add after other repository registrations (around line with other `AddScoped` calls)

**Implementation Notes:**
- Use Scoped lifetime for repository (matches DbContext lifetime)
- Add after existing repository registrations for consistency

---

### Step 3: Create Unit Tests for Repository

**File:** `src/Abuvi.Tests/Unit/Features/Memberships/MembershipsRepositoryTests.cs`

**Action:** Create new file

**Code:**
```csharp
using Abuvi.API.Data;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Memberships;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Memberships;

public class MembershipsRepositoryTests : IDisposable
{
    private readonly AbuviDbContext _dbContext;
    private readonly MembershipsRepository _repository;

    public MembershipsRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AbuviDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new AbuviDbContext(options);
        _repository = new MembershipsRepository(_dbContext);
    }

    [Fact]
    public async Task GetByIdAsync_WhenMembershipExists_ReturnsMembership()
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
        var result = await _repository.GetByIdAsync(membership.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(membership.Id);
        result.FamilyMemberId.Should().Be(familyMember.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WhenMembershipDoesNotExist_ReturnsNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByIdAsync(nonExistentId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByFamilyMemberIdAsync_WhenActiveMembershipExists_ReturnsMembership()
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
        var result = await _repository.GetByFamilyMemberIdAsync(familyMember.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.FamilyMemberId.Should().Be(familyMember.Id);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetByFamilyMemberIdAsync_WhenOnlyInactiveMembershipExists_ReturnsNull()
    {
        // Arrange
        var user = CreateTestUser();
        var familyUnit = CreateTestFamilyUnit(user.Id);
        var familyMember = CreateTestFamilyMember(familyUnit.Id);
        var membership = CreateTestMembership(familyMember.Id);
        membership.IsActive = false;

        _dbContext.Users.Add(user);
        _dbContext.FamilyUnits.Add(familyUnit);
        _dbContext.FamilyMembers.Add(familyMember);
        _dbContext.Memberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByFamilyMemberIdAsync(familyMember.Id, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsOnlyActiveMemberships()
    {
        // Arrange
        var user = CreateTestUser();
        var familyUnit = CreateTestFamilyUnit(user.Id);
        var familyMember1 = CreateTestFamilyMember(familyUnit.Id);
        var familyMember2 = CreateTestFamilyMember(familyUnit.Id);

        var activeMembership = CreateTestMembership(familyMember1.Id);
        var inactiveMembership = CreateTestMembership(familyMember2.Id);
        inactiveMembership.IsActive = false;

        _dbContext.Users.Add(user);
        _dbContext.FamilyUnits.Add(familyUnit);
        _dbContext.FamilyMembers.AddRange(familyMember1, familyMember2);
        _dbContext.Memberships.AddRange(activeMembership, inactiveMembership);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetActiveAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task AddAsync_SavesMembershipToDatabase()
    {
        // Arrange
        var user = CreateTestUser();
        var familyUnit = CreateTestFamilyUnit(user.Id);
        var familyMember = CreateTestFamilyMember(familyUnit.Id);

        _dbContext.Users.Add(user);
        _dbContext.FamilyUnits.Add(familyUnit);
        _dbContext.FamilyMembers.Add(familyMember);
        await _dbContext.SaveChangesAsync();

        var membership = CreateTestMembership(familyMember.Id);

        // Act
        await _repository.AddAsync(membership, CancellationToken.None);

        // Assert
        var saved = await _dbContext.Memberships.FindAsync(membership.Id);
        saved.Should().NotBeNull();
        saved!.FamilyMemberId.Should().Be(familyMember.Id);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesMembershipInDatabase()
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
        _dbContext.Entry(membership).State = EntityState.Detached;
        membership.IsActive = false;
        membership.EndDate = DateTime.UtcNow;
        await _repository.UpdateAsync(membership, CancellationToken.None);

        // Assert
        var updated = await _dbContext.Memberships.FindAsync(membership.Id);
        updated.Should().NotBeNull();
        updated!.IsActive.Should().BeFalse();
        updated.EndDate.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFeeByIdAsync_WhenFeeExists_ReturnsFee()
    {
        // Arrange
        var user = CreateTestUser();
        var familyUnit = CreateTestFamilyUnit(user.Id);
        var familyMember = CreateTestFamilyMember(familyUnit.Id);
        var membership = CreateTestMembership(familyMember.Id);
        var fee = CreateTestFee(membership.Id, 2026);

        _dbContext.Users.Add(user);
        _dbContext.FamilyUnits.Add(familyUnit);
        _dbContext.FamilyMembers.Add(familyMember);
        _dbContext.Memberships.Add(membership);
        _dbContext.MembershipFees.Add(fee);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetFeeByIdAsync(fee.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(fee.Id);
        result.MembershipId.Should().Be(membership.Id);
    }

    [Fact]
    public async Task GetCurrentYearFeeAsync_ReturnsFeeForCurrentYear()
    {
        // Arrange
        var currentYear = DateTime.UtcNow.Year;
        var user = CreateTestUser();
        var familyUnit = CreateTestFamilyUnit(user.Id);
        var familyMember = CreateTestFamilyMember(familyUnit.Id);
        var membership = CreateTestMembership(familyMember.Id);
        var currentYearFee = CreateTestFee(membership.Id, currentYear);
        var pastYearFee = CreateTestFee(membership.Id, currentYear - 1);

        _dbContext.Users.Add(user);
        _dbContext.FamilyUnits.Add(familyUnit);
        _dbContext.FamilyMembers.Add(familyMember);
        _dbContext.Memberships.Add(membership);
        _dbContext.MembershipFees.AddRange(currentYearFee, pastYearFee);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetCurrentYearFeeAsync(membership.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Year.Should().Be(currentYear);
    }

    [Fact]
    public async Task GetFeesByMembershipAsync_ReturnsFeesOrderedByYearDescending()
    {
        // Arrange
        var user = CreateTestUser();
        var familyUnit = CreateTestFamilyUnit(user.Id);
        var familyMember = CreateTestFamilyMember(familyUnit.Id);
        var membership = CreateTestMembership(familyMember.Id);
        var fee2024 = CreateTestFee(membership.Id, 2024);
        var fee2025 = CreateTestFee(membership.Id, 2025);
        var fee2026 = CreateTestFee(membership.Id, 2026);

        _dbContext.Users.Add(user);
        _dbContext.FamilyUnits.Add(familyUnit);
        _dbContext.FamilyMembers.Add(familyMember);
        _dbContext.Memberships.Add(membership);
        _dbContext.MembershipFees.AddRange(fee2024, fee2025, fee2026);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetFeesByMembershipAsync(membership.Id, CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result[0].Year.Should().Be(2026);
        result[1].Year.Should().Be(2025);
        result[2].Year.Should().Be(2024);
    }

    // Helper methods
    private User CreateTestUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = $"test{Guid.NewGuid()}@example.com",
        FirstName = "Test",
        LastName = "User",
        PasswordHash = "hashedpassword",
        Role = UserRole.Member,
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

    private MembershipFee CreateTestFee(Guid membershipId, int year) => new()
    {
        Id = Guid.NewGuid(),
        MembershipId = membershipId,
        Year = year,
        Amount = 50.00m,
        Status = FeeStatus.Pending,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
```

**Implementation Notes:**
- Tests cover all public methods in the repository
- Uses in-memory database for isolated tests
- Each test follows AAA pattern (Arrange-Act-Assert)
- Helper methods create test data consistently

---

### Step 4: Create Integration Tests

**File:** `src/Abuvi.Tests/Integration/Features/Memberships/MembershipsRepositoryIntegrationTests.cs`

**Action:** Create new file

**Code:**
```csharp
using Abuvi.API.Data;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Memberships;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Abuvi.Tests.Integration.Features.Memberships;

public class MembershipsRepositoryIntegrationTests : IDisposable
{
    private readonly AbuviDbContext _dbContext;
    private readonly MembershipsRepository _repository;

    public MembershipsRepositoryIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<AbuviDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new AbuviDbContext(options);
        _repository = new MembershipsRepository(_dbContext);
    }

    [Fact]
    public async Task GetOverdueAsync_ReturnsOnlyMembershipsWithOverdueCurrentYearFees()
    {
        // Arrange
        var currentYear = DateTime.UtcNow.Year;
        var user = CreateTestUser();
        var familyUnit = CreateTestFamilyUnit(user.Id);

        var familyMember1 = CreateTestFamilyMember(familyUnit.Id, "Member1");
        var familyMember2 = CreateTestFamilyMember(familyUnit.Id, "Member2");
        var familyMember3 = CreateTestFamilyMember(familyUnit.Id, "Member3");

        var membershipWithOverdueFee = CreateTestMembership(familyMember1.Id);
        var membershipWithPaidFee = CreateTestMembership(familyMember2.Id);
        var membershipWithPendingFee = CreateTestMembership(familyMember3.Id);

        var overdueFee = new MembershipFee
        {
            Id = Guid.NewGuid(),
            MembershipId = membershipWithOverdueFee.Id,
            Year = currentYear,
            Amount = 50.00m,
            Status = FeeStatus.Overdue,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var paidFee = new MembershipFee
        {
            Id = Guid.NewGuid(),
            MembershipId = membershipWithPaidFee.Id,
            Year = currentYear,
            Amount = 50.00m,
            Status = FeeStatus.Paid,
            PaidDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var pendingFee = new MembershipFee
        {
            Id = Guid.NewGuid(),
            MembershipId = membershipWithPendingFee.Id,
            Year = currentYear,
            Amount = 50.00m,
            Status = FeeStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        _dbContext.FamilyUnits.Add(familyUnit);
        _dbContext.FamilyMembers.AddRange(familyMember1, familyMember2, familyMember3);
        _dbContext.Memberships.AddRange(membershipWithOverdueFee, membershipWithPaidFee, membershipWithPendingFee);
        _dbContext.MembershipFees.AddRange(overdueFee, paidFee, pendingFee);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetOverdueAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().Id.Should().Be(membershipWithOverdueFee.Id);
        result.First().Fees.Should().Contain(f => f.Status == FeeStatus.Overdue && f.Year == currentYear);
    }

    [Fact]
    public async Task GetByIdAsync_IncludesFees()
    {
        // Arrange
        var user = CreateTestUser();
        var familyUnit = CreateTestFamilyUnit(user.Id);
        var familyMember = CreateTestFamilyMember(familyUnit.Id, "Member");
        var membership = CreateTestMembership(familyMember.Id);

        var fee2025 = CreateTestFee(membership.Id, 2025);
        var fee2026 = CreateTestFee(membership.Id, 2026);

        _dbContext.Users.Add(user);
        _dbContext.FamilyUnits.Add(familyUnit);
        _dbContext.FamilyMembers.Add(familyMember);
        _dbContext.Memberships.Add(membership);
        _dbContext.MembershipFees.AddRange(fee2025, fee2026);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(membership.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Fees.Should().HaveCount(2);
        result.Fees.Should().Contain(f => f.Year == 2025);
        result.Fees.Should().Contain(f => f.Year == 2026);
    }

    [Fact]
    public async Task GetActiveAsync_IncludesFamilyMember()
    {
        // Arrange
        var user = CreateTestUser();
        var familyUnit = CreateTestFamilyUnit(user.Id);
        var familyMember = CreateTestFamilyMember(familyUnit.Id, "TestMember");
        var membership = CreateTestMembership(familyMember.Id);

        _dbContext.Users.Add(user);
        _dbContext.FamilyUnits.Add(familyUnit);
        _dbContext.FamilyMembers.Add(familyMember);
        _dbContext.Memberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetActiveAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().FamilyMember.Should().NotBeNull();
        result.First().FamilyMember.FirstName.Should().Be("TestMember");
    }

    [Fact]
    public async Task AddFeeAsync_SavesFeeToDatabase()
    {
        // Arrange
        var user = CreateTestUser();
        var familyUnit = CreateTestFamilyUnit(user.Id);
        var familyMember = CreateTestFamilyMember(familyUnit.Id, "Member");
        var membership = CreateTestMembership(familyMember.Id);

        _dbContext.Users.Add(user);
        _dbContext.FamilyUnits.Add(familyUnit);
        _dbContext.FamilyMembers.Add(familyMember);
        _dbContext.Memberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        var fee = CreateTestFee(membership.Id, 2026);

        // Act
        await _repository.AddFeeAsync(fee, CancellationToken.None);

        // Assert
        var saved = await _dbContext.MembershipFees.FindAsync(fee.Id);
        saved.Should().NotBeNull();
        saved!.MembershipId.Should().Be(membership.Id);
        saved.Year.Should().Be(2026);
    }

    [Fact]
    public async Task UpdateFeeAsync_UpdatesFeeInDatabase()
    {
        // Arrange
        var user = CreateTestUser();
        var familyUnit = CreateTestFamilyUnit(user.Id);
        var familyMember = CreateTestFamilyMember(familyUnit.Id, "Member");
        var membership = CreateTestMembership(familyMember.Id);
        var fee = CreateTestFee(membership.Id, 2026);

        _dbContext.Users.Add(user);
        _dbContext.FamilyUnits.Add(familyUnit);
        _dbContext.FamilyMembers.Add(familyMember);
        _dbContext.Memberships.Add(membership);
        _dbContext.MembershipFees.Add(fee);
        await _dbContext.SaveChangesAsync();

        // Act
        _dbContext.Entry(fee).State = EntityState.Detached;
        fee.Status = FeeStatus.Paid;
        fee.PaidDate = DateTime.UtcNow;
        fee.PaymentReference = "PAY-123456";
        await _repository.UpdateFeeAsync(fee, CancellationToken.None);

        // Assert
        var updated = await _dbContext.MembershipFees.FindAsync(fee.Id);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(FeeStatus.Paid);
        updated.PaidDate.Should().NotBeNull();
        updated.PaymentReference.Should().Be("PAY-123456");
    }

    // Helper methods
    private User CreateTestUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = $"test{Guid.NewGuid()}@example.com",
        FirstName = "Test",
        LastName = "User",
        PasswordHash = "hashedpassword",
        Role = UserRole.Member,
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

    private FamilyMember CreateTestFamilyMember(Guid familyUnitId, string firstName) => new()
    {
        Id = Guid.NewGuid(),
        FamilyUnitId = familyUnitId,
        FirstName = firstName,
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

    private MembershipFee CreateTestFee(Guid membershipId, int year) => new()
    {
        Id = Guid.NewGuid(),
        MembershipId = membershipId,
        Year = year,
        Amount = 50.00m,
        Status = FeeStatus.Pending,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
```

**Implementation Notes:**
- Tests verify complex repository behavior with real database operations
- Verifies eager loading of related entities
- Tests GetOverdueAsync with multiple scenarios
- Tests fee management operations

---

### Step 5: Run All Tests

**Bash Commands:**
```bash
# Run unit tests
dotnet test --filter "FullyQualifiedName~MembershipsRepositoryTests" --logger "console;verbosity=detailed"

# Run integration tests
dotnet test --filter "FullyQualifiedName~MembershipsRepositoryIntegrationTests" --logger "console;verbosity=detailed"

# Run all membership tests
dotnet test --filter "FullyQualifiedName~Memberships" --logger "console;verbosity=detailed"
```

**Expected Result:** All tests should pass (21 tests total: 11 from Story 1.1.1 + 10 new tests)

---

### Step 6: Commit Changes

**Bash Command:**
```bash
git add src/Abuvi.API/Features/Memberships/MembershipsRepository.cs \
        src/Abuvi.API/Program.cs \
        src/Abuvi.Tests/Unit/Features/Memberships/MembershipsRepositoryTests.cs \
        src/Abuvi.Tests/Integration/Features/Memberships/MembershipsRepositoryIntegrationTests.cs

git commit -m "$(cat <<'EOF'
feat(membership): Add MembershipsRepository with comprehensive data access layer

- Create IMembershipsRepository interface with all CRUD operations
- Implement MembershipsRepository with EF Core
- Support for membership queries (active, overdue, by family member)
- Support for fee queries (current year, by membership, by ID)
- Register repository in DI container
- Add 10 unit tests for repository methods
- Add 5 integration tests for complex queries and eager loading

Implements Story 1.1.2 from Epic 1.1 Membership Entities

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>
EOF
)"
```

**Expected Result:** Commit created with descriptive message

---

### Step 7: Push and Create Pull Request

**Bash Commands:**
```bash
# Push branch
git push -u origin feature/story-1.1.2-membership-repository-backend

# Create PR
gh pr create --base feat-family-units \
  --title "feat(membership): Story 1.1.2 - Create MembershipsRepository" \
  --body "$(cat <<'EOF'
## Summary
Implements Story 1.1.2 from Epic 1.1 Membership Entities - creating the data access layer for memberships with comprehensive repository pattern implementation.

### Changes
- ✅ Create IMembershipsRepository interface with all CRUD operations
- ✅ Implement MembershipsRepository with EF Core and async operations
- ✅ Register repository in DI container with scoped lifetime
- ✅ Add comprehensive unit tests (10 tests)
- ✅ Add integration tests for complex queries (5 tests)

### Repository Features
- **Membership Operations**: GetById, GetByFamilyMemberId, GetActive, GetOverdue, Add, Update
- **Fee Operations**: GetFeeById, GetCurrentYearFee, GetFeesByMembership, AddFee, UpdateFee
- **Performance**: AsNoTracking() for read operations
- **Eager Loading**: Include() for related entities (Fees, FamilyMember)
- **Query Optimization**: Filters for current year, active status, overdue fees

### Test Coverage
All 15 tests passing (10 unit + 5 integration):
- Unit tests validate all repository methods with in-memory database
- Integration tests verify eager loading and complex query scenarios
- Tests verify GetOverdueAsync filters correctly for current year
- Tests verify proper ordering (fees by year descending)

### Test plan
- [x] Run all unit tests: `dotnet test --filter "FullyQualifiedName~MembershipsRepositoryTests"`
- [x] Run all integration tests: `dotnet test --filter "FullyQualifiedName~MembershipsRepositoryIntegrationTests"`
- [x] Verify DI registration in Program.cs
- [x] Code review for proper async/await patterns
- [ ] Manual QA: Verify repository works with service layer (Story 1.2.1)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

**Expected Result:** Pull request created successfully

---

## Testing Strategy

### Unit Tests (10 tests)
1. GetByIdAsync - returns membership when exists
2. GetByIdAsync - returns null when not found
3. GetByFamilyMemberIdAsync - returns active membership
4. GetByFamilyMemberIdAsync - returns null for inactive membership
5. GetActiveAsync - returns only active memberships
6. AddAsync - saves membership to database
7. UpdateAsync - updates membership in database
8. GetFeeByIdAsync - returns fee when exists
9. GetCurrentYearFeeAsync - returns fee for current year
10. GetFeesByMembershipAsync - returns fees ordered by year descending

### Integration Tests (5 tests)
1. GetOverdueAsync - returns only memberships with overdue current year fees
2. GetByIdAsync - includes fees with eager loading
3. GetActiveAsync - includes family member with eager loading
4. AddFeeAsync - saves fee to database
5. UpdateFeeAsync - updates fee in database

---

## Success Criteria

✅ All 15 tests passing
✅ Repository registered in DI container
✅ No code duplication
✅ Proper async/await patterns
✅ AsNoTracking() used for read operations
✅ Eager loading configured correctly
✅ Code follows Vertical Slice Architecture
✅ Commit message follows project standards
✅ PR created and linked to story
