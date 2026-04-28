using Abuvi.API.Features.Camps;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

public class CampEditionAccommodationsServiceTests
{
    private readonly ICampEditionAccommodationsRepository _repository;
    private readonly ICampEditionsRepository _editionsRepository;
    private readonly CampEditionAccommodationsService _sut;

    public CampEditionAccommodationsServiceTests()
    {
        _repository = Substitute.For<ICampEditionAccommodationsRepository>();
        _editionsRepository = Substitute.For<ICampEditionsRepository>();
        _sut = new CampEditionAccommodationsService(_repository, _editionsRepository);
    }

    private static CampEdition MakeEdition(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        CampId = Guid.NewGuid(),
        Year = 2026,
        Status = CampEditionStatus.Draft,
        StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
        PricePerAdult = 100m,
        PricePerChild = 80m,
        PricePerBaby = 40m,
        MaxCapacity = 100,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static CampEditionAccommodation MakeAccommodation(
        Guid? id = null,
        AccommodationType type = AccommodationType.Lodge,
        bool countByFamily = false) => new()
    {
        Id = id ?? Guid.NewGuid(),
        CampEditionId = Guid.NewGuid(),
        Name = "Test Unit",
        AccommodationType = type,
        CountByFamily = countByFamily,
        IsActive = true,
        SortOrder = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        FeatureAssignments = []
    };

    private void SetupEdition(Guid editionId)
    {
        var edition = MakeEdition(editionId);
        _editionsRepository.GetByIdAsync(editionId, Arg.Any<CancellationToken>()).Returns(edition);
        _repository.GetPreferenceCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(0);
        _repository.GetFirstChoiceCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(0);
    }

    [Fact]
    public async Task CreateAsync_WithTentType_DefaultsCountByFamilyTrue()
    {
        var editionId = Guid.NewGuid();
        SetupEdition(editionId);

        var request = new CreateCampEditionAccommodationRequest(
            "Parcela T-01", AccommodationType.Tent, null, 1);

        var result = await _sut.CreateAsync(editionId, request, CancellationToken.None);

        result.CountByFamily.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WithCaravanType_DefaultsCountByFamilyTrue()
    {
        var editionId = Guid.NewGuid();
        SetupEdition(editionId);

        var request = new CreateCampEditionAccommodationRequest(
            "Parcela C-01", AccommodationType.Caravan, null, null);

        var result = await _sut.CreateAsync(editionId, request, CancellationToken.None);

        result.CountByFamily.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WithLodgeType_DefaultsCountByFamilyFalse()
    {
        var editionId = Guid.NewGuid();
        SetupEdition(editionId);

        var request = new CreateCampEditionAccommodationRequest(
            "Habitación 101", AccommodationType.Lodge, null, 4);

        var result = await _sut.CreateAsync(editionId, request, CancellationToken.None);

        result.CountByFamily.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_WithExplicitFalse_OnTentType_OverridesDefault()
    {
        var editionId = Guid.NewGuid();
        SetupEdition(editionId);

        var request = new CreateCampEditionAccommodationRequest(
            "Parcela especial", AccommodationType.Tent, null, 6, CountByFamily: false);

        var result = await _sut.CreateAsync(editionId, request, CancellationToken.None);

        result.CountByFamily.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_WithExplicitTrue_OnLodgeType_UsesProvidedValue()
    {
        var editionId = Guid.NewGuid();
        SetupEdition(editionId);

        var request = new CreateCampEditionAccommodationRequest(
            "Suite familiar", AccommodationType.Lodge, null, null, CountByFamily: true);

        var result = await _sut.CreateAsync(editionId, request, CancellationToken.None);

        result.CountByFamily.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesCountByFamily()
    {
        var id = Guid.NewGuid();
        var accommodation = MakeAccommodation(id, AccommodationType.Lodge, countByFamily: false);
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(accommodation);
        _repository.GetPreferenceCountAsync(id, Arg.Any<CancellationToken>()).Returns(0);
        _repository.GetFirstChoiceCountAsync(id, Arg.Any<CancellationToken>()).Returns(0);

        var request = new UpdateCampEditionAccommodationRequest(
            "Habitación 101", AccommodationType.Lodge, null, 4,
            CountByFamily: true, IsActive: true, ZoneId: null, SortOrder: 0);

        var result = await _sut.UpdateAsync(id, request, CancellationToken.None);

        result.CountByFamily.Should().BeTrue();
    }
}
