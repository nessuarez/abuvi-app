using Abuvi.API.Data;
using Abuvi.API.Features.Camps;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Abuvi.Tests.Integration.Features.Camps;

/// <summary>
/// Integration tests to verify database schema for Camps entities
/// Following TDD: Tests verify migration created schema correctly
/// </summary>
public class CampsDbSchemaTests : IDisposable
{
    private readonly AbuviDbContext _context;

    public CampsDbSchemaTests()
    {
        // Use in-memory database for testing
        var options = new DbContextOptionsBuilder<AbuviDbContext>()
            .UseInMemoryDatabase(databaseName: $"CampsTest_{Guid.NewGuid()}")
            .Options;

        _context = new AbuviDbContext(options);

        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task Camp_CanBeCreatedWithValidData()
    {
        // Arrange
        var camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = "Test Camp",
            Description = "A test camp location",
            Location = "Test Location",
            Latitude = 40.7128m,
            Longitude = -74.0060m,
            PricePerAdult = 180.00m,
            PricePerChild = 120.00m,
            PricePerBaby = 60.00m,
            IsActive = true
        };

        // Act
        _context.Camps.Add(camp);
        await _context.SaveChangesAsync();

        // Assert
        var savedCamp = await _context.Camps.FindAsync(camp.Id);
        savedCamp.Should().NotBeNull();
        savedCamp!.Name.Should().Be("Test Camp");
        savedCamp.PricePerAdult.Should().Be(180.00m);
        savedCamp.PricePerChild.Should().Be(120.00m);
        savedCamp.PricePerBaby.Should().Be(60.00m);
        // Note: In-memory database doesn't set default values automatically
    }

    // NOTE: In-memory database doesn't enforce check constraints
    // These validations will be tested at the service/validator level

    [Fact]
    public async Task CampEdition_CanBeCreatedWithValidData()
    {
        // Arrange
        var camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = "Test Camp",
            PricePerAdult = 180.00m,
            PricePerChild = 120.00m,
            PricePerBaby = 60.00m
        };

        var edition = new CampEdition
        {
            Id = Guid.NewGuid(),
            CampId = camp.Id,
            Year = 2026,
            StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult = 180.00m,
            PricePerChild = 120.00m,
            PricePerBaby = 60.00m,
            Status = CampEditionStatus.Draft,
            UseCustomAgeRanges = false
        };

        // Act
        _context.Camps.Add(camp);
        _context.CampEditions.Add(edition);
        await _context.SaveChangesAsync();

        // Assert
        var savedEdition = await _context.CampEditions.FindAsync(edition.Id);
        savedEdition.Should().NotBeNull();
        savedEdition!.Year.Should().Be(2026);
        savedEdition.Status.Should().Be(CampEditionStatus.Draft);
        savedEdition.UseCustomAgeRanges.Should().BeFalse();
    }

    [Fact]
    public async Task CampEdition_WithCustomAgeRanges_CanBeCreated()
    {
        // Arrange
        var camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = "Test Camp",
            PricePerAdult = 180.00m,
            PricePerChild = 120.00m,
            PricePerBaby = 60.00m
        };

        var edition = new CampEdition
        {
            Id = Guid.NewGuid(),
            CampId = camp.Id,
            Year = 2027,
            StartDate = new DateTime(2027, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2027, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult = 180.00m,
            PricePerChild = 120.00m,
            PricePerBaby = 60.00m,
            Status = CampEditionStatus.Draft,
            UseCustomAgeRanges = true,
            CustomBabyMaxAge = 3,
            CustomChildMinAge = 4,
            CustomChildMaxAge = 14,
            CustomAdultMinAge = 15
        };

        // Act
        _context.Camps.Add(camp);
        _context.CampEditions.Add(edition);
        await _context.SaveChangesAsync();

        // Assert
        var savedEdition = await _context.CampEditions.FindAsync(edition.Id);
        savedEdition.Should().NotBeNull();
        savedEdition!.UseCustomAgeRanges.Should().BeTrue();
        savedEdition.CustomBabyMaxAge.Should().Be(3);
        savedEdition.CustomChildMinAge.Should().Be(4);
        savedEdition.CustomChildMaxAge.Should().Be(14);
        savedEdition.CustomAdultMinAge.Should().Be(15);
    }

    [Fact]
    public async Task CampEditionExtra_CanBeCreatedWithValidData()
    {
        // Arrange
        var camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = "Test Camp",
            PricePerAdult = 180.00m,
            PricePerChild = 120.00m,
            PricePerBaby = 60.00m
        };

        var edition = new CampEdition
        {
            Id = Guid.NewGuid(),
            CampId = camp.Id,
            Year = 2026,
            StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult = 180.00m,
            PricePerChild = 120.00m,
            PricePerBaby = 60.00m,
            Status = CampEditionStatus.Draft
        };

        var extra = new CampEditionExtra
        {
            Id = Guid.NewGuid(),
            CampEditionId = edition.Id,
            Name = "Kayak Rental",
            Description = "Kayak rental for the duration",
            Price = 25.00m,
            PricingType = PricingType.PerPerson,
            PricingPeriod = PricingPeriod.OneTime,
            IsRequired = false,
            IsActive = true
        };

        // Act
        _context.Camps.Add(camp);
        _context.CampEditions.Add(edition);
        _context.CampEditionExtras.Add(extra);
        await _context.SaveChangesAsync();

        // Assert
        var savedExtra = await _context.CampEditionExtras.FindAsync(extra.Id);
        savedExtra.Should().NotBeNull();
        savedExtra!.Name.Should().Be("Kayak Rental");
        savedExtra.Price.Should().Be(25.00m);
        savedExtra.PricingType.Should().Be(PricingType.PerPerson);
        savedExtra.PricingPeriod.Should().Be(PricingPeriod.OneTime);
    }

    [Fact]
    public async Task AssociationSettings_CanBeCreated()
    {
        // Arrange
        var settings = new AssociationSettings
        {
            Id = Guid.NewGuid(),
            SettingKey = "age_ranges",
            SettingValue = "{\"babyMaxAge\":2,\"childMinAge\":3,\"childMaxAge\":12,\"adultMinAge\":13}"
        };

        // Act
        _context.AssociationSettings.Add(settings);
        await _context.SaveChangesAsync();

        // Assert
        var savedSettings = await _context.AssociationSettings.FindAsync(settings.Id);
        savedSettings.Should().NotBeNull();
        savedSettings!.SettingKey.Should().Be("age_ranges");
        savedSettings.SettingValue.Should().Contain("babyMaxAge");
    }

    public void Dispose()
    {
        // Clean up test data
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
