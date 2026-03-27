using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Memberships;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Memberships;

public class MembershipsServiceTests_CreateFee
{
    private readonly IMembershipsRepository _membershipsRepository;
    private readonly IFamilyUnitsRepository _familyUnitsRepository;
    private readonly MembershipsService _service;

    public MembershipsServiceTests_CreateFee()
    {
        _membershipsRepository = Substitute.For<IMembershipsRepository>();
        _familyUnitsRepository = Substitute.For<IFamilyUnitsRepository>();
        _service = new MembershipsService(_membershipsRepository, _familyUnitsRepository);
    }

    [Fact]
    public async Task CreateFeeAsync_WhenMembershipNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var membershipId = Guid.NewGuid();
        var request = new CreateMembershipFeeRequest(DateTime.UtcNow.Year, 25.00m);

        _membershipsRepository.GetByIdAsync(membershipId, Arg.Any<CancellationToken>())
            .Returns((Membership?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.CreateFeeAsync(membershipId, request, CancellationToken.None));

        await _membershipsRepository.DidNotReceive().AddFeeAsync(
            Arg.Any<MembershipFee>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFeeAsync_WhenFeeAlreadyExistsForYear_ThrowsBusinessRuleException()
    {
        // Arrange
        var membershipId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var request = new CreateMembershipFeeRequest(year, 25.00m);
        var membership = CreateTestMembership(membershipId);
        var existingFee = CreateTestFee(membershipId, year);

        _membershipsRepository.GetByIdAsync(membershipId, Arg.Any<CancellationToken>())
            .Returns(membership);
        _membershipsRepository.GetFeeByYearAsync(membershipId, year, Arg.Any<CancellationToken>())
            .Returns(existingFee);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.CreateFeeAsync(membershipId, request, CancellationToken.None));

        exception.Message.Should().Contain(year.ToString());

        await _membershipsRepository.DidNotReceive().AddFeeAsync(
            Arg.Any<MembershipFee>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFeeAsync_WhenValidRequest_CreatesFeeWithPendingStatus()
    {
        // Arrange
        var membershipId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var request = new CreateMembershipFeeRequest(year, 30.00m);
        var membership = CreateTestMembership(membershipId);

        _membershipsRepository.GetByIdAsync(membershipId, Arg.Any<CancellationToken>())
            .Returns(membership);
        _membershipsRepository.GetFeeByYearAsync(membershipId, year, Arg.Any<CancellationToken>())
            .Returns((MembershipFee?)null);

        // Act
        var result = await _service.CreateFeeAsync(membershipId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(FeeStatus.Pending);
        result.MembershipId.Should().Be(membershipId);

        await _membershipsRepository.Received(1).AddFeeAsync(
            Arg.Is<MembershipFee>(f =>
                f.MembershipId == membershipId &&
                f.Status == FeeStatus.Pending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFeeAsync_WhenValidRequest_SetsCorrectYearAndAmount()
    {
        // Arrange
        var membershipId = Guid.NewGuid();
        var year = 2024;
        var amount = 25.50m;
        var request = new CreateMembershipFeeRequest(year, amount);
        var membership = CreateTestMembership(membershipId);

        _membershipsRepository.GetByIdAsync(membershipId, Arg.Any<CancellationToken>())
            .Returns(membership);
        _membershipsRepository.GetFeeByYearAsync(membershipId, year, Arg.Any<CancellationToken>())
            .Returns((MembershipFee?)null);

        // Act
        var result = await _service.CreateFeeAsync(membershipId, request, CancellationToken.None);

        // Assert
        result.Year.Should().Be(year);
        result.Amount.Should().Be(amount);
    }

    // Helpers
    private static Membership CreateTestMembership(Guid membershipId) => new()
    {
        Id = membershipId,
        FamilyMemberId = Guid.NewGuid(),
        StartDate = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        IsActive = true,
        Fees = new List<MembershipFee>(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static MembershipFee CreateTestFee(Guid membershipId, int year) => new()
    {
        Id = Guid.NewGuid(),
        MembershipId = membershipId,
        Year = year,
        Amount = 50.00m,
        Status = FeeStatus.Pending,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
