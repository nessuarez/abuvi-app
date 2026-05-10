using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Services;
using Abuvi.API.Features.BlobStorage;
using Abuvi.API.Features.FamilyUnits;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Abuvi.Tests.Unit.Features.FamilyUnits;

public class DeleteFamilyMemberTests
{
    private readonly IFamilyUnitsRepository _repository;
    private readonly FamilyUnitsService _sut;

    public DeleteFamilyMemberTests()
    {
        _repository = Substitute.For<IFamilyUnitsRepository>();
        var encryptionService = Substitute.For<IEncryptionService>();
        var blobService = Substitute.For<IBlobStorageService>();
        var blobOptions = Options.Create(new BlobStorageOptions());
        var logger = Substitute.For<ILogger<FamilyUnitsService>>();

        _sut = new FamilyUnitsService(
            _repository, encryptionService, blobService, blobOptions, logger);
    }

    // ─────────────────────────────────────────────────────────────
    // DeleteFamilyMemberAsync — hard vs. soft delete
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteFamilyMemberAsync_NoHistory_HardDeletes()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var familyUnitId = Guid.NewGuid();
        var member = BuildMember(memberId, familyUnitId, userId: null);
        var familyUnit = BuildFamilyUnit(familyUnitId, representativeUserId: Guid.NewGuid());

        _repository.GetFamilyMemberByIdAsync(memberId, Arg.Any<CancellationToken>()).Returns(member);
        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);
        _repository.MemberHasActiveRegistrationsAsync(memberId, Arg.Any<CancellationToken>()).Returns(false);
        _repository.MemberHasAnyRegistrationsAsync(memberId, Arg.Any<CancellationToken>()).Returns(false);
        _repository.MemberHasMembershipAsync(memberId, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        await _sut.DeleteFamilyMemberAsync(memberId, isAdminOrBoard: false, CancellationToken.None);

        // Assert — hard delete called, soft delete not called
        await _repository.Received(1).DeleteFamilyMemberAsync(memberId, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().SoftDeleteFamilyMemberAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteFamilyMemberAsync_WithCancelledRegistration_SoftDeletes()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var familyUnitId = Guid.NewGuid();
        var member = BuildMember(memberId, familyUnitId, userId: null);
        var familyUnit = BuildFamilyUnit(familyUnitId, representativeUserId: Guid.NewGuid());

        _repository.GetFamilyMemberByIdAsync(memberId, Arg.Any<CancellationToken>()).Returns(member);
        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);
        _repository.MemberHasActiveRegistrationsAsync(memberId, Arg.Any<CancellationToken>()).Returns(false);
        _repository.MemberHasAnyRegistrationsAsync(memberId, Arg.Any<CancellationToken>()).Returns(true);
        _repository.MemberHasMembershipAsync(memberId, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        await _sut.DeleteFamilyMemberAsync(memberId, isAdminOrBoard: false, CancellationToken.None);

        // Assert — soft delete called, hard delete not called
        await _repository.Received(1).SoftDeleteFamilyMemberAsync(memberId, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().DeleteFamilyMemberAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteFamilyMemberAsync_WithActiveMembership_SoftDeletes()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var familyUnitId = Guid.NewGuid();
        var member = BuildMember(memberId, familyUnitId, userId: null);
        var familyUnit = BuildFamilyUnit(familyUnitId, representativeUserId: Guid.NewGuid());

        _repository.GetFamilyMemberByIdAsync(memberId, Arg.Any<CancellationToken>()).Returns(member);
        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);
        _repository.MemberHasActiveRegistrationsAsync(memberId, Arg.Any<CancellationToken>()).Returns(false);
        _repository.MemberHasAnyRegistrationsAsync(memberId, Arg.Any<CancellationToken>()).Returns(false);
        _repository.MemberHasMembershipAsync(memberId, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await _sut.DeleteFamilyMemberAsync(memberId, isAdminOrBoard: false, CancellationToken.None);

        // Assert
        await _repository.Received(1).SoftDeleteFamilyMemberAsync(memberId, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().DeleteFamilyMemberAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteFamilyMemberAsync_WithBothRegistrationAndMembership_SoftDeletes()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var familyUnitId = Guid.NewGuid();
        var member = BuildMember(memberId, familyUnitId, userId: null);
        var familyUnit = BuildFamilyUnit(familyUnitId, representativeUserId: Guid.NewGuid());

        _repository.GetFamilyMemberByIdAsync(memberId, Arg.Any<CancellationToken>()).Returns(member);
        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);
        _repository.MemberHasActiveRegistrationsAsync(memberId, Arg.Any<CancellationToken>()).Returns(false);
        _repository.MemberHasAnyRegistrationsAsync(memberId, Arg.Any<CancellationToken>()).Returns(true);
        _repository.MemberHasMembershipAsync(memberId, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await _sut.DeleteFamilyMemberAsync(memberId, isAdminOrBoard: false, CancellationToken.None);

        // Assert
        await _repository.Received(1).SoftDeleteFamilyMemberAsync(memberId, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().DeleteFamilyMemberAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────
    // DeleteFamilyMemberAsync — guard cases
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteFamilyMemberAsync_Representative_ThrowsBusinessRuleException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var familyUnitId = Guid.NewGuid();
        // Member's UserId matches the representative
        var member = BuildMember(memberId, familyUnitId, userId: userId);
        var familyUnit = BuildFamilyUnit(familyUnitId, representativeUserId: userId);

        _repository.GetFamilyMemberByIdAsync(memberId, Arg.Any<CancellationToken>()).Returns(member);
        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);

        // Act
        Func<Task> act = () => _sut.DeleteFamilyMemberAsync(memberId, isAdminOrBoard: false, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*representante*");
    }

    [Fact]
    public async Task DeleteFamilyMemberAsync_AdminBoard_ActiveRegistration_ThrowsBusinessRuleException()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var familyUnitId = Guid.NewGuid();
        var member = BuildMember(memberId, familyUnitId, userId: null);
        var familyUnit = BuildFamilyUnit(familyUnitId, representativeUserId: Guid.NewGuid());

        _repository.GetFamilyMemberByIdAsync(memberId, Arg.Any<CancellationToken>()).Returns(member);
        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);
        _repository.MemberHasActiveRegistrationsAsync(memberId, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        Func<Task> act = () => _sut.DeleteFamilyMemberAsync(memberId, isAdminOrBoard: true, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*activas*");
    }

    [Fact]
    public async Task DeleteFamilyMemberAsync_NotFound_ThrowsNotFoundException()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        _repository.GetFamilyMemberByIdAsync(memberId, Arg.Any<CancellationToken>()).ReturnsNull();

        // Act
        Func<Task> act = () => _sut.DeleteFamilyMemberAsync(memberId, isAdminOrBoard: false, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteFamilyMemberAsync_NonAdmin_DoesNotCheckActiveRegistrations()
    {
        // Arrange — isAdminOrBoard: false, so active-registration check should be skipped
        var memberId = Guid.NewGuid();
        var familyUnitId = Guid.NewGuid();
        var member = BuildMember(memberId, familyUnitId, userId: null);
        var familyUnit = BuildFamilyUnit(familyUnitId, representativeUserId: Guid.NewGuid());

        _repository.GetFamilyMemberByIdAsync(memberId, Arg.Any<CancellationToken>()).Returns(member);
        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);
        _repository.MemberHasAnyRegistrationsAsync(memberId, Arg.Any<CancellationToken>()).Returns(false);
        _repository.MemberHasMembershipAsync(memberId, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        await _sut.DeleteFamilyMemberAsync(memberId, isAdminOrBoard: false, CancellationToken.None);

        // Assert — active registration check not called for non-admin
        await _repository.DidNotReceive()
            .MemberHasActiveRegistrationsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────
    // AnonymiseFamilyMemberAsync
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AnonymiseFamilyMemberAsync_ValidMember_CallsRepository()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var familyUnitId = Guid.NewGuid();
        var member = BuildMember(memberId, familyUnitId, userId: null);

        _repository.GetFamilyMemberByIdIgnoringFiltersAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(member);

        // Act
        await _sut.AnonymiseFamilyMemberAsync(familyUnitId, memberId, CancellationToken.None);

        // Assert
        await _repository.Received(1)
            .AnonymiseFamilyMemberAsync(memberId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnonymiseFamilyMemberAsync_WrongFamilyUnit_ThrowsNotFoundException()
    {
        // Arrange — member belongs to a different family unit
        var memberId = Guid.NewGuid();
        var actualFamilyUnitId = Guid.NewGuid();
        var requestedFamilyUnitId = Guid.NewGuid();
        var member = BuildMember(memberId, actualFamilyUnitId, userId: null);

        _repository.GetFamilyMemberByIdIgnoringFiltersAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(member);

        // Act
        Func<Task> act = () =>
            _sut.AnonymiseFamilyMemberAsync(requestedFamilyUnitId, memberId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AnonymiseFamilyMemberAsync_NotFound_ThrowsNotFoundException()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var familyUnitId = Guid.NewGuid();

        _repository.GetFamilyMemberByIdIgnoringFiltersAsync(memberId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        Func<Task> act = () =>
            _sut.AnonymiseFamilyMemberAsync(familyUnitId, memberId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────

    private static FamilyMember BuildMember(Guid id, Guid familyUnitId, Guid? userId) => new()
    {
        Id = id,
        FamilyUnitId = familyUnitId,
        UserId = userId,
        FirstName = "Test",
        LastName = "Member",
        DateOfBirth = new DateOnly(1990, 1, 1),
        Relationship = FamilyRelationship.Child,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static FamilyUnit BuildFamilyUnit(Guid id, Guid representativeUserId) => new()
    {
        Id = id,
        Name = "Test Family",
        RepresentativeUserId = representativeUserId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
