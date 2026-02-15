using Abuvi.API.Features.Camps;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

/// <summary>
/// Unit tests for CampsService
/// Following TDD: Tests written FIRST before implementation
/// </summary>
public class CampsServiceTests
{
    private readonly ICampsRepository _repository;
    private readonly CampsService _sut;

    public CampsServiceTests()
    {
        _repository = Substitute.For<ICampsRepository>();
        _sut = new CampsService(_repository);
    }

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WithValidPricingTemplate_CreatesCamp()
    {
        // Arrange
        var request = new CreateCampRequest(
            Name: "Test Camp",
            Description: "A test camp",
            Location: "Test Location",
            Latitude: 40.7128m,
            Longitude: -74.0060m,
            PricePerAdult: 180.00m,
            PricePerChild: 120.00m,
            PricePerBaby: 60.00m
        );

        var createdCamp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Location = request.Location,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            PricePerAdult = request.PricePerAdult,
            PricePerChild = request.PricePerChild,
            PricePerBaby = request.PricePerBaby,
            IsActive = true
        };

        _repository.CreateAsync(Arg.Any<Camp>(), Arg.Any<CancellationToken>())
            .Returns(createdCamp);

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Camp");
        result.PricePerAdult.Should().Be(180.00m);
        result.PricePerChild.Should().Be(120.00m);
        result.PricePerBaby.Should().Be(60.00m);
        result.IsActive.Should().BeTrue();

        await _repository.Received(1).CreateAsync(Arg.Any<Camp>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithNegativePrices_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateCampRequest(
            Name: "Test Camp",
            Description: null,
            Location: null,
            Latitude: null,
            Longitude: null,
            PricePerAdult: -10.00m, // Invalid negative price
            PricePerChild: 120.00m,
            PricePerBaby: 60.00m
        );

        // Act & Assert
        var act = async () => await _sut.CreateAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*price*cannot be negative*");
    }

    [Fact]
    public async Task CreateAsync_WithInvalidLatitude_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateCampRequest(
            Name: "Test Camp",
            Description: null,
            Location: null,
            Latitude: 95.0m, // Invalid latitude (> 90)
            Longitude: -74.0060m,
            PricePerAdult: 180.00m,
            PricePerChild: 120.00m,
            PricePerBaby: 60.00m
        );

        // Act & Assert
        var act = async () => await _sut.CreateAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Latitude*must be between -90 and 90*");
    }

    [Fact]
    public async Task CreateAsync_WithInvalidLongitude_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateCampRequest(
            Name: "Test Camp",
            Description: null,
            Location: null,
            Latitude: 40.7128m,
            Longitude: 185.0m, // Invalid longitude (> 180)
            PricePerAdult: 180.00m,
            PricePerChild: 120.00m,
            PricePerBaby: 60.00m
        );

        // Act & Assert
        var act = async () => await _sut.CreateAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Longitude*must be between -180 and 180*");
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsCampResponse()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var camp = new Camp
        {
            Id = campId,
            Name = "Test Camp",
            PricePerAdult = 180.00m,
            PricePerChild = 120.00m,
            PricePerBaby = 60.00m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetByIdAsync(campId, Arg.Any<CancellationToken>())
            .Returns(camp);

        // Act
        var result = await _sut.GetByIdAsync(campId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(campId);
        result.Name.Should().Be("Test Camp");
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Camp?)null);

        // Act
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllCamps()
    {
        // Arrange
        var camps = new List<Camp>
        {
            new() { Id = Guid.NewGuid(), Name = "Camp 1", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Camp 2", PricePerAdult = 200m, PricePerChild = 140m, PricePerBaby = 70m, IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        _repository.GetAllAsync(null, 0, 100, Arg.Any<CancellationToken>())
            .Returns(camps);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(c => c.Name == "Camp 1");
        result.Should().Contain(c => c.Name == "Camp 2");
    }

    [Fact]
    public async Task GetAllAsync_WithActiveFilter_ReturnsOnlyActiveCamps()
    {
        // Arrange
        var camps = new List<Camp>
        {
            new() { Id = Guid.NewGuid(), Name = "Active Camp", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        _repository.GetAllAsync(true, 0, 100, Arg.Any<CancellationToken>())
            .Returns(camps);

        // Act
        var result = await _sut.GetAllAsync(isActive: true);

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(c => c.Name == "Active Camp");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesCamp()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var existingCamp = new Camp
        {
            Id = campId,
            Name = "Original Name",
            PricePerAdult = 180.00m,
            PricePerChild = 120.00m,
            PricePerBaby = 60.00m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var updateRequest = new UpdateCampRequest(
            Name: "Updated Name",
            Description: "Updated description",
            Location: null,
            Latitude: null,
            Longitude: null,
            PricePerAdult: 200.00m,
            PricePerChild: 140.00m,
            PricePerBaby: 70.00m,
            IsActive: true
        );

        _repository.GetByIdAsync(campId, Arg.Any<CancellationToken>())
            .Returns(existingCamp);

        _repository.UpdateAsync(Arg.Any<Camp>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<Camp>());

        // Act
        var result = await _sut.UpdateAsync(campId, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");
        result.PricePerAdult.Should().Be(200.00m);

        await _repository.Received(1).UpdateAsync(Arg.Any<Camp>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Camp?)null);

        var updateRequest = new UpdateCampRequest(
            Name: "Updated Name",
            Description: null,
            Location: null,
            Latitude: null,
            Longitude: null,
            PricePerAdult: 200.00m,
            PricePerChild: 140.00m,
            PricePerBaby: 70.00m,
            IsActive: true
        );

        // Act
        var result = await _sut.UpdateAsync(Guid.NewGuid(), updateRequest);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_WithValidId_DeletesCamp()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var camp = new Camp
        {
            Id = campId,
            Name = "Camp to Delete",
            PricePerAdult = 180.00m,
            PricePerChild = 120.00m,
            PricePerBaby = 60.00m,
            IsActive = true
        };

        _repository.GetByIdAsync(campId, Arg.Any<CancellationToken>())
            .Returns(camp);

        _repository.DeleteAsync(campId, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _sut.DeleteAsync(campId);

        // Assert
        result.Should().BeTrue();
        await _repository.Received(1).DeleteAsync(campId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_ReturnsFalse()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Camp?)null);

        // Act
        var result = await _sut.DeleteAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WithActiveEditions_ThrowsInvalidOperationException()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var camp = new Camp
        {
            Id = campId,
            Name = "Camp with Editions",
            PricePerAdult = 180.00m,
            PricePerChild = 120.00m,
            PricePerBaby = 60.00m,
            IsActive = true,
            Editions = new List<CampEdition>
            {
                new() { Id = Guid.NewGuid(), CampId = campId, Year = 2026, Status = CampEditionStatus.Open, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(10), PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m }
            }
        };

        _repository.GetByIdAsync(campId, Arg.Any<CancellationToken>())
            .Returns(camp);

        // Act & Assert
        var act = async () => await _sut.DeleteAsync(campId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot delete camp*editions*");
    }

    #endregion
}
