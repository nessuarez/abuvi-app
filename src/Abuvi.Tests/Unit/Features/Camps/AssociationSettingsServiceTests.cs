using Abuvi.API.Features.Camps;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

/// <summary>
/// Unit tests for AssociationSettingsService
/// Following TDD: Tests written FIRST before implementation
/// </summary>
public class AssociationSettingsServiceTests
{
    private readonly IAssociationSettingsRepository _repository;
    private readonly AssociationSettingsService _sut;

    public AssociationSettingsServiceTests()
    {
        _repository = Substitute.For<IAssociationSettingsRepository>();
        _sut = new AssociationSettingsService(_repository);
    }

    #region GetAgeRangesAsync Tests

    [Fact]
    public async Task GetAgeRangesAsync_WithExistingSetting_ReturnsAgeRanges()
    {
        // Arrange
        var settings = new AssociationSettings
        {
            Id = Guid.NewGuid(),
            SettingKey = "age_ranges",
            SettingValue = "{\"babyMaxAge\":2,\"childMinAge\":3,\"childMaxAge\":12,\"adultMinAge\":13}",
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetByKeyAsync("age_ranges", Arg.Any<CancellationToken>())
            .Returns(settings);

        // Act
        var result = await _sut.GetAgeRangesAsync();

        // Assert
        result.Should().NotBeNull();
        result!.BabyMaxAge.Should().Be(2);
        result.ChildMinAge.Should().Be(3);
        result.ChildMaxAge.Should().Be(12);
        result.AdultMinAge.Should().Be(13);
    }

    [Fact]
    public async Task GetAgeRangesAsync_WithMissingSetting_ReturnsNull()
    {
        // Arrange
        _repository.GetByKeyAsync("age_ranges", Arg.Any<CancellationToken>())
            .Returns((AssociationSettings?)null);

        // Act
        var result = await _sut.GetAgeRangesAsync();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region UpdateAgeRangesAsync Tests

    [Fact]
    public async Task UpdateAgeRangesAsync_WithValidRanges_UpdatesSetting()
    {
        // Arrange
        var request = new UpdateAgeRangesRequest(
            BabyMaxAge: 3,
            ChildMinAge: 4,
            ChildMaxAge: 14,
            AdultMinAge: 15
        );

        var userId = Guid.NewGuid();

        var existingSetting = new AssociationSettings
        {
            Id = Guid.NewGuid(),
            SettingKey = "age_ranges",
            SettingValue = "{\"babyMaxAge\":2,\"childMinAge\":3,\"childMaxAge\":12,\"adultMinAge\":13}",
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetByKeyAsync("age_ranges", Arg.Any<CancellationToken>())
            .Returns(existingSetting);

        _repository.UpdateAsync(Arg.Any<AssociationSettings>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<AssociationSettings>());

        // Act
        var result = await _sut.UpdateAgeRangesAsync(request, userId);

        // Assert
        result.Should().NotBeNull();
        result.BabyMaxAge.Should().Be(3);
        result.ChildMinAge.Should().Be(4);
        result.ChildMaxAge.Should().Be(14);
        result.AdultMinAge.Should().Be(15);
        result.UpdatedBy.Should().Be(userId);

        await _repository.Received(1).UpdateAsync(Arg.Any<AssociationSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAgeRangesAsync_WithNonExistentSetting_CreatesNewSetting()
    {
        // Arrange
        var request = new UpdateAgeRangesRequest(
            BabyMaxAge: 3,
            ChildMinAge: 4,
            ChildMaxAge: 14,
            AdultMinAge: 15
        );

        var userId = Guid.NewGuid();

        _repository.GetByKeyAsync("age_ranges", Arg.Any<CancellationToken>())
            .Returns((AssociationSettings?)null);

        _repository.CreateAsync(Arg.Any<AssociationSettings>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<AssociationSettings>());

        // Act
        var result = await _sut.UpdateAgeRangesAsync(request, userId);

        // Assert
        result.Should().NotBeNull();
        result.BabyMaxAge.Should().Be(3);
        result.ChildMinAge.Should().Be(4);
        result.ChildMaxAge.Should().Be(14);
        result.AdultMinAge.Should().Be(15);

        await _repository.Received(1).CreateAsync(Arg.Any<AssociationSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAgeRangesAsync_WithInvalidRanges_ThrowsArgumentException()
    {
        // Arrange - baby max age >= child min age (invalid)
        var request = new UpdateAgeRangesRequest(
            BabyMaxAge: 5,
            ChildMinAge: 4,
            ChildMaxAge: 14,
            AdultMinAge: 15
        );

        var userId = Guid.NewGuid();

        // Act & Assert
        var act = async () => await _sut.UpdateAgeRangesAsync(request, userId);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Age ranges must not overlap*");
    }

    [Fact]
    public async Task UpdateAgeRangesAsync_WithChildMaxGreaterThanAdultMin_ThrowsArgumentException()
    {
        // Arrange - child max age >= adult min age (invalid)
        var request = new UpdateAgeRangesRequest(
            BabyMaxAge: 2,
            ChildMinAge: 3,
            ChildMaxAge: 15,
            AdultMinAge: 14
        );

        var userId = Guid.NewGuid();

        // Act & Assert
        var act = async () => await _sut.UpdateAgeRangesAsync(request, userId);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Age ranges must not overlap*");
    }

    [Fact]
    public async Task UpdateAgeRangesAsync_WithNegativeAges_ThrowsArgumentException()
    {
        // Arrange
        var request = new UpdateAgeRangesRequest(
            BabyMaxAge: -1,
            ChildMinAge: 3,
            ChildMaxAge: 12,
            AdultMinAge: 13
        );

        var userId = Guid.NewGuid();

        // Act & Assert
        var act = async () => await _sut.UpdateAgeRangesAsync(request, userId);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Age values must be non-negative*");
    }

    #endregion
}
