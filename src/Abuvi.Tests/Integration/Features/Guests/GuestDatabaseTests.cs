using Abuvi.API.Data;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Guests;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Abuvi.Tests.Integration.Features.Guests;

public class GuestDatabaseTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GuestDatabaseTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Guest_CanBeCreatedWithRequiredFields()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();
        var (user, familyUnit) = await SeedFamilyUnitAsync(dbContext);

        var guest = new Guest
        {
            Id = Guid.NewGuid(),
            FamilyUnitId = familyUnit.Id,
            FirstName = "Jane",
            LastName = "Doe",
            DateOfBirth = new DateOnly(1995, 5, 15),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        dbContext.Guests.Add(guest);
        await dbContext.SaveChangesAsync();

        // Assert
        var saved = await dbContext.Guests.FindAsync(guest.Id);
        saved.Should().NotBeNull();
        saved!.FirstName.Should().Be("Jane");
        saved.FamilyUnitId.Should().Be(familyUnit.Id);
    }

    [Fact]
    public async Task Guest_NavigationToFamilyUnit_Works()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();
        var (user, familyUnit) = await SeedFamilyUnitAsync(dbContext);

        var guest = new Guest
        {
            Id = Guid.NewGuid(),
            FamilyUnitId = familyUnit.Id,
            FirstName = "Jane",
            LastName = "Doe",
            DateOfBirth = new DateOnly(1995, 5, 15),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Guests.Add(guest);
        await dbContext.SaveChangesAsync();

        // Act
        var savedGuest = await dbContext.Guests.FindAsync(guest.Id);

        // Assert
        savedGuest.Should().NotBeNull();
        savedGuest!.FamilyUnitId.Should().Be(familyUnit.Id);
    }

    [Fact]
    public async Task Guest_OptionalFields_CanBeNull()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();
        var (user, familyUnit) = await SeedFamilyUnitAsync(dbContext);

        var guest = new Guest
        {
            Id = Guid.NewGuid(),
            FamilyUnitId = familyUnit.Id,
            FirstName = "Jane",
            LastName = "Doe",
            DateOfBirth = new DateOnly(1995, 5, 15),
            DocumentNumber = null,
            Email = null,
            Phone = null,
            MedicalNotes = null,
            Allergies = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        dbContext.Guests.Add(guest);
        await dbContext.SaveChangesAsync();

        // Assert
        var saved = await dbContext.Guests.FindAsync(guest.Id);
        saved.Should().NotBeNull();
        saved!.DocumentNumber.Should().BeNull();
        saved.Email.Should().BeNull();
        saved.MedicalNotes.Should().BeNull();
    }

    // Helper methods
    private async Task<(User user, FamilyUnit familyUnit)> SeedFamilyUnitAsync(AbuviDbContext dbContext)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"test{Guid.NewGuid()}@example.com",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hashedpassword",
            Role = UserRole.Member,
            EmailVerified = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var familyUnit = new FamilyUnit
        {
            Id = Guid.NewGuid(),
            Name = "Test Family",
            RepresentativeUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        dbContext.FamilyUnits.Add(familyUnit);
        await dbContext.SaveChangesAsync();

        return (user, familyUnit);
    }
}
