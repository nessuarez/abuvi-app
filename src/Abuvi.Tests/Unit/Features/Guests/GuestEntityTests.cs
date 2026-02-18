using Abuvi.API.Features.Guests;
using FluentAssertions;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Guests;

public class GuestEntityTests
{
    [Fact]
    public void Guest_DefaultProperties_HaveExpectedValues()
    {
        // Act
        var guest = new Guest();

        // Assert
        guest.Id.Should().Be(Guid.Empty);
        guest.FirstName.Should().BeEmpty();
        guest.LastName.Should().BeEmpty();
        guest.IsActive.Should().BeFalse();
        guest.DocumentNumber.Should().BeNull();
        guest.Email.Should().BeNull();
        guest.Phone.Should().BeNull();
        guest.MedicalNotes.Should().BeNull();
        guest.Allergies.Should().BeNull();
    }

    [Fact]
    public void Guest_ToResponse_MapsCorrectly()
    {
        // Arrange
        var guest = new Guest
        {
            Id = Guid.NewGuid(),
            FamilyUnitId = Guid.NewGuid(),
            FirstName = "Jane",
            LastName = "Doe",
            DateOfBirth = new DateOnly(1995, 5, 15),
            DocumentNumber = "ABC123",
            Email = "jane@example.com",
            Phone = "+34612345678",
            MedicalNotes = "Some notes",
            Allergies = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var response = guest.ToResponse();

        // Assert
        response.Id.Should().Be(guest.Id);
        response.FamilyUnitId.Should().Be(guest.FamilyUnitId);
        response.FirstName.Should().Be(guest.FirstName);
        response.LastName.Should().Be(guest.LastName);
        response.DateOfBirth.Should().Be(guest.DateOfBirth);
        response.DocumentNumber.Should().Be(guest.DocumentNumber);
        response.Email.Should().Be(guest.Email);
        response.Phone.Should().Be(guest.Phone);
        response.HasMedicalNotes.Should().BeTrue();
        response.HasAllergies.Should().BeFalse();
        response.IsActive.Should().BeTrue();
    }

    [Fact]
    public void GuestResponse_HasMedicalNotes_IsTrueWhenNotNull()
    {
        // Arrange
        var guest = new Guest { MedicalNotes = "Diabetes", Allergies = null };

        // Act
        var response = guest.ToResponse();

        // Assert
        response.HasMedicalNotes.Should().BeTrue();
        response.HasAllergies.Should().BeFalse();
    }

    [Fact]
    public void GuestResponse_HasAllergies_IsTrueWhenNotNull()
    {
        // Arrange
        var guest = new Guest { MedicalNotes = null, Allergies = "Peanuts" };

        // Act
        var response = guest.ToResponse();

        // Assert
        response.HasMedicalNotes.Should().BeFalse();
        response.HasAllergies.Should().BeTrue();
    }
}
