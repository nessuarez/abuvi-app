using Abuvi.API.Data;
using Abuvi.API.Features.Camps;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Abuvi.Tests.Integration.Features.Camps;

/// <summary>
/// Integration tests for accommodation capacity JSON storage and camp photos
/// </summary>
public class CampAccommodationTests : IDisposable
{
    private readonly AbuviDbContext _context;

    public CampAccommodationTests()
    {
        var options = new DbContextOptionsBuilder<AbuviDbContext>()
            .UseInMemoryDatabase($"CampAccommodationTest_{Guid.NewGuid()}")
            .Options;
        _context = new AbuviDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose() => _context.Dispose();

    private static Camp CreateCamp(string name = "Test Camp") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        PricePerAdult = 180m,
        PricePerChild = 120m,
        PricePerBaby = 60m,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    #region Accommodation Capacity JSON Storage

    [Fact]
    public async Task Camp_WithAccommodationJson_CanBeSavedAndRetrieved()
    {
        // Arrange
        var capacity = new AccommodationCapacity
        {
            PrivateRoomsWithBathroom = 5,
            PrivateRoomsSharedBathroom = 3,
            SharedRooms = new List<SharedRoomInfo>
            {
                new() { Quantity = 2, BedsPerRoom = 8, HasBathroom = false, Notes = "Literas" }
            },
            Bungalows = 10,
            MotorhomeSpots = 5
        };

        var camp = CreateCamp();
        camp.SetAccommodationCapacity(capacity);

        // Act
        await _context.Camps.AddAsync(camp);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        var retrieved = await _context.Camps.FindAsync(camp.Id);
        var retrievedCapacity = retrieved!.GetAccommodationCapacity();

        // Assert
        retrievedCapacity.Should().NotBeNull();
        retrievedCapacity!.PrivateRoomsWithBathroom.Should().Be(5);
        retrievedCapacity.PrivateRoomsSharedBathroom.Should().Be(3);
        retrievedCapacity.SharedRooms.Should().HaveCount(1);
        retrievedCapacity.SharedRooms![0].Quantity.Should().Be(2);
        retrievedCapacity.SharedRooms[0].BedsPerRoom.Should().Be(8);
        retrievedCapacity.SharedRooms[0].Notes.Should().Be("Literas");
        retrievedCapacity.Bungalows.Should().Be(10);
        retrievedCapacity.MotorhomeSpots.Should().Be(5);
    }

    [Fact]
    public async Task Camp_WithNullAccommodation_StoresNullJson()
    {
        // Arrange
        var camp = CreateCamp();

        // Act
        await _context.Camps.AddAsync(camp);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        var retrieved = await _context.Camps.FindAsync(camp.Id);

        // Assert
        retrieved!.AccommodationCapacityJson.Should().BeNull();
        retrieved.GetAccommodationCapacity().Should().BeNull();
    }

    [Fact]
    public async Task CampEdition_WithAccommodationJson_CanBeSavedAndRetrieved()
    {
        // Arrange
        var camp = CreateCamp();
        await _context.Camps.AddAsync(camp);
        await _context.SaveChangesAsync();

        var capacity = new AccommodationCapacity
        {
            PrivateRoomsWithBathroom = 8,
            CampOwnedTents = 20,
            MemberTentAreaSquareMeters = 300
        };

        var edition = new CampEdition
        {
            Id = Guid.NewGuid(),
            CampId = camp.Id,
            Year = 2026,
            StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            Status = CampEditionStatus.Proposed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        edition.SetAccommodationCapacity(capacity);

        // Act
        await _context.CampEditions.AddAsync(edition);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        var retrieved = await _context.CampEditions.FindAsync(edition.Id);
        var retrievedCapacity = retrieved!.GetAccommodationCapacity();

        // Assert
        retrievedCapacity.Should().NotBeNull();
        retrievedCapacity!.PrivateRoomsWithBathroom.Should().Be(8);
        retrievedCapacity.CampOwnedTents.Should().Be(20);
        retrievedCapacity.MemberTentAreaSquareMeters.Should().Be(300);
    }

    [Fact]
    public async Task AccommodationCapacity_TotalBedCalculation_IsCorrectAfterRoundTrip()
    {
        // Arrange
        var capacity = new AccommodationCapacity
        {
            PrivateRoomsWithBathroom = 5,     // 10 beds
            PrivateRoomsSharedBathroom = 3,    // 6 beds
            SharedRooms = new List<SharedRoomInfo>
            {
                new() { Quantity = 2, BedsPerRoom = 8 },   // 16 beds
                new() { Quantity = 8, BedsPerRoom = 2 }    // 16 beds
            }
        };

        var camp = CreateCamp();
        camp.SetAccommodationCapacity(capacity);

        await _context.Camps.AddAsync(camp);
        await _context.SaveChangesAsync();

        // Act
        _context.ChangeTracker.Clear();
        var retrieved = await _context.Camps.FindAsync(camp.Id);
        var retrievedCapacity = retrieved!.GetAccommodationCapacity();
        var total = retrievedCapacity!.CalculateTotalBedCapacity();

        // Assert: (5+3)*2 + (2*8 + 8*2) = 16 + 32 = 48
        total.Should().Be(48);
    }

    #endregion

    #region Camp Photos Storage

    [Fact]
    public async Task CampPhoto_CanBeCreatedAndLinkedToCamp()
    {
        // Arrange
        var camp = CreateCamp();
        await _context.Camps.AddAsync(camp);
        await _context.SaveChangesAsync();

        var photo = new CampPhoto
        {
            Id = Guid.NewGuid(),
            CampId = camp.Id,
            PhotoUrl = "https://example.com/photo.jpg",
            Description = "Camp entrance",
            DisplayOrder = 1,
            IsPrimary = true,
            IsOriginal = false,
            Width = 0,
            Height = 0,
            AttributionName = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        await _context.CampPhotos.AddAsync(photo);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        var retrieved = await _context.CampPhotos.FindAsync(photo.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.CampId.Should().Be(camp.Id);
        retrieved.PhotoUrl.Should().Be("https://example.com/photo.jpg");
        retrieved.Description.Should().Be("Camp entrance");
        retrieved.IsPrimary.Should().BeTrue();
        retrieved.DisplayOrder.Should().Be(1);
    }

    [Fact]
    public async Task Camp_CanHaveMultiplePhotos_WithCorrectOrder()
    {
        // Arrange
        var camp = CreateCamp();
        await _context.Camps.AddAsync(camp);
        await _context.SaveChangesAsync();

        var photos = new List<CampPhoto>
        {
            new() { Id = Guid.NewGuid(), CampId = camp.Id, PhotoUrl = "https://example.com/3.jpg", DisplayOrder = 3, Width = 0, Height = 0, AttributionName = string.Empty, IsOriginal = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), CampId = camp.Id, PhotoUrl = "https://example.com/1.jpg", DisplayOrder = 1, Width = 0, Height = 0, AttributionName = string.Empty, IsOriginal = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), CampId = camp.Id, PhotoUrl = "https://example.com/2.jpg", DisplayOrder = 2, Width = 0, Height = 0, AttributionName = string.Empty, IsOriginal = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        // Act
        await _context.CampPhotos.AddRangeAsync(photos);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        var retrieved = await _context.CampPhotos
            .Where(p => p.CampId == camp.Id)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();

        // Assert
        retrieved.Should().HaveCount(3);
        retrieved[0].DisplayOrder.Should().Be(1);
        retrieved[1].DisplayOrder.Should().Be(2);
        retrieved[2].DisplayOrder.Should().Be(3);
    }

    [Fact]
    public async Task CampPhoto_CascadeDeleteWithCamp()
    {
        // Arrange
        var camp = CreateCamp();
        await _context.Camps.AddAsync(camp);
        await _context.SaveChangesAsync();

        var photo = new CampPhoto
        {
            Id = Guid.NewGuid(),
            CampId = camp.Id,
            PhotoUrl = "https://example.com/photo.jpg",
            DisplayOrder = 1,
            Width = 0,
            Height = 0,
            AttributionName = string.Empty,
            IsOriginal = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context.CampPhotos.AddAsync(photo);
        await _context.SaveChangesAsync();

        // Act
        _context.Camps.Remove(camp);
        await _context.SaveChangesAsync();

        // Assert: photo cascade deleted with camp
        var deletedPhoto = await _context.CampPhotos.FindAsync(photo.Id);
        deletedPhoto.Should().BeNull();
    }

    [Fact]
    public async Task CampPhoto_Description_CanBeStoredAndRetrieved()
    {
        // Arrange
        var camp = CreateCamp();
        await _context.Camps.AddAsync(camp);
        await _context.SaveChangesAsync();

        var photo = new CampPhoto
        {
            Id = Guid.NewGuid(),
            CampId = camp.Id,
            PhotoUrl = "https://example.com/photo.jpg",
            Description = "Beautiful sunset at the campsite",
            DisplayOrder = 1,
            IsPrimary = false,
            IsOriginal = false,
            Width = 0,
            Height = 0,
            AttributionName = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        await _context.CampPhotos.AddAsync(photo);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        var retrieved = await _context.CampPhotos.FindAsync(photo.Id);

        // Assert
        retrieved!.Description.Should().Be("Beautiful sunset at the campsite");
    }

    #endregion
}
