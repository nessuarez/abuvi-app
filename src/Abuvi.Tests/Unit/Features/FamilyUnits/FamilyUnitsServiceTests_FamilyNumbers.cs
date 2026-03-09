using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Services;
using Abuvi.API.Features.BlobStorage;
using Abuvi.API.Features.FamilyUnits;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.FamilyUnits;

/// <summary>
/// Unit tests for family number update and membership status filter functionality
/// </summary>
public class FamilyUnitsServiceTests_FamilyNumbers
{
    private readonly IFamilyUnitsRepository _repository;
    private readonly FamilyUnitsService _sut;

    public FamilyUnitsServiceTests_FamilyNumbers()
    {
        _repository = Substitute.For<IFamilyUnitsRepository>();
        var encryptionService = Substitute.For<IEncryptionService>();
        var logger = Substitute.For<ILogger<FamilyUnitsService>>();
        var blobStorageService = Substitute.For<IBlobStorageService>();
        var blobOptions = Options.Create(new BlobStorageOptions());
        _sut = new FamilyUnitsService(_repository, encryptionService, blobStorageService, blobOptions, logger);
    }

    // -------------------------------------------------------------------------
    // UpdateFamilyNumberAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateFamilyNumberAsync_Success_UpdatesNumber()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var familyUnit = CreateTestFamilyUnit(familyUnitId);
        var request = new UpdateFamilyNumberRequest(42);

        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);
        _repository.IsFamilyNumberTakenAsync(42, familyUnitId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _sut.UpdateFamilyNumberAsync(familyUnitId, request, CancellationToken.None);

        // Assert
        result.FamilyNumber.Should().Be(42);
        await _repository.Received(1).UpdateFamilyUnitAsync(
            Arg.Is<FamilyUnit>(fu => fu.FamilyNumber == 42),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateFamilyNumberAsync_ThrowsWhenDuplicate()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var familyUnit = CreateTestFamilyUnit(familyUnitId);
        var request = new UpdateFamilyNumberRequest(5);

        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);
        _repository.IsFamilyNumberTakenAsync(5, familyUnitId, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _sut.UpdateFamilyNumberAsync(familyUnitId, request, CancellationToken.None));

        exception.Message.Should().Contain("ya está en uso");
        await _repository.DidNotReceive().UpdateFamilyUnitAsync(
            Arg.Any<FamilyUnit>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateFamilyNumberAsync_ThrowsWhenNotFound()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var request = new UpdateFamilyNumberRequest(5);

        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns((FamilyUnit?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.UpdateFamilyNumberAsync(familyUnitId, request, CancellationToken.None));
    }

    // -------------------------------------------------------------------------
    // GetAllFamilyUnitsAsync — membershipStatus filter
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllFamilyUnitsAsync_FiltersByMembershipStatus_Active()
    {
        // Arrange
        var items = BuildProjections(3);
        _repository.GetAllPagedAsync(1, 20, null, null, null, "active", Arg.Any<CancellationToken>())
            .Returns((items, 3));

        // Act
        var result = await _sut.GetAllFamilyUnitsAsync(
            page: 1, pageSize: 20, search: null,
            sortBy: null, sortOrder: null, membershipStatus: "active", ct: default);

        // Assert
        result.Items.Should().HaveCount(3);
        await _repository.Received(1).GetAllPagedAsync(
            1, 20, null, null, null, "active", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllFamilyUnitsAsync_FiltersByMembershipStatus_None()
    {
        // Arrange
        var items = BuildProjections(2);
        _repository.GetAllPagedAsync(1, 20, null, null, null, "none", Arg.Any<CancellationToken>())
            .Returns((items, 2));

        // Act
        var result = await _sut.GetAllFamilyUnitsAsync(
            page: 1, pageSize: 20, search: null,
            sortBy: null, sortOrder: null, membershipStatus: "none", ct: default);

        // Assert
        result.Items.Should().HaveCount(2);
        await _repository.Received(1).GetAllPagedAsync(
            1, 20, null, null, null, "none", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllFamilyUnitsAsync_NoFilter_ReturnsAll()
    {
        // Arrange
        var items = BuildProjections(5);
        _repository.GetAllPagedAsync(1, 20, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns((items, 5));

        // Act
        var result = await _sut.GetAllFamilyUnitsAsync(
            page: 1, pageSize: 20, search: null,
            sortBy: null, sortOrder: null, membershipStatus: null, ct: default);

        // Assert
        result.Items.Should().HaveCount(5);
        result.TotalCount.Should().Be(5);
        await _repository.Received(1).GetAllPagedAsync(
            1, 20, null, null, null, null, Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static FamilyUnit CreateTestFamilyUnit(Guid id) => new()
    {
        Id = id,
        Name = "Test Family",
        RepresentativeUserId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static List<FamilyUnitAdminProjection> BuildProjections(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new FamilyUnitAdminProjection(
                Id: Guid.NewGuid(),
                Name: $"Family {i}",
                RepresentativeUserId: Guid.NewGuid(),
                RepresentativeName: $"Rep {i}",
                FamilyNumber: i,
                MembersCount: i,
                CreatedAt: DateTime.UtcNow.AddDays(-i),
                UpdatedAt: DateTime.UtcNow.AddDays(-i)
            ))
            .ToList();
    }
}
