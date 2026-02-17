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
