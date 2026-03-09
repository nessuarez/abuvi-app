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
/// Unit tests for FamilyUnitsService.GetAllFamilyUnitsAsync()
/// Following TDD: Tests written FIRST before implementation
/// </summary>
public class FamilyUnitsServiceTests_GetAll
{
    private readonly IFamilyUnitsRepository _repository;
    private readonly FamilyUnitsService _sut;

    public FamilyUnitsServiceTests_GetAll()
    {
        _repository = Substitute.For<IFamilyUnitsRepository>();
        var encryptionService = Substitute.For<IEncryptionService>();
        var logger = Substitute.For<ILogger<FamilyUnitsService>>();
        var blobStorageService = Substitute.For<IBlobStorageService>();
        var blobOptions = Options.Create(new BlobStorageOptions());
        _sut = new FamilyUnitsService(_repository, encryptionService, blobStorageService, blobOptions, logger);
    }

    // ---------------------------------------------------------------------------
    // Helper factory
    // ---------------------------------------------------------------------------

    private static List<FamilyUnitAdminProjection> BuildProjections(int count, string namePrefix = "Family")
    {
        return Enumerable.Range(1, count)
            .Select(i => new FamilyUnitAdminProjection(
                Id: Guid.NewGuid(),
                Name: $"{namePrefix} {i}",
                RepresentativeUserId: Guid.NewGuid(),
                RepresentativeName: $"Rep User {i}",
                FamilyNumber: null,
                MembersCount: i,
                CreatedAt: DateTime.UtcNow.AddDays(-i),
                UpdatedAt: DateTime.UtcNow.AddDays(-i)
            ))
            .ToList();
    }

    // ---------------------------------------------------------------------------
    // 1. Returns paged list with correct metadata (page 1 of 2)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAllFamilyUnitsAsync_25Items_Page1_Returns20ItemsWithCorrectMetadata()
    {
        // Arrange
        var items = BuildProjections(20);
        _repository.GetAllPagedAsync(1, 20, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns((items, 25));

        // Act
        var result = await _sut.GetAllFamilyUnitsAsync(page: 1, pageSize: 20, search: null,
            sortBy: null, sortOrder: null, membershipStatus: null, ct: default);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(20);
        result.TotalCount.Should().Be(25);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.TotalPages.Should().Be(2);
    }

    // ---------------------------------------------------------------------------
    // 2. Returns second page correctly
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAllFamilyUnitsAsync_25Items_Page2_Returns5Items()
    {
        // Arrange
        var items = BuildProjections(5);
        _repository.GetAllPagedAsync(2, 20, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns((items, 25));

        // Act
        var result = await _sut.GetAllFamilyUnitsAsync(page: 2, pageSize: 20, search: null,
            sortBy: null, sortOrder: null, membershipStatus: null, ct: default);

        // Assert
        result.Items.Should().HaveCount(5);
        result.TotalCount.Should().Be(25);
        result.Page.Should().Be(2);
        result.TotalPages.Should().Be(2);
    }

    // ---------------------------------------------------------------------------
    // 3. Filters by search term on family name
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAllFamilyUnitsAsync_SearchByFamilyName_PassesSearchToRepository()
    {
        // Arrange
        var items = BuildProjections(2, namePrefix: "Garcia");
        _repository.GetAllPagedAsync(1, 20, "Garcia", null, null, null, Arg.Any<CancellationToken>())
            .Returns((items, 2));

        // Act
        var result = await _sut.GetAllFamilyUnitsAsync(page: 1, pageSize: 20, search: "Garcia",
            sortBy: null, sortOrder: null, membershipStatus: null, ct: default);

        // Assert
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        await _repository.Received(1).GetAllPagedAsync(1, 20, "Garcia", null, null, null, Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------------
    // 4. Filters by search term on representative name
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAllFamilyUnitsAsync_SearchByRepresentativeName_PassesSearchToRepository()
    {
        // Arrange
        var items = BuildProjections(1);
        _repository.GetAllPagedAsync(1, 20, "Juan", null, null, null, Arg.Any<CancellationToken>())
            .Returns((items, 1));

        // Act
        var result = await _sut.GetAllFamilyUnitsAsync(page: 1, pageSize: 20, search: "Juan",
            sortBy: null, sortOrder: null, membershipStatus: null, ct: default);

        // Assert
        result.Items.Should().HaveCount(1);
        await _repository.Received(1).GetAllPagedAsync(1, 20, "Juan", null, null, null, Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------------
    // 5. Passes sort parameters to repository
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAllFamilyUnitsAsync_SortByNameAscending_PassesSortToRepository()
    {
        // Arrange
        _repository.GetAllPagedAsync(1, 20, null, "name", "asc", null, Arg.Any<CancellationToken>())
            .Returns((BuildProjections(3), 3));

        // Act
        await _sut.GetAllFamilyUnitsAsync(page: 1, pageSize: 20, search: null,
            sortBy: "name", sortOrder: "asc", membershipStatus: null, ct: default);

        // Assert
        await _repository.Received(1).GetAllPagedAsync(1, 20, null, "name", "asc", null, Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------------
    // 6. Passes sort by createdAt desc to repository
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAllFamilyUnitsAsync_SortByCreatedAtDescending_PassesSortToRepository()
    {
        // Arrange
        _repository.GetAllPagedAsync(1, 20, null, "createdAt", "desc", null, Arg.Any<CancellationToken>())
            .Returns((BuildProjections(3), 3));

        // Act
        await _sut.GetAllFamilyUnitsAsync(page: 1, pageSize: 20, search: null,
            sortBy: "createdAt", sortOrder: "desc", membershipStatus: null, ct: default);

        // Assert
        await _repository.Received(1).GetAllPagedAsync(1, 20, null, "createdAt", "desc", null, Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------------
    // 7. Returns empty list when no family units exist
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAllFamilyUnitsAsync_EmptyDatabase_ReturnsEmptyListWithZeroTotals()
    {
        // Arrange
        _repository.GetAllPagedAsync(1, 20, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns((new List<FamilyUnitAdminProjection>(), 0));

        // Act
        var result = await _sut.GetAllFamilyUnitsAsync(page: 1, pageSize: 20, search: null,
            sortBy: null, sortOrder: null, membershipStatus: null, ct: default);

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    // ---------------------------------------------------------------------------
    // 8. Clamps invalid page values to 1
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAllFamilyUnitsAsync_InvalidPage_ClampsToOne()
    {
        // Arrange
        _repository.GetAllPagedAsync(1, 20, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns((BuildProjections(5), 5));

        // Act
        var result = await _sut.GetAllFamilyUnitsAsync(page: 0, pageSize: 20, search: null,
            sortBy: null, sortOrder: null, membershipStatus: null, ct: default);

        // Assert
        result.Page.Should().Be(1);
        await _repository.Received(1).GetAllPagedAsync(1, 20, null, null, null, null, Arg.Any<CancellationToken>());
    }
}
