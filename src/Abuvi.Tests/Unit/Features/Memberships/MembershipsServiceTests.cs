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
        var request = new CreateMembershipRequest(DateTime.UtcNow.Year);

        _familyUnitsRepository.GetFamilyMemberByIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(familyMember);
        _membershipsRepository.GetByFamilyMemberIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns((Membership?)null);
        _membershipsRepository.GetNextMemberNumberAsync(Arg.Any<CancellationToken>())
            .Returns(1);
        _familyUnitsRepository.GetFamilyUnitByIdAsync(familyMember.FamilyUnitId, Arg.Any<CancellationToken>())
            .Returns(CreateTestFamilyUnit(familyMember.FamilyUnitId));
        _familyUnitsRepository.GetNextFamilyNumberAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        var result = await _service.CreateAsync(familyMemberId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FamilyMemberId.Should().Be(familyMemberId);
        result.IsActive.Should().BeTrue();
        result.StartDate.Should().Be(new DateTime(request.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        await _membershipsRepository.Received(1).AddAsync(
            Arg.Is<Membership>(m => m.FamilyMemberId == familyMemberId && m.IsActive),
            Arg.Any<CancellationToken>());

        await _membershipsRepository.Received(1).AddFeeAsync(
            Arg.Is<MembershipFee>(f =>
                f.Year == request.Year &&
                f.Status == FeeStatus.Pending &&
                f.Amount == 0m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenValidYear_SetsStartDateToJanFirst()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();
        var familyMember = CreateTestFamilyMember(familyMemberId);
        var request = new CreateMembershipRequest(2023);

        _familyUnitsRepository.GetFamilyMemberByIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns(familyMember);
        _membershipsRepository.GetByFamilyMemberIdAsync(familyMemberId, Arg.Any<CancellationToken>())
            .Returns((Membership?)null);
        _membershipsRepository.GetNextMemberNumberAsync(Arg.Any<CancellationToken>())
            .Returns(1);
        _familyUnitsRepository.GetFamilyUnitByIdAsync(familyMember.FamilyUnitId, Arg.Any<CancellationToken>())
            .Returns(CreateTestFamilyUnit(familyMember.FamilyUnitId));
        _familyUnitsRepository.GetNextFamilyNumberAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        var result = await _service.CreateAsync(familyMemberId, request, CancellationToken.None);

        // Assert
        result.StartDate.Should().Be(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task CreateAsync_WhenFamilyMemberDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var familyMemberId = Guid.NewGuid();
        var request = new CreateMembershipRequest(DateTime.UtcNow.Year);

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
        var request = new CreateMembershipRequest(DateTime.UtcNow.Year);

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

    [Fact]
    public async Task GetFeesAsync_WhenFeesExist_ReturnsFees()
    {
        // Arrange
        var membershipId = Guid.NewGuid();
        var fees = new List<MembershipFee>
        {
            CreateTestFee(membershipId, 2024),
            CreateTestFee(membershipId, 2025)
        };

        _membershipsRepository.GetFeesByMembershipAsync(membershipId, Arg.Any<CancellationToken>())
            .Returns(fees.AsReadOnly());

        // Act
        var result = await _service.GetFeesAsync(membershipId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(f => f.MembershipId.Should().Be(membershipId));
    }

    [Fact]
    public async Task GetCurrentYearFeeAsync_WhenFeeExists_ReturnsFee()
    {
        // Arrange
        var membershipId = Guid.NewGuid();
        var currentYear = DateTime.UtcNow.Year;
        var fee = CreateTestFee(membershipId, currentYear);

        _membershipsRepository.GetCurrentYearFeeAsync(membershipId, Arg.Any<CancellationToken>())
            .Returns(fee);

        // Act
        var result = await _service.GetCurrentYearFeeAsync(membershipId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.MembershipId.Should().Be(membershipId);
        result.Year.Should().Be(currentYear);
    }

    [Fact]
    public async Task GetCurrentYearFeeAsync_WhenFeeDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var membershipId = Guid.NewGuid();

        _membershipsRepository.GetCurrentYearFeeAsync(membershipId, Arg.Any<CancellationToken>())
            .Returns((MembershipFee?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetCurrentYearFeeAsync(membershipId, CancellationToken.None));
    }

    [Fact]
    public async Task PayFeeAsync_WhenFeeIsPending_MarksFeeAsPaid()
    {
        // Arrange
        var feeId = Guid.NewGuid();
        var fee = CreateTestFee(Guid.NewGuid(), 2025);
        fee.Id = feeId;
        fee.Status = FeeStatus.Pending;
        var request = new PayFeeRequest(DateTime.UtcNow.AddDays(-1), "REF-123");

        _membershipsRepository.GetFeeByIdAsync(feeId, Arg.Any<CancellationToken>())
            .Returns(fee);

        // Act
        var result = await _service.PayFeeAsync(feeId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(FeeStatus.Paid);
        result.PaidDate.Should().BeCloseTo(request.PaidDate, TimeSpan.FromSeconds(1));
        result.PaymentReference.Should().Be("REF-123");

        await _membershipsRepository.Received(1).UpdateFeeAsync(
            Arg.Is<MembershipFee>(f =>
                f.Id == feeId &&
                f.Status == FeeStatus.Paid &&
                f.PaidDate == request.PaidDate &&
                f.PaymentReference == "REF-123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PayFeeAsync_WhenFeeDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var feeId = Guid.NewGuid();
        var request = new PayFeeRequest(DateTime.UtcNow, "REF-123");

        _membershipsRepository.GetFeeByIdAsync(feeId, Arg.Any<CancellationToken>())
            .Returns((MembershipFee?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.PayFeeAsync(feeId, request, CancellationToken.None));

        await _membershipsRepository.DidNotReceive().UpdateFeeAsync(
            Arg.Any<MembershipFee>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PayFeeAsync_WhenFeeAlreadyPaid_ThrowsBusinessRuleException()
    {
        // Arrange
        var feeId = Guid.NewGuid();
        var fee = CreateTestFee(Guid.NewGuid(), 2025);
        fee.Id = feeId;
        fee.Status = FeeStatus.Paid;
        fee.PaidDate = DateTime.UtcNow.AddDays(-5);
        var request = new PayFeeRequest(DateTime.UtcNow, "REF-456");

        _membershipsRepository.GetFeeByIdAsync(feeId, Arg.Any<CancellationToken>())
            .Returns(fee);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.PayFeeAsync(feeId, request, CancellationToken.None));

        exception.Message.Should().Contain("ya está pagada");

        await _membershipsRepository.DidNotReceive().UpdateFeeAsync(
            Arg.Any<MembershipFee>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkActivateAsync_WhenFamilyUnitNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var request = new BulkActivateMembershipRequest(DateTime.UtcNow.Year);

        _familyUnitsRepository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns((FamilyUnit?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.BulkActivateAsync(familyUnitId, request, CancellationToken.None));
    }

    [Fact]
    public async Task BulkActivateAsync_WhenFamilyHasNoMembers_ReturnsZeroActivated()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var familyUnit = CreateTestFamilyUnit(familyUnitId);
        var request = new BulkActivateMembershipRequest(DateTime.UtcNow.Year);

        _familyUnitsRepository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);
        _familyUnitsRepository.GetFamilyMembersByFamilyUnitIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<FamilyMember>() as IReadOnlyList<FamilyMember>);

        // Act
        var result = await _service.BulkActivateAsync(familyUnitId, request, CancellationToken.None);

        // Assert
        result.Activated.Should().Be(0);
        result.Skipped.Should().Be(0);
        result.Results.Should().BeEmpty();
        await _membershipsRepository.DidNotReceive().AddAsync(Arg.Any<Membership>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkActivateAsync_WhenAllMembersHaveNoMembership_ActivatesAll()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var familyUnit = CreateTestFamilyUnit(familyUnitId);
        var member1 = CreateTestFamilyMember(Guid.NewGuid(), familyUnitId);
        var member2 = CreateTestFamilyMember(Guid.NewGuid(), familyUnitId);
        var members = new[] { member1, member2 };
        var request = new BulkActivateMembershipRequest(DateTime.UtcNow.Year);

        _familyUnitsRepository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);
        _familyUnitsRepository.GetFamilyMembersByFamilyUnitIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(members as IReadOnlyList<FamilyMember>);
        _membershipsRepository.GetByFamilyMemberIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Membership?)null);
        _membershipsRepository.GetNextMemberNumberAsync(Arg.Any<CancellationToken>())
            .Returns(1, 2);
        _familyUnitsRepository.GetNextFamilyNumberAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        var result = await _service.BulkActivateAsync(familyUnitId, request, CancellationToken.None);

        // Assert
        result.Activated.Should().Be(2);
        result.Skipped.Should().Be(0);
        result.Results.Should().HaveCount(2);
        result.Results.Should().AllSatisfy(r => r.Status.Should().Be(BulkMembershipResultStatus.Activated));
        await _membershipsRepository.Received(2).AddAsync(Arg.Any<Membership>(), Arg.Any<CancellationToken>());
        await _membershipsRepository.Received(2).AddFeeAsync(
            Arg.Is<MembershipFee>(f => f.Year == request.Year && f.Status == FeeStatus.Pending && f.Amount == 0m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkActivateAsync_WhenSomeMembersAlreadyHaveMembership_SkipsThose()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var familyUnit = CreateTestFamilyUnit(familyUnitId);
        var member1 = CreateTestFamilyMember(Guid.NewGuid(), familyUnitId);
        var member2 = CreateTestFamilyMember(Guid.NewGuid(), familyUnitId);
        var existingMembership = CreateTestMembership(member2.Id);
        var request = new BulkActivateMembershipRequest(DateTime.UtcNow.Year);

        _familyUnitsRepository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);
        _familyUnitsRepository.GetFamilyMembersByFamilyUnitIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(new[] { member1, member2 } as IReadOnlyList<FamilyMember>);
        _membershipsRepository.GetByFamilyMemberIdAsync(member1.Id, Arg.Any<CancellationToken>())
            .Returns((Membership?)null);
        _membershipsRepository.GetByFamilyMemberIdAsync(member2.Id, Arg.Any<CancellationToken>())
            .Returns(existingMembership);
        _membershipsRepository.GetNextMemberNumberAsync(Arg.Any<CancellationToken>())
            .Returns(1);
        _familyUnitsRepository.GetNextFamilyNumberAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        var result = await _service.BulkActivateAsync(familyUnitId, request, CancellationToken.None);

        // Assert
        result.Activated.Should().Be(1);
        result.Skipped.Should().Be(1);
        result.Results.Should().HaveCount(2);
        result.Results.Should().Contain(r => r.Status == BulkMembershipResultStatus.Activated);
        result.Results.Should().Contain(r => r.Status == BulkMembershipResultStatus.Skipped && r.MemberId == member2.Id);
        await _membershipsRepository.Received(1).AddAsync(Arg.Any<Membership>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkActivateAsync_WhenMemberHasMembership_SetsStartDateToJanFirst()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var familyUnit = CreateTestFamilyUnit(familyUnitId);
        var member = CreateTestFamilyMember(Guid.NewGuid(), familyUnitId);
        var request = new BulkActivateMembershipRequest(2023);

        _familyUnitsRepository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);
        _familyUnitsRepository.GetFamilyMembersByFamilyUnitIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(new[] { member } as IReadOnlyList<FamilyMember>);
        _membershipsRepository.GetByFamilyMemberIdAsync(member.Id, Arg.Any<CancellationToken>())
            .Returns((Membership?)null);
        _membershipsRepository.GetNextMemberNumberAsync(Arg.Any<CancellationToken>())
            .Returns(1);
        _familyUnitsRepository.GetNextFamilyNumberAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        await _service.BulkActivateAsync(familyUnitId, request, CancellationToken.None);

        // Assert — membership added with Jan 1st start date
        await _membershipsRepository.Received(1).AddAsync(
            Arg.Is<Membership>(m => m.StartDate == new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            Arg.Any<CancellationToken>());
    }

    // Helper methods
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
