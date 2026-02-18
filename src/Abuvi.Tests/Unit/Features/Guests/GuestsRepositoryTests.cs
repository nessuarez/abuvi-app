using Abuvi.API.Common.Services;
using Abuvi.API.Data;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Guests;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Guests;

public class GuestsRepositoryTests : IDisposable
{
    private readonly AbuviDbContext _dbContext;
    private readonly IEncryptionService _encryption;
    private readonly GuestsRepository _repository;

    public GuestsRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AbuviDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new AbuviDbContext(options);

        // Simple pass-through encryption for tests
        _encryption = Substitute.For<IEncryptionService>();
        _encryption.Encrypt(Arg.Any<string>()).Returns(x => x.Arg<string>());
        _encryption.Decrypt(Arg.Any<string>()).Returns(x => x.Arg<string>());

        _repository = new GuestsRepository(_dbContext, _encryption);
    }

    [Fact]
    public async Task GetByIdAsync_WhenGuestExists_ReturnsGuest()
    {
        // Arrange
        var (user, familyUnit) = await SeedFamilyUnitAsync();
        var guest = CreateTestGuest(familyUnit.Id);
        _dbContext.Guests.Add(guest);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(guest.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(guest.Id);
        result.FirstName.Should().Be(guest.FirstName);
    }

    [Fact]
    public async Task GetByIdAsync_WhenGuestDoesNotExist_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByFamilyUnitAsync_ReturnsOnlyActiveGuests()
    {
        // Arrange
        var (user, familyUnit) = await SeedFamilyUnitAsync();
        var activeGuest = CreateTestGuest(familyUnit.Id);
        var inactiveGuest = CreateTestGuest(familyUnit.Id);
        inactiveGuest.IsActive = false;

        _dbContext.Guests.AddRange(activeGuest, inactiveGuest);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByFamilyUnitAsync(familyUnit.Id, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetByFamilyUnitAsync_ReturnsGuestsOrderedByLastNameThenFirstName()
    {
        // Arrange
        var (user, familyUnit) = await SeedFamilyUnitAsync();
        var guestC = CreateTestGuest(familyUnit.Id, "Charlie", "Zebra");
        var guestA = CreateTestGuest(familyUnit.Id, "Alice", "Apple");
        var guestB = CreateTestGuest(familyUnit.Id, "Bob", "Apple");

        _dbContext.Guests.AddRange(guestC, guestA, guestB);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByFamilyUnitAsync(familyUnit.Id, CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result[0].LastName.Should().Be("Apple");
        result[0].FirstName.Should().Be("Alice");
        result[1].LastName.Should().Be("Apple");
        result[1].FirstName.Should().Be("Bob");
        result[2].LastName.Should().Be("Zebra");
    }

    [Fact]
    public async Task AddAsync_SavesGuestToDatabase()
    {
        // Arrange
        var (user, familyUnit) = await SeedFamilyUnitAsync();
        var guest = CreateTestGuest(familyUnit.Id);

        // Act
        await _repository.AddAsync(guest, CancellationToken.None);

        // Assert
        var saved = await _dbContext.Guests.FindAsync(guest.Id);
        saved.Should().NotBeNull();
        saved!.FirstName.Should().Be(guest.FirstName);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesGuestInDatabase()
    {
        // Arrange
        var (user, familyUnit) = await SeedFamilyUnitAsync();
        var guest = CreateTestGuest(familyUnit.Id);
        _dbContext.Guests.Add(guest);
        await _dbContext.SaveChangesAsync();

        // Act
        _dbContext.Entry(guest).State = EntityState.Detached;
        guest.FirstName = "UpdatedName";
        await _repository.UpdateAsync(guest, CancellationToken.None);

        // Assert
        var updated = await _dbContext.Guests.FindAsync(guest.Id);
        updated.Should().NotBeNull();
        updated!.FirstName.Should().Be("UpdatedName");
    }

    [Fact]
    public async Task DeleteAsync_RemovesGuestFromDatabase()
    {
        // Arrange
        var (user, familyUnit) = await SeedFamilyUnitAsync();
        var guest = CreateTestGuest(familyUnit.Id);
        _dbContext.Guests.Add(guest);
        await _dbContext.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(guest.Id, CancellationToken.None);

        // Assert
        var deleted = await _dbContext.Guests.FindAsync(guest.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_EncryptsSensitiveFields()
    {
        // Arrange
        var (user, familyUnit) = await SeedFamilyUnitAsync();
        var guest = CreateTestGuest(familyUnit.Id);
        guest.MedicalNotes = "Diabetes";
        guest.Allergies = "Peanuts";

        // Act
        await _repository.AddAsync(guest, CancellationToken.None);

        // Assert
        _encryption.Received(1).Encrypt("Diabetes");
        _encryption.Received(1).Encrypt("Peanuts");
    }

    [Fact]
    public async Task GetByIdAsync_DecryptsSensitiveFields()
    {
        // Arrange
        var (user, familyUnit) = await SeedFamilyUnitAsync();
        var guest = CreateTestGuest(familyUnit.Id);
        guest.MedicalNotes = "SomeNotes";
        _dbContext.Guests.Add(guest);
        await _dbContext.SaveChangesAsync();

        // Act
        _encryption.ClearReceivedCalls();
        var result = await _repository.GetByIdAsync(guest.Id, CancellationToken.None);

        // Assert
        _encryption.Received(1).Decrypt("SomeNotes");
    }

    // Helper methods
    private async Task<(User user, FamilyUnit familyUnit)> SeedFamilyUnitAsync()
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

        _dbContext.Users.Add(user);
        _dbContext.FamilyUnits.Add(familyUnit);
        await _dbContext.SaveChangesAsync();

        return (user, familyUnit);
    }

    private static Guest CreateTestGuest(Guid familyUnitId, string firstName = "John", string lastName = "Doe") => new()
    {
        Id = Guid.NewGuid(),
        FamilyUnitId = familyUnitId,
        FirstName = firstName,
        LastName = lastName,
        DateOfBirth = new DateOnly(1990, 1, 1),
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
