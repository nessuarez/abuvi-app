using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.Camps;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

public class CampObservationsServiceTests
{
    private readonly ICampObservationsRepository _repository = Substitute.For<ICampObservationsRepository>();
    private readonly CampObservationsService _sut;

    public CampObservationsServiceTests()
        => _sut = new CampObservationsService(_repository);

    [Fact]
    public async Task AddObservationAsync_WhenCampExists_CreatesAndReturnsObservation()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new AddCampObservationRequest("Test observation", "2025");

        _repository.CampExistsAsync(campId, Arg.Any<CancellationToken>()).Returns(true);
        _repository.AddAsync(Arg.Any<CampObservation>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<CampObservation>());

        // Act
        var result = await _sut.AddAsync(campId, request, userId);

        // Assert
        result.Text.Should().Be("Test observation");
        result.Season.Should().Be("2025");
        result.CreatedByUserId.Should().Be(userId);
        await _repository.Received(1).AddAsync(Arg.Any<CampObservation>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddObservationAsync_WhenCampDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var campId = Guid.NewGuid();
        _repository.CampExistsAsync(campId, Arg.Any<CancellationToken>()).Returns(false);

        // Act & Assert
        var act = async () => await _sut.AddAsync(campId, new AddCampObservationRequest("text", null), Guid.NewGuid());
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetObservationsAsync_ReturnsObservationsOrderedByCreatedAtDesc()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var observations = new List<CampObservation>
        {
            new() { Id = Guid.NewGuid(), CampId = campId, Text = "Second", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), CampId = campId, Text = "First", CreatedAt = DateTime.UtcNow.AddMinutes(-10) }
        };

        _repository.GetByCampIdAsync(campId, Arg.Any<CancellationToken>()).Returns(observations);

        // Act
        var result = await _sut.GetByCampIdAsync(campId);

        // Assert
        result.Should().HaveCount(2);
        result[0].Text.Should().Be("Second");
        result[1].Text.Should().Be("First");
    }
}
