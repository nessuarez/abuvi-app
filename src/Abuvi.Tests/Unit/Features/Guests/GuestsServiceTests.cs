using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Guests;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Guests;

public class GuestsServiceTests
{
    private readonly IGuestsRepository _repository;
    private readonly IFamilyUnitsRepository _familyUnitsRepository;
    private readonly GuestsService _service;

    public GuestsServiceTests()
    {
        _repository = Substitute.For<IGuestsRepository>();
        _familyUnitsRepository = Substitute.For<IFamilyUnitsRepository>();
        _service = new GuestsService(_repository, _familyUnitsRepository);
    }

    [Fact]
    public async Task CreateAsync_WhenFamilyUnitExists_CreatesGuest()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var familyUnit = CreateTestFamilyUnit(familyUnitId);
        var request = new CreateGuestRequest("Jane", "Doe", new DateOnly(1995, 5, 15));

        _familyUnitsRepository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        // Act
        var result = await _service.CreateAsync(familyUnitId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FirstName.Should().Be("Jane");
        result.LastName.Should().Be("Doe");
        result.FamilyUnitId.Should().Be(familyUnitId);
        result.IsActive.Should().BeTrue();

        await _repository.Received(1).AddAsync(Arg.Any<Guest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenFamilyUnitDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var request = new CreateGuestRequest("Jane", "Doe", new DateOnly(1995, 5, 15));

        _familyUnitsRepository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns((FamilyUnit?)null);

        // Act & Assert
        await _service.Invoking(s => s.CreateAsync(familyUnitId, request, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_NormalizesDocumentNumberToUppercase()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        _familyUnitsRepository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(CreateTestFamilyUnit(familyUnitId));

        var request = new CreateGuestRequest("Jane", "Doe", new DateOnly(1995, 5, 15),
            DocumentNumber: "abc123");

        Guest? capturedGuest = null;
        await _repository.AddAsync(Arg.Do<Guest>(g => capturedGuest = g), Arg.Any<CancellationToken>());

        // Act
        await _service.CreateAsync(familyUnitId, request, CancellationToken.None);

        // Assert
        capturedGuest.Should().NotBeNull();
        capturedGuest!.DocumentNumber.Should().Be("ABC123");
    }

    [Fact]
    public async Task UpdateAsync_WhenGuestExists_UpdatesGuest()
    {
        // Arrange
        var guestId = Guid.NewGuid();
        var existingGuest = CreateTestGuest(guestId);
        var request = new UpdateGuestRequest("UpdatedFirst", "UpdatedLast", new DateOnly(1995, 6, 20),
            DocumentNumber: "newdoc");

        _repository.GetByIdAsync(guestId, Arg.Any<CancellationToken>())
            .Returns(existingGuest);

        // Act
        var result = await _service.UpdateAsync(guestId, request, CancellationToken.None);

        // Assert
        result.FirstName.Should().Be("UpdatedFirst");
        result.LastName.Should().Be("UpdatedLast");
        result.DocumentNumber.Should().Be("NEWDOC");

        await _repository.Received(1).UpdateAsync(Arg.Any<Guest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WhenGuestDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var guestId = Guid.NewGuid();
        _repository.GetByIdAsync(guestId, Arg.Any<CancellationToken>())
            .Returns((Guest?)null);

        var request = new UpdateGuestRequest("Jane", "Doe", new DateOnly(1995, 5, 15));

        // Act & Assert
        await _service.Invoking(s => s.UpdateAsync(guestId, request, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenGuestExists_ReturnsGuest()
    {
        // Arrange
        var guestId = Guid.NewGuid();
        var guest = CreateTestGuest(guestId);

        _repository.GetByIdAsync(guestId, Arg.Any<CancellationToken>())
            .Returns(guest);

        // Act
        var result = await _service.GetByIdAsync(guestId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(guestId);
    }

    [Fact]
    public async Task GetByIdAsync_WhenGuestDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var guestId = Guid.NewGuid();
        _repository.GetByIdAsync(guestId, Arg.Any<CancellationToken>())
            .Returns((Guest?)null);

        // Act & Assert
        await _service.Invoking(s => s.GetByIdAsync(guestId, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetByFamilyUnitAsync_ReturnsGuestList()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var guests = new List<Guest>
        {
            CreateTestGuest(Guid.NewGuid()),
            CreateTestGuest(Guid.NewGuid())
        };

        _repository.GetByFamilyUnitAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(guests);

        // Act
        var result = await _service.GetByFamilyUnitAsync(familyUnitId, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteAsync_WhenGuestExists_SoftDeletesGuest()
    {
        // Arrange
        var guestId = Guid.NewGuid();
        var guest = CreateTestGuest(guestId);
        guest.IsActive = true;

        _repository.GetByIdAsync(guestId, Arg.Any<CancellationToken>())
            .Returns(guest);

        Guest? updatedGuest = null;
        await _repository.UpdateAsync(Arg.Do<Guest>(g => updatedGuest = g), Arg.Any<CancellationToken>());

        // Act
        await _service.DeleteAsync(guestId, CancellationToken.None);

        // Assert
        updatedGuest.Should().NotBeNull();
        updatedGuest!.IsActive.Should().BeFalse();

        await _repository.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenGuestDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var guestId = Guid.NewGuid();
        _repository.GetByIdAsync(guestId, Arg.Any<CancellationToken>())
            .Returns((Guest?)null);

        // Act & Assert
        await _service.Invoking(s => s.DeleteAsync(guestId, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    // Helper methods
    private static FamilyUnit CreateTestFamilyUnit(Guid id) => new()
    {
        Id = id,
        Name = "Test Family",
        RepresentativeUserId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Guest CreateTestGuest(Guid id) => new()
    {
        Id = id,
        FamilyUnitId = Guid.NewGuid(),
        FirstName = "John",
        LastName = "Doe",
        DateOfBirth = new DateOnly(1990, 1, 1),
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
