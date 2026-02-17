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
