using Abuvi.API.Features.Camps;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

/// <summary>
/// Unit tests for CampEditionsService.GetCurrentAsync()
/// Following TDD: Tests written FIRST before implementation
/// </summary>
public class CampEditionsServiceTests_GetCurrent
{
    private readonly ICampEditionsRepository _repository;
    private readonly ICampsRepository _campsRepository;
    private readonly CampEditionsService _sut;

    private static readonly int CurrentYear = DateTime.UtcNow.Year;
    private static readonly Guid DefaultCampId = Guid.NewGuid();

    public CampEditionsServiceTests_GetCurrent()
    {
        _repository = Substitute.For<ICampEditionsRepository>();
        _campsRepository = Substitute.For<ICampsRepository>();
        _sut = new CampEditionsService(_repository, _campsRepository);
    }

    // ---------------------------------------------------------------------------
    // Helper factory
    // ---------------------------------------------------------------------------

    private static CampEdition BuildEdition(
        Guid campId,
        int year,
        CampEditionStatus status,
        int? maxCapacity = 100,
        decimal? latitude = 46.8182m,
        decimal? longitude = 8.2275m)
    {
        return new CampEdition
        {
            Id = Guid.NewGuid(),
            CampId = campId,
            Year = year,
            StartDate = new DateTime(year, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(year, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            UseCustomAgeRanges = false,
            Status = status,
            MaxCapacity = maxCapacity,
            IsArchived = false,
            Camp = new Camp
            {
                Id = campId,
                Name = "Test Camp",
                Location = "Swiss Alps",
                FormattedAddress = "Mountain Rd 1, Switzerland",
                Latitude = latitude,
                Longitude = longitude,
                IsActive = true
            }
        };
    }

    // ---------------------------------------------------------------------------
    // 1. Returns current year's Open edition
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetCurrentAsync_CurrentYearHasOpenEdition_ReturnsIt()
    {
        // Arrange
        var edition = BuildEdition(DefaultCampId, CurrentYear, CampEditionStatus.Open);
        _repository.GetCurrentAsync(CurrentYear, Arg.Any<CancellationToken>())
            .Returns(edition);

        // Act
        var result = await _sut.GetCurrentAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(edition.Id);
        result.Status.Should().Be(CampEditionStatus.Open);
        result.Year.Should().Be(CurrentYear);
    }

    // ---------------------------------------------------------------------------
    // 2. Returns current year's Closed edition when no Open exists
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetCurrentAsync_CurrentYearHasClosedEditionOnly_ReturnsClosedEdition()
    {
        // Arrange
        var edition = BuildEdition(DefaultCampId, CurrentYear, CampEditionStatus.Closed);
        _repository.GetCurrentAsync(CurrentYear, Arg.Any<CancellationToken>())
            .Returns(edition);

        // Act
        var result = await _sut.GetCurrentAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(CampEditionStatus.Closed);
        result.Year.Should().Be(CurrentYear);
    }

    // ---------------------------------------------------------------------------
    // 3. Falls back to previous year's Completed edition
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetCurrentAsync_CurrentYearHasNone_PreviousYearHasCompleted_ReturnsPreviousCompleted()
    {
        // Arrange
        var previousEdition = BuildEdition(DefaultCampId, CurrentYear - 1, CampEditionStatus.Completed);
        _repository.GetCurrentAsync(CurrentYear, Arg.Any<CancellationToken>())
            .Returns(previousEdition);

        // Act
        var result = await _sut.GetCurrentAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(CampEditionStatus.Completed);
        result.Year.Should().Be(CurrentYear - 1);
    }

    // ---------------------------------------------------------------------------
    // 4. Falls back to previous year's Closed edition when no Completed
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetCurrentAsync_CurrentYearHasNone_PreviousYearHasClosedOnly_ReturnsPreviousClosed()
    {
        // Arrange
        var previousEdition = BuildEdition(DefaultCampId, CurrentYear - 1, CampEditionStatus.Closed);
        _repository.GetCurrentAsync(CurrentYear, Arg.Any<CancellationToken>())
            .Returns(previousEdition);

        // Act
        var result = await _sut.GetCurrentAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(CampEditionStatus.Closed);
        result.Year.Should().Be(CurrentYear - 1);
    }

    // ---------------------------------------------------------------------------
    // 5. Returns null when no editions exist within lookback window
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetCurrentAsync_NoEditionsExist_ReturnsNull()
    {
        // Arrange
        _repository.GetCurrentAsync(CurrentYear, Arg.Any<CancellationToken>())
            .Returns((CampEdition?)null);

        // Act
        var result = await _sut.GetCurrentAsync();

        // Assert
        result.Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // 6. Computes AvailableSpots correctly when MaxCapacity is set
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetCurrentAsync_MaxCapacitySet_ComputesAvailableSpots()
    {
        // Arrange
        var edition = BuildEdition(DefaultCampId, CurrentYear, CampEditionStatus.Open, maxCapacity: 100);
        _repository.GetCurrentAsync(CurrentYear, Arg.Any<CancellationToken>())
            .Returns(edition);

        // Act
        var result = await _sut.GetCurrentAsync();

        // Assert
        result.Should().NotBeNull();
        result!.MaxCapacity.Should().Be(100);
        result.RegistrationCount.Should().Be(0); // Placeholder until Registrations feature
        result.AvailableSpots.Should().Be(100);  // MaxCapacity - RegistrationCount
    }

    // ---------------------------------------------------------------------------
    // 7. AvailableSpots is null when MaxCapacity is null
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetCurrentAsync_MaxCapacityNull_AvailableSpotsIsNull()
    {
        // Arrange
        var edition = BuildEdition(DefaultCampId, CurrentYear, CampEditionStatus.Open, maxCapacity: null);
        _repository.GetCurrentAsync(CurrentYear, Arg.Any<CancellationToken>())
            .Returns(edition);

        // Act
        var result = await _sut.GetCurrentAsync();

        // Assert
        result.Should().NotBeNull();
        result!.MaxCapacity.Should().BeNull();
        result.AvailableSpots.Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // 8. Includes camp coordinates in response
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetCurrentAsync_CampHasCoordinates_IncludesLatLongInResponse()
    {
        // Arrange
        var edition = BuildEdition(DefaultCampId, CurrentYear, CampEditionStatus.Open,
            latitude: 46.8182m, longitude: 8.2275m);
        _repository.GetCurrentAsync(CurrentYear, Arg.Any<CancellationToken>())
            .Returns(edition);

        // Act
        var result = await _sut.GetCurrentAsync();

        // Assert
        result.Should().NotBeNull();
        result!.CampLatitude.Should().Be(46.8182m);
        result.CampLongitude.Should().Be(8.2275m);
        result.CampName.Should().Be("Test Camp");
        result.CampLocation.Should().Be("Swiss Alps");
        result.CampFormattedAddress.Should().Be("Mountain Rd 1, Switzerland");
    }
}
