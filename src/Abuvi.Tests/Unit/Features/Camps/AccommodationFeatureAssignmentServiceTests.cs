using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.Camps;
using Abuvi.Tests.Helpers.Builders;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

public class AccommodationFeatureAssignmentServiceTests
{
    private readonly IAccommodationFeaturesRepository _featuresRepo;
    private readonly ICampEditionAccommodationsRepository _accommodationsRepo;
    private readonly IAccommodationZonesRepository _zonesRepo;
    private readonly AccommodationFeatureAssignmentService _sut;

    public AccommodationFeatureAssignmentServiceTests()
    {
        _featuresRepo = Substitute.For<IAccommodationFeaturesRepository>();
        _accommodationsRepo = Substitute.For<ICampEditionAccommodationsRepository>();
        _zonesRepo = Substitute.For<IAccommodationZonesRepository>();
        _sut = new AccommodationFeatureAssignmentService(_featuresRepo, _accommodationsRepo, _zonesRepo);
    }

    [Fact]
    public async Task SetAccommodationFeaturesAsync_WithValidFeatureIds_ReturnsUpdatedList()
    {
        var accommodationId = Guid.NewGuid();
        var featureId = Guid.NewGuid();
        var feature = new AccommodationFeatureBuilder().WithId(featureId).Build();
        var accommodation = new CampEditionAccommodation { Id = accommodationId };

        _accommodationsRepo.GetByIdAsync(accommodationId, default).Returns(accommodation);
        _featuresRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), default).Returns(new[] { feature });
        _featuresRepo.SetAccommodationAssignmentsAsync(accommodationId, Arg.Any<IEnumerable<Guid>>(), default)
            .Returns(Task.CompletedTask);
        _featuresRepo.GetForAccommodationAsync(accommodationId, default).Returns(new[] { feature });

        var request = new SetFeatureAssignmentsRequest([featureId]);
        var result = await _sut.SetAccommodationFeaturesAsync(accommodationId, request, default);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(featureId);
    }

    [Fact]
    public async Task SetAccommodationFeaturesAsync_WithEmptyList_RemovesAllAndReturnsEmpty()
    {
        var accommodationId = Guid.NewGuid();
        var accommodation = new CampEditionAccommodation { Id = accommodationId };

        _accommodationsRepo.GetByIdAsync(accommodationId, default).Returns(accommodation);
        _featuresRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), default)
            .Returns(Array.Empty<AccommodationFeature>());
        _featuresRepo.SetAccommodationAssignmentsAsync(accommodationId, Arg.Any<IEnumerable<Guid>>(), default)
            .Returns(Task.CompletedTask);
        _featuresRepo.GetForAccommodationAsync(accommodationId, default)
            .Returns(Array.Empty<AccommodationFeature>());

        var result = await _sut.SetAccommodationFeaturesAsync(accommodationId, new SetFeatureAssignmentsRequest([]), default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SetAccommodationFeaturesAsync_WithNonExistentFeatureId_ThrowsValidationException()
    {
        var accommodationId = Guid.NewGuid();
        var accommodation = new CampEditionAccommodation { Id = accommodationId };
        var missingId = Guid.NewGuid();

        _accommodationsRepo.GetByIdAsync(accommodationId, default).Returns(accommodation);
        _featuresRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), default)
            .Returns(Array.Empty<AccommodationFeature>());

        var act = () => _sut.SetAccommodationFeaturesAsync(
            accommodationId, new SetFeatureAssignmentsRequest([missingId]), default);

        await act.Should().ThrowAsync<API.Common.Exceptions.ValidationException>();
    }

    [Fact]
    public async Task SetAccommodationFeaturesAsync_WithInactiveFeature_ThrowsValidationException()
    {
        var accommodationId = Guid.NewGuid();
        var featureId = Guid.NewGuid();
        var inactiveFeature = new AccommodationFeatureBuilder().WithId(featureId).WithIsActive(false).Build();
        var accommodation = new CampEditionAccommodation { Id = accommodationId };

        _accommodationsRepo.GetByIdAsync(accommodationId, default).Returns(accommodation);
        _featuresRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), default).Returns(new[] { inactiveFeature });

        var act = () => _sut.SetAccommodationFeaturesAsync(
            accommodationId, new SetFeatureAssignmentsRequest([featureId]), default);

        await act.Should().ThrowAsync<API.Common.Exceptions.ValidationException>();
    }

    [Fact]
    public async Task SetAccommodationFeaturesAsync_WhenAccommodationNotFound_ThrowsNotFoundException()
    {
        var accommodationId = Guid.NewGuid();
        _accommodationsRepo.GetByIdAsync(accommodationId, default).Returns((CampEditionAccommodation?)null);

        var act = () => _sut.SetAccommodationFeaturesAsync(
            accommodationId, new SetFeatureAssignmentsRequest([]), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SetZoneFeaturesAsync_WithValidFeatureIds_ReturnsUpdatedList()
    {
        var zoneId = Guid.NewGuid();
        var featureId = Guid.NewGuid();
        var feature = new AccommodationFeatureBuilder().WithId(featureId).Build();
        var zone = new AccommodationZone { Id = zoneId };

        _zonesRepo.GetByIdAsync(zoneId, default).Returns(zone);
        _featuresRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), default).Returns(new[] { feature });
        _featuresRepo.SetZoneAssignmentsAsync(zoneId, Arg.Any<IEnumerable<Guid>>(), default)
            .Returns(Task.CompletedTask);
        _featuresRepo.GetForZoneAsync(zoneId, default).Returns(new[] { feature });

        var result = await _sut.SetZoneFeaturesAsync(zoneId, new SetFeatureAssignmentsRequest([featureId]), default);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(featureId);
    }

    [Fact]
    public async Task SetZoneFeaturesAsync_WhenZoneNotFound_ThrowsNotFoundException()
    {
        var zoneId = Guid.NewGuid();
        _zonesRepo.GetByIdAsync(zoneId, default).Returns((AccommodationZone?)null);

        var act = () => _sut.SetZoneFeaturesAsync(zoneId, new SetFeatureAssignmentsRequest([]), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
