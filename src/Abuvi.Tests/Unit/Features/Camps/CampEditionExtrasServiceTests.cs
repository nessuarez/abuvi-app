using Abuvi.API.Features.Camps;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

public class CampEditionExtrasServiceTests
{
    private readonly ICampEditionExtrasRepository _repository;
    private readonly ICampEditionsRepository _editionsRepository;
    private readonly CampEditionExtrasService _sut;

    public CampEditionExtrasServiceTests()
    {
        _repository = Substitute.For<ICampEditionExtrasRepository>();
        _editionsRepository = Substitute.For<ICampEditionsRepository>();
        _sut = new CampEditionExtrasService(_repository, _editionsRepository);
    }

    private static CampEditionExtra MakeExtra(Action<CampEditionExtra>? configure = null)
    {
        var extra = new CampEditionExtra
        {
            Id = Guid.NewGuid(),
            CampEditionId = Guid.NewGuid(),
            Name = "Camp T-Shirt",
            Description = "Official t-shirt",
            Price = 15m,
            PricingType = PricingType.PerPerson,
            PricingPeriod = PricingPeriod.OneTime,
            IsRequired = false,
            IsActive = true,
            MaxQuantity = 100,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        configure?.Invoke(extra);
        return extra;
    }

    private static CampEdition MakeEdition(CampEditionStatus status = CampEditionStatus.Draft)
        => new()
        {
            Id = Guid.NewGuid(),
            CampId = Guid.NewGuid(),
            Year = 2030,
            Status = status,
            StartDate = new DateTime(2030, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2030, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            MaxCapacity = 100,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesExtraAndReturnsResponse()
    {
        // Arrange
        var edition = MakeEdition(CampEditionStatus.Draft);
        var request = new CreateCampEditionExtraRequest(
            Name: "Camp T-Shirt",
            Description: "Official t-shirt",
            Price: 15m,
            PricingType: PricingType.PerPerson,
            PricingPeriod: PricingPeriod.OneTime,
            IsRequired: false,
            MaxQuantity: 100
        );

        _editionsRepository.GetByIdAsync(edition.Id, Arg.Any<CancellationToken>())
            .Returns(edition);

        // Act
        var result = await _sut.CreateAsync(edition.Id, request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Camp T-Shirt");
        result.Price.Should().Be(15m);
        result.PricingType.Should().Be(PricingType.PerPerson);
        result.IsActive.Should().BeTrue();
        result.CurrentQuantitySold.Should().Be(0);

        await _repository.Received(1).AddAsync(Arg.Any<CampEditionExtra>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenEditionNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        _editionsRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((CampEdition?)null);

        var request = new CreateCampEditionExtraRequest("Name", null, 10m,
            PricingType.PerPerson, PricingPeriod.OneTime, false, null);

        // Act
        var act = () => _sut.CreateAsync(Guid.NewGuid(), request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*edición*");
    }

    [Fact]
    public async Task CreateAsync_WhenEditionIsCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var edition = MakeEdition(CampEditionStatus.Completed);
        _editionsRepository.GetByIdAsync(edition.Id, Arg.Any<CancellationToken>())
            .Returns(edition);

        var request = new CreateCampEditionExtraRequest("Name", null, 10m,
            PricingType.PerPerson, PricingPeriod.OneTime, false, null);

        // Act
        var act = () => _sut.CreateAsync(edition.Id, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cerrada o completada*");
    }

    [Fact]
    public async Task CreateAsync_WhenEditionIsClosed_ThrowsInvalidOperationException()
    {
        // Arrange
        var edition = MakeEdition(CampEditionStatus.Closed);
        _editionsRepository.GetByIdAsync(edition.Id, Arg.Any<CancellationToken>())
            .Returns(edition);

        var request = new CreateCampEditionExtraRequest("Name", null, 10m,
            PricingType.PerPerson, PricingPeriod.OneTime, false, null);

        // Act
        var act = () => _sut.CreateAsync(edition.Id, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_WithZeroPrice_CreatesExtraSuccessfully()
    {
        // Arrange
        var edition = MakeEdition(CampEditionStatus.Open);
        _editionsRepository.GetByIdAsync(edition.Id, Arg.Any<CancellationToken>())
            .Returns(edition);

        var request = new CreateCampEditionExtraRequest("Free Extra", null, 0m,
            PricingType.PerFamily, PricingPeriod.OneTime, true, null);

        // Act
        var result = await _sut.CreateAsync(edition.Id, request);

        // Assert
        result.Price.Should().Be(0m);
        result.IsRequired.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WithNullMaxQuantity_AllowsUnlimited()
    {
        // Arrange
        var edition = MakeEdition();
        _editionsRepository.GetByIdAsync(edition.Id, Arg.Any<CancellationToken>())
            .Returns(edition);

        var request = new CreateCampEditionExtraRequest("Unlimited Extra", null, 5m,
            PricingType.PerPerson, PricingPeriod.PerDay, false, null);

        // Act
        var result = await _sut.CreateAsync(edition.Id, request);

        // Assert
        result.MaxQuantity.Should().BeNull();
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesExtraAndReturnsResponse()
    {
        // Arrange
        var extra = MakeExtra();
        _repository.GetByIdAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(extra);
        _repository.GetQuantitySoldAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(0);

        var request = new UpdateCampEditionExtraRequest(
            Name: "Updated Name",
            Description: "Updated description",
            Price: 20m,
            IsRequired: true,
            IsActive: true,
            MaxQuantity: 50
        );

        // Act
        var result = await _sut.UpdateAsync(extra.Id, request);

        // Assert
        result.Name.Should().Be("Updated Name");
        result.Price.Should().Be(20m);
        result.MaxQuantity.Should().Be(50);
        result.IsRequired.Should().BeTrue();

        await _repository.Received(1).UpdateAsync(Arg.Any<CampEditionExtra>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WhenExtraNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((CampEditionExtra?)null);

        var request = new UpdateCampEditionExtraRequest("Name", null, 10m, false, true, null);

        // Act
        var act = () => _sut.UpdateAsync(Guid.NewGuid(), request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*extra*");
    }

    [Fact]
    public async Task UpdateAsync_WhenReducingMaxQuantityBelowSold_ThrowsInvalidOperationException()
    {
        // Arrange
        var extra = MakeExtra(e => e.MaxQuantity = 100);
        _repository.GetByIdAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(extra);
        _repository.GetQuantitySoldAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(50);

        var request = new UpdateCampEditionExtraRequest("Name", null, extra.Price, false, true, 10);

        // Act
        var act = () => _sut.UpdateAsync(extra.Id, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cantidad máxima*");
    }

    [Fact]
    public async Task UpdateAsync_WhenChangingPriceOnSoldExtra_ThrowsInvalidOperationException()
    {
        // Arrange
        var extra = MakeExtra(e => e.Price = 15m);
        _repository.GetByIdAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(extra);
        _repository.GetQuantitySoldAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(5);

        var request = new UpdateCampEditionExtraRequest("Name", null, 20m, false, true, null);

        // Act
        var act = () => _sut.UpdateAsync(extra.Id, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*precio*");
    }

    [Fact]
    public async Task UpdateAsync_WhenSoldIsZeroAndChangingPrice_AllowsUpdate()
    {
        // Arrange
        var extra = MakeExtra(e => e.Price = 15m);
        _repository.GetByIdAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(extra);
        _repository.GetQuantitySoldAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(0);

        var request = new UpdateCampEditionExtraRequest("Name", null, 25m, false, true, null);

        // Act
        var result = await _sut.UpdateAsync(extra.Id, request);

        // Assert
        result.Price.Should().Be(25m);
    }

    [Fact]
    public async Task UpdateAsync_WhenReducingMaxQuantityAboveSold_AllowsUpdate()
    {
        // Arrange
        var extra = MakeExtra(e => e.MaxQuantity = 100);
        _repository.GetByIdAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(extra);
        _repository.GetQuantitySoldAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(10);

        // Reduce to 20 — still above the 10 sold
        var request = new UpdateCampEditionExtraRequest("Name", null, extra.Price, false, true, 20);

        // Act
        var result = await _sut.UpdateAsync(extra.Id, request);

        // Assert
        result.MaxQuantity.Should().Be(20);
    }

    [Fact]
    public async Task UpdateAsync_CanDeactivateExtra()
    {
        // Arrange
        var extra = MakeExtra(e => e.IsActive = true);
        _repository.GetByIdAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(extra);
        _repository.GetQuantitySoldAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(0);

        var request = new UpdateCampEditionExtraRequest("Name", null, extra.Price, false, false, null);

        // Act
        var result = await _sut.UpdateAsync(extra.Id, request);

        // Assert
        result.IsActive.Should().BeFalse();
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_WhenNotSold_DeletesAndReturnsTrue()
    {
        // Arrange
        var extra = MakeExtra();
        _repository.GetByIdAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(extra);
        _repository.GetQuantitySoldAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(0);

        // Act
        var result = await _sut.DeleteAsync(extra.Id);

        // Assert
        result.Should().BeTrue();
        await _repository.Received(1).DeleteAsync(extra.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenExtraNotFound_ReturnsFalse()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((CampEditionExtra?)null);

        // Act
        var result = await _sut.DeleteAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
        await _repository.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenExtraHasSold_ThrowsInvalidOperationException()
    {
        // Arrange
        var extra = MakeExtra();
        _repository.GetByIdAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(extra);
        _repository.GetQuantitySoldAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(3);

        // Act
        var act = () => _sut.DeleteAsync(extra.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*seleccionado*");
    }

    #endregion

    #region GetByEditionAsync

    [Fact]
    public async Task GetByEditionAsync_ReturnsExtrasWithQuantitySold()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        var extra1 = MakeExtra(e => e.CampEditionId = editionId);
        var extra2 = MakeExtra(e => e.CampEditionId = editionId);

        _repository.GetByCampEditionAsync(editionId, null, Arg.Any<CancellationToken>())
            .Returns([extra1, extra2]);
        _repository.GetQuantitySoldAsync(extra1.Id, Arg.Any<CancellationToken>()).Returns(5);
        _repository.GetQuantitySoldAsync(extra2.Id, Arg.Any<CancellationToken>()).Returns(0);

        // Act
        var result = await _sut.GetByEditionAsync(editionId, null);

        // Assert
        result.Should().HaveCount(2);
        result[0].CurrentQuantitySold.Should().Be(5);
        result[1].CurrentQuantitySold.Should().Be(0);
    }

    [Fact]
    public async Task GetByEditionAsync_WithActiveOnly_PassesFilterToRepository()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        _repository.GetByCampEditionAsync(editionId, true, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        await _sut.GetByEditionAsync(editionId, activeOnly: true);

        // Assert
        await _repository.Received(1)
            .GetByCampEditionAsync(editionId, true, Arg.Any<CancellationToken>());
    }

    #endregion

    #region IsAvailableAsync

    [Fact]
    public async Task IsAvailableAsync_WhenUnlimited_ReturnsTrue()
    {
        // Arrange
        var extra = MakeExtra(e => e.MaxQuantity = null);
        _repository.GetByIdAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(extra);

        // Act
        var result = await _sut.IsAvailableAsync(extra.Id, requestedQuantity: 999);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_WhenQuantityAvailable_ReturnsTrue()
    {
        // Arrange
        var extra = MakeExtra(e => e.MaxQuantity = 100);
        _repository.GetByIdAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(extra);
        _repository.GetQuantitySoldAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(60);

        // Act
        var result = await _sut.IsAvailableAsync(extra.Id, requestedQuantity: 30);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_WhenQuantityExceeded_ReturnsFalse()
    {
        // Arrange
        var extra = MakeExtra(e => e.MaxQuantity = 100);
        _repository.GetByIdAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(extra);
        _repository.GetQuantitySoldAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(90);

        // Act
        var result = await _sut.IsAvailableAsync(extra.Id, requestedQuantity: 20);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAvailableAsync_WhenExtraNotFound_ReturnsFalse()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((CampEditionExtra?)null);

        // Act
        var result = await _sut.IsAvailableAsync(Guid.NewGuid(), 1);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAvailableAsync_WhenExtraInactive_ReturnsFalse()
    {
        // Arrange
        var extra = MakeExtra(e => e.IsActive = false);
        _repository.GetByIdAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(extra);

        // Act
        var result = await _sut.IsAvailableAsync(extra.Id, 1);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ActivateAsync / DeactivateAsync

    [Fact]
    public async Task ActivateAsync_SetsIsActiveToTrue()
    {
        // Arrange
        var extra = MakeExtra(e => e.IsActive = false);
        _repository.GetByIdAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(extra);
        _repository.GetQuantitySoldAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(0);

        // Act
        var result = await _sut.ActivateAsync(extra.Id);

        // Assert
        result.IsActive.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(Arg.Is<CampEditionExtra>(e => e.IsActive), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateAsync_SetsIsActiveToFalse()
    {
        // Arrange
        var extra = MakeExtra(e => e.IsActive = true);
        _repository.GetByIdAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(extra);
        _repository.GetQuantitySoldAsync(extra.Id, Arg.Any<CancellationToken>()).Returns(0);

        // Act
        var result = await _sut.DeactivateAsync(extra.Id);

        // Assert
        result.IsActive.Should().BeFalse();
        await _repository.Received(1).UpdateAsync(Arg.Is<CampEditionExtra>(e => !e.IsActive), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActivateAsync_WhenExtraNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((CampEditionExtra?)null);

        // Act
        var act = () => _sut.ActivateAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*extra*");
    }

    #endregion
}
