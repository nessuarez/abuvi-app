using Abuvi.API.Features.Camps;
using FluentAssertions;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

/// <summary>
/// Unit tests for AccommodationCapacity value object and serialization helpers
/// </summary>
public class AccommodationCapacityTests
{
    #region CalculateTotalBedCapacity

    [Fact]
    public void CalculateTotalBedCapacity_WithAllTypes_ReturnsCorrectSum()
    {
        // Arrange
        var capacity = new AccommodationCapacity
        {
            PrivateRoomsWithBathroom = 5,
            PrivateRoomsSharedBathroom = 3,
            SharedRooms = new List<SharedRoomInfo>
            {
                new() { Quantity = 2, BedsPerRoom = 8 },
                new() { Quantity = 8, BedsPerRoom = 2 }
            }
        };

        // Act
        var total = capacity.CalculateTotalBedCapacity();

        // Assert: (5+3)*2 + (2*8 + 8*2) = 16 + 32 = 48
        total.Should().Be(48);
    }

    [Fact]
    public void CalculateTotalBedCapacity_WithNullValues_HandlesGracefully()
    {
        // Arrange
        var capacity = new AccommodationCapacity
        {
            PrivateRoomsWithBathroom = null,
            PrivateRoomsSharedBathroom = null,
            SharedRooms = null
        };

        // Act
        var total = capacity.CalculateTotalBedCapacity();

        // Assert
        total.Should().Be(0);
    }

    [Fact]
    public void CalculateTotalBedCapacity_WithEmptySharedRooms_ReturnsPrivateRoomsOnly()
    {
        // Arrange
        var capacity = new AccommodationCapacity
        {
            PrivateRoomsWithBathroom = 4,
            PrivateRoomsSharedBathroom = 2,
            SharedRooms = new List<SharedRoomInfo>()
        };

        // Act
        var total = capacity.CalculateTotalBedCapacity();

        // Assert: (4+2) * 2 = 12
        total.Should().Be(12);
    }

    [Fact]
    public void CalculateTotalBedCapacity_WithOnlySharedRooms_ReturnsSumOfSharedBeds()
    {
        // Arrange
        var capacity = new AccommodationCapacity
        {
            SharedRooms = new List<SharedRoomInfo>
            {
                new() { Quantity = 3, BedsPerRoom = 6 },
                new() { Quantity = 1, BedsPerRoom = 10 }
            }
        };

        // Act
        var total = capacity.CalculateTotalBedCapacity();

        // Assert: (3*6) + (1*10) = 28
        total.Should().Be(28);
    }

    [Fact]
    public void CalculateTotalBedCapacity_WithEmptyObject_ReturnsZero()
    {
        // Arrange
        var capacity = new AccommodationCapacity();

        // Act
        var total = capacity.CalculateTotalBedCapacity();

        // Assert
        total.Should().Be(0);
    }

    #endregion

    #region Camp JSON serialization helpers

    [Fact]
    public void Camp_SetAccommodationCapacity_SerializesToJson()
    {
        // Arrange
        var camp = new Camp { Id = Guid.NewGuid(), Name = "Test" };
        var capacity = new AccommodationCapacity
        {
            PrivateRoomsWithBathroom = 5,
            Bungalows = 10
        };

        // Act
        camp.SetAccommodationCapacity(capacity);

        // Assert
        camp.AccommodationCapacityJson.Should().NotBeNullOrWhiteSpace();
        camp.AccommodationCapacityJson.Should().Contain("privateRoomsWithBathroom");
        camp.AccommodationCapacityJson.Should().Contain("5");
    }

    [Fact]
    public void Camp_GetAccommodationCapacity_DeserializesFromJson()
    {
        // Arrange
        var camp = new Camp { Id = Guid.NewGuid(), Name = "Test" };
        var original = new AccommodationCapacity
        {
            PrivateRoomsWithBathroom = 5,
            PrivateRoomsSharedBathroom = 3,
            SharedRooms = new List<SharedRoomInfo>
            {
                new() { Quantity = 2, BedsPerRoom = 8, HasBathroom = false, Notes = "Literas" }
            }
        };
        camp.SetAccommodationCapacity(original);

        // Act
        var retrieved = camp.GetAccommodationCapacity();

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.PrivateRoomsWithBathroom.Should().Be(5);
        retrieved.PrivateRoomsSharedBathroom.Should().Be(3);
        retrieved.SharedRooms.Should().HaveCount(1);
        retrieved.SharedRooms![0].Quantity.Should().Be(2);
        retrieved.SharedRooms[0].BedsPerRoom.Should().Be(8);
        retrieved.SharedRooms[0].Notes.Should().Be("Literas");
    }

    [Fact]
    public void Camp_SetAccommodationCapacity_WithNull_SetsJsonToNull()
    {
        // Arrange
        var camp = new Camp { Id = Guid.NewGuid(), Name = "Test" };
        camp.SetAccommodationCapacity(new AccommodationCapacity { PrivateRoomsWithBathroom = 5 });

        // Act
        camp.SetAccommodationCapacity(null);

        // Assert
        camp.AccommodationCapacityJson.Should().BeNull();
    }

    [Fact]
    public void Camp_GetAccommodationCapacity_WhenJsonIsNull_ReturnsNull()
    {
        // Arrange
        var camp = new Camp { Id = Guid.NewGuid(), Name = "Test", AccommodationCapacityJson = null };

        // Act
        var result = camp.GetAccommodationCapacity();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Camp_SetAccommodationCapacity_NullValuesAreOmittedFromJson()
    {
        // Arrange
        var camp = new Camp { Id = Guid.NewGuid(), Name = "Test" };
        var capacity = new AccommodationCapacity { PrivateRoomsWithBathroom = 3 };

        // Act
        camp.SetAccommodationCapacity(capacity);

        // Assert: null fields should not appear in serialized JSON (WhenWritingNull)
        camp.AccommodationCapacityJson.Should().NotContain("privateRoomsSharedBathroom");
        camp.AccommodationCapacityJson.Should().NotContain("bungalows");
    }

    #endregion

    #region CampEdition JSON serialization helpers

    [Fact]
    public void CampEdition_SetAccommodationCapacity_SerializesAndDeserializesCorrectly()
    {
        // Arrange
        var edition = new CampEdition { Id = Guid.NewGuid() };
        var capacity = new AccommodationCapacity
        {
            PrivateRoomsWithBathroom = 10,
            MotorhomeSpots = 5
        };

        // Act
        edition.SetAccommodationCapacity(capacity);
        var retrieved = edition.GetAccommodationCapacity();

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.PrivateRoomsWithBathroom.Should().Be(10);
        retrieved.MotorhomeSpots.Should().Be(5);
    }

    [Fact]
    public void CampEdition_GetAccommodationCapacity_WhenJsonIsNull_ReturnsNull()
    {
        // Arrange
        var edition = new CampEdition { Id = Guid.NewGuid(), AccommodationCapacityJson = null };

        // Act
        var result = edition.GetAccommodationCapacity();

        // Assert
        result.Should().BeNull();
    }

    #endregion
}
