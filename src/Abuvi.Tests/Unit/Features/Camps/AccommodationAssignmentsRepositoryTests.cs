using Abuvi.API.Data;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.MediaItems;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

public class AccommodationAssignmentsRepositoryTests : IDisposable
{
    private readonly AbuviDbContext _context;
    private readonly AccommodationAssignmentsRepository _sut;

    private static readonly Guid EditionId = Guid.NewGuid();
    private static readonly Guid ProposalId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public AccommodationAssignmentsRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AbuviDbContext>()
            .UseInMemoryDatabase(databaseName: $"AssignmentsRepoTest_{Guid.NewGuid()}")
            .Options;

        _context = new AbuviDbContext(options);
        _sut = new AccommodationAssignmentsRepository(_context);
    }

    public void Dispose() => _context.Dispose();

    private static CampEditionAccommodation MakeAccommodation(
        Guid campEditionId,
        bool isActive = true,
        bool isAssignable = true,
        Guid? zoneId = null) => new()
    {
        Id = Guid.NewGuid(),
        CampEditionId = campEditionId,
        Name = "Habitación Test",
        AccommodationType = AccommodationType.Lodge,
        Quantity = 1,
        IsActive = isActive,
        IsAssignable = isAssignable,
        SortOrder = 0,
        ZoneId = zoneId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static AccommodationZone MakeZone(Guid campEditionId) => new()
    {
        Id = Guid.NewGuid(),
        CampEditionId = campEditionId,
        Name = "Zona Test",
        AccommodationType = AccommodationType.Lodge,
        SortOrder = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static MediaItem MakePrimaryZoneMedia(Guid zoneId) => new()
    {
        Id = Guid.NewGuid(),
        UploadedByUserId = UserId,
        ZoneId = zoneId,
        FileUrl = "https://example.com/zone-photo.jpg",
        ThumbnailUrl = "https://example.com/zone-thumb.jpg",
        Type = MediaItemType.Photo,
        Title = "Foto zona",
        IsPrimary = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GetAssignmentStateAsync_WhenAccommodationIsNotAssignable_ExcludesItFromResponse()
    {
        var assignable = MakeAccommodation(EditionId, isAssignable: true);
        var nonAssignable = MakeAccommodation(EditionId, isAssignable: false);

        _context.CampEditionAccommodations.AddRange(assignable, nonAssignable);
        await _context.SaveChangesAsync();

        var result = await _sut.GetAssignmentStateAsync(EditionId, ProposalId);

        result.Accommodations.Should().ContainSingle(a => a.Id == assignable.Id);
        result.Accommodations.Should().NotContain(a => a.Id == nonAssignable.Id);
    }

    [Fact]
    public async Task GetAssignmentStateAsync_WhenZoneHasPrimaryMedia_IncludesZoneThumbnailInResponse()
    {
        var zone = MakeZone(EditionId);
        _context.AccommodationZones.Add(zone);

        var media = MakePrimaryZoneMedia(zone.Id);
        _context.MediaItems.Add(media);

        var accommodation = MakeAccommodation(EditionId, zoneId: zone.Id);
        _context.CampEditionAccommodations.Add(accommodation);

        await _context.SaveChangesAsync();

        var result = await _sut.GetAssignmentStateAsync(EditionId, ProposalId);

        result.Accommodations.Should().ContainSingle();
        result.Accommodations[0].ZonePrimaryThumbnailUrl.Should().Be(media.ThumbnailUrl);
        result.Accommodations[0].ZonePrimaryFileUrl.Should().Be(media.FileUrl);
    }

    [Fact]
    public async Task GetAssignmentStateAsync_WhenZoneHasNoMedia_ReturnsNullZoneThumbnailUrl()
    {
        var zone = MakeZone(EditionId);
        _context.AccommodationZones.Add(zone);

        var accommodation = MakeAccommodation(EditionId, zoneId: zone.Id);
        _context.CampEditionAccommodations.Add(accommodation);

        await _context.SaveChangesAsync();

        var result = await _sut.GetAssignmentStateAsync(EditionId, ProposalId);

        result.Accommodations.Should().ContainSingle();
        result.Accommodations[0].ZonePrimaryThumbnailUrl.Should().BeNull();
        result.Accommodations[0].ZonePrimaryFileUrl.Should().BeNull();
    }

    [Fact]
    public async Task GetAssignmentStateAsync_AccommodationTypeLookupIncludesNonAssignable()
    {
        var assignable = MakeAccommodation(EditionId, isAssignable: true);
        var nonAssignable = MakeAccommodation(EditionId, isAssignable: false);

        _context.CampEditionAccommodations.AddRange(assignable, nonAssignable);
        await _context.SaveChangesAsync();

        var result = await _sut.GetAssignmentStateAsync(EditionId, ProposalId);

        result.Accommodations.Should().ContainSingle(a => a.Id == assignable.Id);
        result.AccommodationTypeLookup.Should().HaveCount(2);
        result.AccommodationTypeLookup.Should().Contain(x => x.Id == assignable.Id);
        result.AccommodationTypeLookup.Should().Contain(x => x.Id == nonAssignable.Id);
    }

    [Fact]
    public async Task GetAssignmentStateAsync_AllFeaturesIncludesFeatureNamesAndIcons()
    {
        var feature = new AccommodationFeature
        {
            Id = Guid.NewGuid(),
            Name = "Accesible",
            Icon = "pi pi-wheelchair",
            Description = null,
            ApplicabilityLevel = FeatureApplicabilityLevel.Accommodation,
            IsActive = true,
            SortOrder = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.AccommodationFeatures.Add(feature);

        var accommodation = MakeAccommodation(EditionId);
        _context.CampEditionAccommodations.Add(accommodation);

        _context.AccommodationFeatureAssignments.Add(new AccommodationFeatureAssignment
        {
            AccommodationId = accommodation.Id,
            FeatureId = feature.Id
        });

        await _context.SaveChangesAsync();

        var result = await _sut.GetAssignmentStateAsync(EditionId, ProposalId);

        result.AllFeatures.Should().ContainSingle();
        result.AllFeatures[0].Id.Should().Be(feature.Id);
        result.AllFeatures[0].Name.Should().Be("Accesible");
        result.AllFeatures[0].Icon.Should().Be("pi pi-wheelchair");
        result.Accommodations[0].AvailableFeatures.Should().Contain(feature.Id);
    }

    [Fact]
    public async Task GetAssignmentStateAsync_AllFeaturesIsEmpty_WhenNoFeaturesAssigned()
    {
        var accommodation = MakeAccommodation(EditionId);
        _context.CampEditionAccommodations.Add(accommodation);
        await _context.SaveChangesAsync();

        var result = await _sut.GetAssignmentStateAsync(EditionId, ProposalId);

        result.AllFeatures.Should().BeEmpty();
        result.Accommodations[0].AvailableFeatures.Should().BeEmpty();
    }
}
