using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Memberships;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Memberships;

public class MembershipsServiceTests_Reactivate
{
    private readonly IMembershipsRepository _membershipsRepository;
    private readonly IFamilyUnitsRepository _familyUnitsRepository;
    private readonly MembershipsService _service;

    public MembershipsServiceTests_Reactivate()
    {
        _membershipsRepository = Substitute.For<IMembershipsRepository>();
        _familyUnitsRepository = Substitute.For<IFamilyUnitsRepository>();
        _service = new MembershipsService(_membershipsRepository, _familyUnitsRepository);
    }

    [Fact]
    public async Task ReactivateAsync_WhenMembershipNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();
        var request = new ReactivateMembershipRequest(DateTime.UtcNow.Year);

        _membershipsRepository.GetByFamilyMemberIdIgnoringActiveAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns((Membership?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ReactivateAsync(familyMemberId, request, CancellationToken.None));

        await _membershipsRepository.DidNotReceive().UpdateAsync(
            Arg.Any<Membership>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReactivateAsync_WhenMembershipAlreadyActive_ThrowsBusinessRuleException()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();
        var activeMembership = CreateTestMembership(familyMemberId, isActive: true);
        var request = new ReactivateMembershipRequest(DateTime.UtcNow.Year);

        _membershipsRepository.GetByFamilyMemberIdIgnoringActiveAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(activeMembership);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.ReactivateAsync(familyMemberId, request, CancellationToken.None));

        exception.Message.Should().NotBeNullOrEmpty();

        await _membershipsRepository.DidNotReceive().UpdateAsync(
            Arg.Any<Membership>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReactivateAsync_WhenMembershipInactive_SetsIsActiveTrue()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();
        var inactiveMembership = CreateTestMembership(familyMemberId, isActive: false);
        var request = new ReactivateMembershipRequest(DateTime.UtcNow.Year);
        var reactivatedMembership = CreateTestMembership(familyMemberId, isActive: true);

        _membershipsRepository.GetByFamilyMemberIdIgnoringActiveAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(inactiveMembership);
        _membershipsRepository.GetFeeByYearAsync(inactiveMembership.Id, request.Year, Arg.Any<CancellationToken>())
            .Returns((MembershipFee?)null);
        _membershipsRepository.GetByFamilyMemberIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(reactivatedMembership);

        // Act
        await _service.ReactivateAsync(familyMemberId, request, CancellationToken.None);

        // Assert
        await _membershipsRepository.Received(1).UpdateAsync(
            Arg.Is<Membership>(m =>
                m.FamilyMemberId == familyMemberId &&
                m.IsActive == true &&
                m.EndDate == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReactivateAsync_WhenMembershipInactive_CreatesFeeForRequestedYear()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var inactiveMembership = CreateTestMembership(familyMemberId, isActive: false);
        var request = new ReactivateMembershipRequest(year);
        var reactivatedMembership = CreateTestMembership(familyMemberId, isActive: true);

        _membershipsRepository.GetByFamilyMemberIdIgnoringActiveAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(inactiveMembership);
        _membershipsRepository.GetFeeByYearAsync(inactiveMembership.Id, year, Arg.Any<CancellationToken>())
            .Returns((MembershipFee?)null);
        _membershipsRepository.GetByFamilyMemberIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(reactivatedMembership);

        // Act
        await _service.ReactivateAsync(familyMemberId, request, CancellationToken.None);

        // Assert
        await _membershipsRepository.Received(1).AddFeeAsync(
            Arg.Is<MembershipFee>(f =>
                f.MembershipId == inactiveMembership.Id &&
                f.Year == year &&
                f.Status == FeeStatus.Pending &&
                f.Amount == 0m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReactivateAsync_WhenFeeAlreadyExistsForYear_SkipsFeeCreation()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var inactiveMembership = CreateTestMembership(familyMemberId, isActive: false);
        var existingFee = CreateTestFee(inactiveMembership.Id, year);
        var request = new ReactivateMembershipRequest(year);
        var reactivatedMembership = CreateTestMembership(familyMemberId, isActive: true);

        _membershipsRepository.GetByFamilyMemberIdIgnoringActiveAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(inactiveMembership);
        _membershipsRepository.GetFeeByYearAsync(inactiveMembership.Id, year, Arg.Any<CancellationToken>())
            .Returns(existingFee);
        _membershipsRepository.GetByFamilyMemberIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(reactivatedMembership);

        // Act
        await _service.ReactivateAsync(familyMemberId, request, CancellationToken.None);

        // Assert — fee creation must NOT be called
        await _membershipsRepository.DidNotReceive().AddFeeAsync(
            Arg.Any<MembershipFee>(),
            Arg.Any<CancellationToken>());
    }

    // Helpers
    private static Membership CreateTestMembership(Guid familyMemberId, bool isActive) => new()
    {
        Id = Guid.NewGuid(),
        FamilyMemberId = familyMemberId,
        StartDate = new DateTime(DateTime.UtcNow.Year - 1, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate = isActive ? null : DateTime.UtcNow.AddMonths(-1),
        IsActive = isActive,
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
        Status = FeeStatus.Paid,
        PaidDate = DateTime.UtcNow.AddDays(-10),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
