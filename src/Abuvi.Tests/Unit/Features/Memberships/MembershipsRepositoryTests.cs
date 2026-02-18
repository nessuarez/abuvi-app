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
