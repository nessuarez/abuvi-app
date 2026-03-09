using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Memberships;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Memberships;

/// <summary>
/// Unit tests for membership number auto-assignment and update functionality
/// </summary>
public class MembershipsServiceTests_MemberNumbers
{
    private readonly IMembershipsRepository _membershipsRepository;
    private readonly IFamilyUnitsRepository _familyUnitsRepository;
    private readonly MembershipsService _service;

    public MembershipsServiceTests_MemberNumbers()
    {
        _membershipsRepository = Substitute.For<IMembershipsRepository>();
        _familyUnitsRepository = Substitute.For<IFamilyUnitsRepository>();
        _service = new MembershipsService(_membershipsRepository, _familyUnitsRepository);
    }

    // -------------------------------------------------------------------------
    // CreateAsync — MemberNumber auto-assignment
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_AssignsMemberNumber()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();
        var familyMember = CreateTestFamilyMember(familyMemberId);
        var familyUnit = CreateTestFamilyUnit(familyMember.FamilyUnitId);
        var request = new CreateMembershipRequest(DateTime.UtcNow.Year);

        _familyUnitsRepository.GetFamilyMemberByIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(familyMember);
        _membershipsRepository.GetByFamilyMemberIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns((Membership?)null);
        _membershipsRepository.GetNextMemberNumberAsync(Arg.Any<CancellationToken>())
            .Returns(42);
        _familyUnitsRepository.GetFamilyUnitByIdAsync(familyMember.FamilyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);
        _familyUnitsRepository.GetNextFamilyNumberAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        var result = await _service.CreateAsync(familyMemberId, request, CancellationToken.None);

        // Assert
        result.MemberNumber.Should().Be(42);
        await _membershipsRepository.Received(1).AddAsync(
            Arg.Is<Membership>(m => m.MemberNumber == 42),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_AssignsFamilyNumber_WhenFirstMembershipInFamily()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();
        var familyUnitId = Guid.NewGuid();
        var familyMember = CreateTestFamilyMember(familyMemberId, familyUnitId);
        var familyUnit = CreateTestFamilyUnit(familyUnitId); // FamilyNumber is null
        var request = new CreateMembershipRequest(DateTime.UtcNow.Year);

        _familyUnitsRepository.GetFamilyMemberByIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(familyMember);
        _membershipsRepository.GetByFamilyMemberIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns((Membership?)null);
        _membershipsRepository.GetNextMemberNumberAsync(Arg.Any<CancellationToken>())
            .Returns(1);
        _familyUnitsRepository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);
        _familyUnitsRepository.GetNextFamilyNumberAsync(Arg.Any<CancellationToken>())
            .Returns(10);

        // Act
        await _service.CreateAsync(familyMemberId, request, CancellationToken.None);

        // Assert
        await _familyUnitsRepository.Received(1).UpdateFamilyUnitAsync(
            Arg.Is<FamilyUnit>(fu => fu.FamilyNumber == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_DoesNotReassignFamilyNumber_WhenAlreadySet()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();
        var familyUnitId = Guid.NewGuid();
        var familyMember = CreateTestFamilyMember(familyMemberId, familyUnitId);
        var familyUnit = CreateTestFamilyUnit(familyUnitId);
        familyUnit.FamilyNumber = 5; // Already assigned
        var request = new CreateMembershipRequest(DateTime.UtcNow.Year);

        _familyUnitsRepository.GetFamilyMemberByIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(familyMember);
        _membershipsRepository.GetByFamilyMemberIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns((Membership?)null);
        _membershipsRepository.GetNextMemberNumberAsync(Arg.Any<CancellationToken>())
            .Returns(1);
        _familyUnitsRepository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        // Act
        await _service.CreateAsync(familyMemberId, request, CancellationToken.None);

        // Assert — should NOT update family unit since FamilyNumber is already set
        await _familyUnitsRepository.DidNotReceive().UpdateFamilyUnitAsync(
            Arg.Any<FamilyUnit>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // BulkActivateAsync — Number assignments
    // -------------------------------------------------------------------------

    [Fact]
    public async Task BulkActivateAsync_AssignsMemberNumbers()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var familyUnit = CreateTestFamilyUnit(familyUnitId);
        var member1 = CreateTestFamilyMember(Guid.NewGuid(), familyUnitId);
        var member2 = CreateTestFamilyMember(Guid.NewGuid(), familyUnitId);
        var request = new BulkActivateMembershipRequest(DateTime.UtcNow.Year);

        _familyUnitsRepository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);
        _familyUnitsRepository.GetFamilyMembersByFamilyUnitIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(new[] { member1, member2 } as IReadOnlyList<FamilyMember>);
        _membershipsRepository.GetByFamilyMemberIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Membership?)null);
        _membershipsRepository.GetNextMemberNumberAsync(Arg.Any<CancellationToken>())
            .Returns(10, 11);
        _familyUnitsRepository.GetNextFamilyNumberAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        var result = await _service.BulkActivateAsync(familyUnitId, request, CancellationToken.None);

        // Assert
        result.Activated.Should().Be(2);
        await _membershipsRepository.Received(2).GetNextMemberNumberAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkActivateAsync_AssignsFamilyNumber_OnceForFamily()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var familyUnit = CreateTestFamilyUnit(familyUnitId); // FamilyNumber is null
        var member1 = CreateTestFamilyMember(Guid.NewGuid(), familyUnitId);
        var member2 = CreateTestFamilyMember(Guid.NewGuid(), familyUnitId);
        var request = new BulkActivateMembershipRequest(DateTime.UtcNow.Year);

        _familyUnitsRepository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);
        _familyUnitsRepository.GetFamilyMembersByFamilyUnitIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(new[] { member1, member2 } as IReadOnlyList<FamilyMember>);
        _membershipsRepository.GetByFamilyMemberIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Membership?)null);
        _membershipsRepository.GetNextMemberNumberAsync(Arg.Any<CancellationToken>())
            .Returns(1, 2);
        _familyUnitsRepository.GetNextFamilyNumberAsync(Arg.Any<CancellationToken>())
            .Returns(5);

        // Act
        await _service.BulkActivateAsync(familyUnitId, request, CancellationToken.None);

        // Assert — family number assigned exactly once
        await _familyUnitsRepository.Received(1).GetNextFamilyNumberAsync(Arg.Any<CancellationToken>());
        await _familyUnitsRepository.Received(1).UpdateFamilyUnitAsync(
            Arg.Is<FamilyUnit>(fu => fu.FamilyNumber == 5),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // UpdateMemberNumberAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateMemberNumberAsync_Success_UpdatesNumber()
    {
        // Arrange
        var membershipId = Guid.NewGuid();
        var membership = CreateTestMembership(Guid.NewGuid());
        membership.Id = membershipId;
        membership.MemberNumber = 1;
        var request = new UpdateMemberNumberRequest(99);

        _membershipsRepository.GetByIdAsync(membershipId, Arg.Any<CancellationToken>())
            .Returns(membership);
        _membershipsRepository.IsMemberNumberTakenAsync(99, membershipId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _service.UpdateMemberNumberAsync(membershipId, request, CancellationToken.None);

        // Assert
        result.MemberNumber.Should().Be(99);
        await _membershipsRepository.Received(1).UpdateAsync(
            Arg.Is<Membership>(m => m.MemberNumber == 99),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateMemberNumberAsync_ThrowsWhenDuplicate()
    {
        // Arrange
        var membershipId = Guid.NewGuid();
        var membership = CreateTestMembership(Guid.NewGuid());
        membership.Id = membershipId;
        var request = new UpdateMemberNumberRequest(5);

        _membershipsRepository.GetByIdAsync(membershipId, Arg.Any<CancellationToken>())
            .Returns(membership);
        _membershipsRepository.IsMemberNumberTakenAsync(5, membershipId, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.UpdateMemberNumberAsync(membershipId, request, CancellationToken.None));

        exception.Message.Should().Contain("ya está en uso");
        await _membershipsRepository.DidNotReceive().UpdateAsync(
            Arg.Any<Membership>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateMemberNumberAsync_ThrowsWhenNotFound()
    {
        // Arrange
        var membershipId = Guid.NewGuid();
        var request = new UpdateMemberNumberRequest(5);

        _membershipsRepository.GetByIdAsync(membershipId, Arg.Any<CancellationToken>())
            .Returns((Membership?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.UpdateMemberNumberAsync(membershipId, request, CancellationToken.None));
    }

    // -------------------------------------------------------------------------
    // Helper methods
    // -------------------------------------------------------------------------

    private static FamilyUnit CreateTestFamilyUnit(Guid id) => new()
    {
        Id = id,
        Name = "Test Family",
        RepresentativeUserId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static FamilyMember CreateTestFamilyMember(Guid id) => new()
    {
        Id = id,
        FamilyUnitId = Guid.NewGuid(),
        FirstName = "John",
        LastName = "Doe",
        DateOfBirth = new DateOnly(1990, 1, 1),
        Relationship = FamilyRelationship.Parent,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static FamilyMember CreateTestFamilyMember(Guid id, Guid familyUnitId) => new()
    {
        Id = id,
        FamilyUnitId = familyUnitId,
        FirstName = "John",
        LastName = "Doe",
        DateOfBirth = new DateOnly(1990, 1, 1),
        Relationship = FamilyRelationship.Parent,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Membership CreateTestMembership(Guid familyMemberId) => new()
    {
        Id = Guid.NewGuid(),
        FamilyMemberId = familyMemberId,
        StartDate = DateTime.UtcNow.AddDays(-30),
        IsActive = true,
        Fees = new List<MembershipFee>(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
