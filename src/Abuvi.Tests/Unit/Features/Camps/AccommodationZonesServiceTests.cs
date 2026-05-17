using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.Camps;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

public class AccommodationZonesServiceTests
{
    private readonly IAccommodationZonesRepository _zonesRepo;
    private readonly ICampEditionsRepository _editionsRepo;
    private readonly AccommodationZonesService _sut;

    private static readonly Guid EditionId = Guid.NewGuid();
    private static readonly Guid ZoneId = Guid.NewGuid();

    public AccommodationZonesServiceTests()
    {
        _zonesRepo = Substitute.For<IAccommodationZonesRepository>();
        _editionsRepo = Substitute.For<ICampEditionsRepository>();
        _sut = new AccommodationZonesService(_zonesRepo, _editionsRepo);
    }

    private static AccommodationZone MakeZone(Guid zoneId, Guid campEditionId) => new()
    {
        Id = zoneId,
        CampEditionId = campEditionId,
        Name = "Zona Test",
        AccommodationType = AccommodationType.Lodge,
        SortOrder = 0,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GetByIdAsync_WhenZoneExistsInEdition_ReturnsZoneResponse()
    {
        var zone = MakeZone(ZoneId, EditionId);
        _zonesRepo.GetByIdAsync(ZoneId, Arg.Any<CancellationToken>()).Returns(zone);

        var result = await _sut.GetByIdAsync(EditionId, ZoneId);

        result.Id.Should().Be(ZoneId);
        result.CampEditionId.Should().Be(EditionId);
        result.Name.Should().Be("Zona Test");
    }

    [Fact]
    public async Task GetByIdAsync_WhenZoneNotFound_ThrowsNotFoundException()
    {
        _zonesRepo.GetByIdAsync(ZoneId, Arg.Any<CancellationToken>()).Returns((AccommodationZone?)null);

        var act = () => _sut.GetByIdAsync(EditionId, ZoneId);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenZoneBelongsToDifferentEdition_ThrowsNotFoundException()
    {
        var otherEditionId = Guid.NewGuid();
        var zone = MakeZone(ZoneId, otherEditionId);
        _zonesRepo.GetByIdAsync(ZoneId, Arg.Any<CancellationToken>()).Returns(zone);

        var act = () => _sut.GetByIdAsync(EditionId, ZoneId);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
