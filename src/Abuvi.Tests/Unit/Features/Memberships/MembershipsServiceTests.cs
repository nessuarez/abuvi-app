using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Memberships;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Memberships;

public class MembershipsServiceTests
{
    private readonly IMembershipsRepository _membershipsRepository;
    private readonly IFamilyUnitsRepository _familyUnitsRepository;
    private readonly MembershipsService _service;

    public MembershipsServiceTests()
    {
        _membershipsRepository = Substitute.For<IMembershipsRepository>();
        _familyUnitsRepository = Substitute.For<IFamilyUnitsRepository>();
        _service = new MembershipsService(_membershipsRepository, _familyUnitsRepository);
    }

    [Fact]
    public async Task CreateAsync_WhenFamilyMemberExists_CreatesMembership()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();
        var familyMember = CreateTestFamilyMember(familyMemberId);
        var request = new CreateMembershipRequest(DateTime.UtcNow.AddDays(-1));

        _familyUnitsRepository.GetFamilyMemberByIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(familyMember);
        _membershipsRepository.GetByFamilyMemberIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns((Membership?)null);

        // Act
        var result = await _service.CreateAsync(familyMemberId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FamilyMemberId.Should().Be(familyMemberId);
        result.IsActive.Should().BeTrue();
        result.StartDate.Should().BeCloseTo(request.StartDate, TimeSpan.FromSeconds(1));

        await _membershipsRepository.Received(1).AddAsync(
            Arg.Is<Membership>(m => m.FamilyMemberId == familyMemberId && m.IsActive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenFamilyMemberDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();
        var request = new CreateMembershipRequest(DateTime.UtcNow);

        _familyUnitsRepository.GetFamilyMemberByIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns((FamilyMember?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.CreateAsync(familyMemberId, request, CancellationToken.None));

        await _membershipsRepository.DidNotReceive().AddAsync(
            Arg.Any<Membership>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenActiveMembershipExists_ThrowsBusinessRuleException()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();
        var familyMember = CreateTestFamilyMember(familyMemberId);
        var existingMembership = CreateTestMembership(familyMemberId);
        var request = new CreateMembershipRequest(DateTime.UtcNow);

        _familyUnitsRepository.GetFamilyMemberByIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(familyMember);
        _membershipsRepository.GetByFamilyMemberIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(existingMembership);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.CreateAsync(familyMemberId, request, CancellationToken.None));

        exception.Message.Should().Contain("ya tiene una membresía activa");

        await _membershipsRepository.DidNotReceive().AddAsync(
            Arg.Any<Membership>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByFamilyMemberIdAsync_WhenMembershipExists_ReturnsMembership()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();
        var membership = CreateTestMembership(familyMemberId);

        _membershipsRepository.GetByFamilyMemberIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(membership);

        // Act
        var result = await _service.GetByFamilyMemberIdAsync(familyMemberId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FamilyMemberId.Should().Be(familyMemberId);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetByFamilyMemberIdAsync_WhenMembershipDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();

        _membershipsRepository.GetByFamilyMemberIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns((Membership?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetByFamilyMemberIdAsync(familyMemberId, CancellationToken.None));
    }

    [Fact]
    public async Task DeactivateAsync_WhenMembershipExists_DeactivatesMembership()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();
        var membership = CreateTestMembership(familyMemberId);

        _membershipsRepository.GetByFamilyMemberIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(membership);

        // Act
        await _service.DeactivateAsync(familyMemberId, CancellationToken.None);

        // Assert
        await _membershipsRepository.Received(1).UpdateAsync(
            Arg.Is<Membership>(m =>
                m.FamilyMemberId == familyMemberId &&
                !m.IsActive &&
                m.EndDate != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateAsync_WhenMembershipDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();

        _membershipsRepository.GetByFamilyMemberIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns((Membership?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.DeactivateAsync(familyMemberId, CancellationToken.None));

        await _membershipsRepository.DidNotReceive().UpdateAsync(
            Arg.Any<Membership>(),
            Arg.Any<CancellationToken>());
    }

    // Helper methods
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
