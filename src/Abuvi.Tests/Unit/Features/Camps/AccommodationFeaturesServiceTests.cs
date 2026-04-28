using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.Camps;
using Abuvi.Tests.Helpers.Builders;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

public class AccommodationFeaturesServiceTests
{
    private readonly IAccommodationFeaturesRepository _repo;
    private readonly AccommodationFeaturesService _sut;

    public AccommodationFeaturesServiceTests()
    {
        _repo = Substitute.For<IAccommodationFeaturesRepository>();
        _sut = new AccommodationFeaturesService(_repo);
    }

    [Fact]
    public async Task GetAllAsync_WhenFeaturesExist_ReturnsAllFeatureResponses()
    {
        var features = new List<AccommodationFeature>
        {
            new AccommodationFeatureBuilder().WithName("Feature A").WithSortOrder(0).Build(),
            new AccommodationFeatureBuilder().WithName("Feature B").WithSortOrder(1).Build()
        };
        _repo.GetAllAsync(null, default).Returns(features);

        var result = await _sut.GetAllAsync(null, default);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Feature A");
    }

    [Fact]
    public async Task GetAllAsync_WithActiveOnlyTrue_ReturnsOnlyActiveFeatures()
    {
        var features = new List<AccommodationFeature>
        {
            new AccommodationFeatureBuilder().WithIsActive(true).Build()
        };
        _repo.GetAllAsync(true, default).Returns(features);

        var result = await _sut.GetAllAsync(true, default);

        result.Should().HaveCount(1);
        result[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllAsync_WithActiveOnlyFalse_ReturnsAllFeatures()
    {
        var features = new List<AccommodationFeature>
        {
            new AccommodationFeatureBuilder().WithIsActive(false).Build(),
            new AccommodationFeatureBuilder().WithIsActive(true).Build()
        };
        _repo.GetAllAsync(false, default).Returns(features);

        var result = await _sut.GetAllAsync(false, default);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_WhenFeatureExists_ReturnsFeatureResponse()
    {
        var feature = new AccommodationFeatureBuilder().WithName("Test").Build();
        _repo.GetByIdAsync(feature.Id, default).Returns(feature);

        var result = await _sut.GetByIdAsync(feature.Id, default);

        result.Id.Should().Be(feature.Id);
        result.Name.Should().Be("Test");
    }

    [Fact]
    public async Task GetByIdAsync_WhenFeatureDoesNotExist_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        _repo.GetByIdAsync(id, default).Returns((AccommodationFeature?)null);

        var act = () => _sut.GetByIdAsync(id, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_SetsDefaultIsActiveTrue()
    {
        var request = new CreateAccommodationFeatureRequest("Feature X", "icon", null, FeatureApplicabilityLevel.Any);
        _repo.GetByNameAsync("Feature X", default).Returns((AccommodationFeature?)null);
        _repo.AddAsync(Arg.Any<AccommodationFeature>(), default)
            .Returns(call => call.Arg<AccommodationFeature>());

        var result = await _sut.CreateAsync(request, default);

        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateName_ThrowsBusinessRuleException()
    {
        var existing = new AccommodationFeatureBuilder().WithName("Duplicate").Build();
        _repo.GetByNameAsync("Duplicate", default).Returns(existing);

        var act = () => _sut.CreateAsync(
            new CreateAccommodationFeatureRequest("Duplicate", "icon", null, FeatureApplicabilityLevel.Any),
            default);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task UpdateAsync_WithValidRequest_UpdatesAllFields()
    {
        var feature = new AccommodationFeatureBuilder().Build();
        _repo.GetByIdAsync(feature.Id, default).Returns(feature);
        _repo.GetByNameAsync("New Name", default).Returns((AccommodationFeature?)null);
        _repo.UpdateAsync(Arg.Any<AccommodationFeature>(), default)
            .Returns(call => call.Arg<AccommodationFeature>());

        var request = new UpdateAccommodationFeatureRequest("New Name", "new-icon", "desc", FeatureApplicabilityLevel.Zone, false, 5);
        var result = await _sut.UpdateAsync(feature.Id, request, default);

        result.Name.Should().Be("New Name");
        result.IsActive.Should().BeFalse();
        result.SortOrder.Should().Be(5);
    }

    [Fact]
    public async Task UpdateAsync_WhenFeatureNotFound_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        _repo.GetByIdAsync(id, default).Returns((AccommodationFeature?)null);

        var act = () => _sut.UpdateAsync(id,
            new UpdateAccommodationFeatureRequest("n", "i", null, FeatureApplicabilityLevel.Any, true, 0),
            default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_WithDuplicateNameOnAnotherFeature_ThrowsBusinessRuleException()
    {
        var feature = new AccommodationFeatureBuilder().Build();
        var otherFeature = new AccommodationFeatureBuilder().WithName("Taken Name").Build();
        _repo.GetByIdAsync(feature.Id, default).Returns(feature);
        _repo.GetByNameAsync("Taken Name", default).Returns(otherFeature);

        var act = () => _sut.UpdateAsync(feature.Id,
            new UpdateAccommodationFeatureRequest("Taken Name", "i", null, FeatureApplicabilityLevel.Any, true, 0),
            default);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task DeleteAsync_WhenNoAssignments_DeletesSuccessfully()
    {
        var feature = new AccommodationFeatureBuilder().Build();
        _repo.GetByIdAsync(feature.Id, default).Returns(feature);
        _repo.HasAssignmentsAsync(feature.Id, default).Returns(false);

        await _sut.DeleteAsync(feature.Id, default);

        await _repo.Received(1).DeleteAsync(feature, default);
    }

    [Fact]
    public async Task DeleteAsync_WhenHasAssignments_ThrowsBusinessRuleException()
    {
        var feature = new AccommodationFeatureBuilder().Build();
        _repo.GetByIdAsync(feature.Id, default).Returns(feature);
        _repo.HasAssignmentsAsync(feature.Id, default).Returns(true);

        var act = () => _sut.DeleteAsync(feature.Id, default);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task DeleteAsync_WhenFeatureNotFound_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        _repo.GetByIdAsync(id, default).Returns((AccommodationFeature?)null);

        var act = () => _sut.DeleteAsync(id, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
