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
    public async Task MembershipFee_WhenMembershipDeleted_IsAlsoDeleted()
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

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
