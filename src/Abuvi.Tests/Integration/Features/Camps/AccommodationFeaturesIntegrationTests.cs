using Abuvi.API.Data;
using Abuvi.API.Features.Camps;
using Abuvi.Tests.Helpers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Abuvi.Tests.Integration.Features.Camps;

public class AccommodationFeaturesIntegrationTests : IDisposable
{
    private readonly AbuviDbContext _context;

    public AccommodationFeaturesIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<AbuviDbContext>()
            .UseInMemoryDatabase($"AccommodationFeaturesTest_{Guid.NewGuid()}")
            .Options;
        _context = new AbuviDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose() => _context.Dispose();

    private (Camp camp, CampEdition edition, CampEditionAccommodation accommodation, AccommodationZone zone) SeedBasicData()
    {
        var camp = new Camp
        {
            Id = Guid.NewGuid(), Name = "Test Camp",
            PricePerAdult = 100m, PricePerChild = 80m, PricePerBaby = 40m,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var edition = new CampEdition
        {
            Id = Guid.NewGuid(), CampId = camp.Id,
            Year = 2026, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(7),
            PricePerAdult = 100m, PricePerChild = 80m, PricePerBaby = 40m,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var accommodation = new CampEditionAccommodation
        {
            Id = Guid.NewGuid(), CampEditionId = edition.Id,
            Name = "Room A", AccommodationType = AccommodationType.Lodge,
            IsActive = true, SortOrder = 0,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var zone = new AccommodationZone
        {
            Id = Guid.NewGuid(), CampEditionId = edition.Id,
            AccommodationType = AccommodationType.Lodge, Name = "Zone 1",
            IsActive = true, SortOrder = 0,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        _context.Camps.Add(camp);
        _context.CampEditions.Add(edition);
        _context.CampEditionAccommodations.Add(accommodation);
        _context.AccommodationZones.Add(zone);
        _context.SaveChanges();

        return (camp, edition, accommodation, zone);
    }

    [Fact]
    public async Task AccommodationFeature_CanBeSavedAndRetrievedFromDatabase()
    {
        var feature = new AccommodationFeatureBuilder().WithName("Wifi").Build();

        _context.AccommodationFeatures.Add(feature);
        await _context.SaveChangesAsync();

        var saved = await _context.AccommodationFeatures.FindAsync(feature.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Wifi");
        saved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task AccommodationFeatureAssignment_CascadeDeletesWhenAccommodationDeleted()
    {
        var (_, _, accommodation, _) = SeedBasicData();
        var feature = new AccommodationFeatureBuilder().Build();
        _context.AccommodationFeatures.Add(feature);

        var assignment = new AccommodationFeatureAssignment
        {
            AccommodationId = accommodation.Id,
            FeatureId = feature.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.AccommodationFeatureAssignments.Add(assignment);
        await _context.SaveChangesAsync();

        _context.CampEditionAccommodations.Remove(accommodation);
        await _context.SaveChangesAsync();

        var remainingAssignments = await _context.AccommodationFeatureAssignments
            .Where(a => a.AccommodationId == accommodation.Id)
            .ToListAsync();
        remainingAssignments.Should().BeEmpty();
    }

    [Fact]
    public async Task ZoneFeatureAssignment_CascadeDeletesWhenZoneDeleted()
    {
        var (_, _, _, zone) = SeedBasicData();
        var feature = new AccommodationFeatureBuilder().Build();
        _context.AccommodationFeatures.Add(feature);

        var assignment = new ZoneFeatureAssignment
        {
            ZoneId = zone.Id,
            FeatureId = feature.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.ZoneFeatureAssignments.Add(assignment);
        await _context.SaveChangesAsync();

        _context.AccommodationZones.Remove(zone);
        await _context.SaveChangesAsync();

        var remainingAssignments = await _context.ZoneFeatureAssignments
            .Where(a => a.ZoneId == zone.Id)
            .ToListAsync();
        remainingAssignments.Should().BeEmpty();
    }

    [Fact]
    public async Task AccommodationFeatureAssignment_FeatureCanBeRetrievedThroughNavigation()
    {
        var (_, _, accommodation, _) = SeedBasicData();
        var feature = new AccommodationFeatureBuilder().WithName("Pool").Build();
        _context.AccommodationFeatures.Add(feature);
        _context.AccommodationFeatureAssignments.Add(new AccommodationFeatureAssignment
        {
            AccommodationId = accommodation.Id, FeatureId = feature.Id, CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var features = await _context.AccommodationFeatureAssignments
            .Where(a => a.AccommodationId == accommodation.Id)
            .Select(a => a.Feature)
            .ToListAsync();

        features.Should().HaveCount(1);
        features[0].Name.Should().Be("Pool");
    }

    [Fact]
    public async Task ZoneFeatureAssignment_FeatureCanBeRetrievedThroughNavigation()
    {
        var (_, _, _, zone) = SeedBasicData();
        var feature = new AccommodationFeatureBuilder().WithName("Garden").Build();
        _context.AccommodationFeatures.Add(feature);
        _context.ZoneFeatureAssignments.Add(new ZoneFeatureAssignment
        {
            ZoneId = zone.Id, FeatureId = feature.Id, CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var features = await _context.ZoneFeatureAssignments
            .Where(a => a.ZoneId == zone.Id)
            .Select(a => a.Feature)
            .ToListAsync();

        features.Should().HaveCount(1);
        features[0].Name.Should().Be("Garden");
    }

    [Fact]
    public async Task AccommodationFeature_MultipleAssignmentsCanExistForSameFeature()
    {
        var (_, _, accommodation, _) = SeedBasicData();
        var zone = (await _context.AccommodationZones.ToListAsync())[0];
        var feature = new AccommodationFeatureBuilder().Build();
        _context.AccommodationFeatures.Add(feature);

        _context.AccommodationFeatureAssignments.Add(new AccommodationFeatureAssignment
        {
            AccommodationId = accommodation.Id, FeatureId = feature.Id, CreatedAt = DateTime.UtcNow
        });
        _context.ZoneFeatureAssignments.Add(new ZoneFeatureAssignment
        {
            ZoneId = zone.Id, FeatureId = feature.Id, CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var accAssignments = await _context.AccommodationFeatureAssignments.CountAsync(a => a.FeatureId == feature.Id);
        var zoneAssignments = await _context.ZoneFeatureAssignments.CountAsync(a => a.FeatureId == feature.Id);

        accAssignments.Should().Be(1);
        zoneAssignments.Should().Be(1);
    }
}
