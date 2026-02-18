using Abuvi.API.Features.Camps;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

/// <summary>
/// Unit tests for CampPhotosService
/// </summary>
public class CampPhotosServiceTests
{
    private readonly ICampsRepository _repository;
    private readonly CampPhotosService _sut;

    public CampPhotosServiceTests()
    {
        _repository = Substitute.For<ICampsRepository>();
        _sut = new CampPhotosService(_repository);
    }

    private static Camp CreateTestCamp(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "Test Camp",
        IsActive = true
    };

    private static CampPhoto CreateTestPhoto(Guid campId, Guid? id = null, bool isPrimary = false, int displayOrder = 1) => new()
    {
        Id = id ?? Guid.NewGuid(),
        CampId = campId,
        PhotoUrl = "https://example.com/photo.jpg",
        IsOriginal = false,
        IsPrimary = isPrimary,
        DisplayOrder = displayOrder,
        Width = 0,
        Height = 0,
        AttributionName = string.Empty
    };

    #region AddPhotoAsync

    [Fact]
    public async Task AddPhotoAsync_WithValidData_CreatesPhoto()
    {
        // Arrange
        var camp = CreateTestCamp();
        var request = new AddCampPhotoRequest(
            Url: "https://example.com/photo.jpg",
            Description: "Main entrance",
            DisplayOrder: 1,
            IsPrimary: false
        );

        _repository.GetByIdAsync(camp.Id, Arg.Any<CancellationToken>())
            .Returns(camp);
        _repository.AddPhotoAsync(Arg.Any<CampPhoto>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<CampPhoto>());

        // Act
        var result = await _sut.AddPhotoAsync(camp.Id, request);

        // Assert
        result.PhotoUrl.Should().Be(request.Url);
        result.Description.Should().Be(request.Description);
        result.DisplayOrder.Should().Be(request.DisplayOrder);
        result.IsPrimary.Should().BeFalse();

        await _repository.Received(1).AddPhotoAsync(
            Arg.Is<CampPhoto>(p =>
                p.CampId == camp.Id &&
                p.PhotoUrl == request.Url &&
                p.IsOriginal == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddPhotoAsync_WhenCampNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var request = new AddCampPhotoRequest("https://example.com/photo.jpg", null, 1, false);

        _repository.GetByIdAsync(campId, Arg.Any<CancellationToken>())
            .Returns((Camp?)null);

        // Act
        var act = async () => await _sut.AddPhotoAsync(campId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Camp not found*");
    }

    [Fact]
    public async Task AddPhotoAsync_WhenIsPrimaryTrue_ClearsPreviousPrimaryFirst()
    {
        // Arrange
        var camp = CreateTestCamp();
        var request = new AddCampPhotoRequest("https://example.com/photo.jpg", null, 1, IsPrimary: true);

        _repository.GetByIdAsync(camp.Id, Arg.Any<CancellationToken>()).Returns(camp);
        _repository.AddPhotoAsync(Arg.Any<CampPhoto>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<CampPhoto>());

        // Act
        await _sut.AddPhotoAsync(camp.Id, request);

        // Assert: ClearPrimaryPhotoAsync called before adding
        await _repository.Received(1).ClearPrimaryPhotoAsync(camp.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddPhotoAsync_WhenIsPrimaryFalse_DoesNotClearPrimary()
    {
        // Arrange
        var camp = CreateTestCamp();
        var request = new AddCampPhotoRequest("https://example.com/photo.jpg", null, 1, IsPrimary: false);

        _repository.GetByIdAsync(camp.Id, Arg.Any<CancellationToken>()).Returns(camp);
        _repository.AddPhotoAsync(Arg.Any<CampPhoto>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<CampPhoto>());

        // Act
        await _sut.AddPhotoAsync(camp.Id, request);

        // Assert
        await _repository.DidNotReceive().ClearPrimaryPhotoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region UpdatePhotoAsync

    [Fact]
    public async Task UpdatePhotoAsync_WithValidData_UpdatesPhoto()
    {
        // Arrange
        var camp = CreateTestCamp();
        var photo = CreateTestPhoto(camp.Id, isPrimary: false);
        var request = new UpdateCampPhotoRequest(
            Url: "https://example.com/new-photo.jpg",
            Description: "Updated description",
            DisplayOrder: 2,
            IsPrimary: false
        );

        _repository.GetPhotoByIdAsync(photo.Id, Arg.Any<CancellationToken>()).Returns(photo);
        _repository.UpdatePhotoAsync(Arg.Any<CampPhoto>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<CampPhoto>());

        // Act
        var result = await _sut.UpdatePhotoAsync(camp.Id, photo.Id, request);

        // Assert
        result.Should().NotBeNull();
        result!.PhotoUrl.Should().Be(request.Url);
        result.Description.Should().Be(request.Description);
        result.DisplayOrder.Should().Be(request.DisplayOrder);
    }

    [Fact]
    public async Task UpdatePhotoAsync_WhenPhotoNotFound_ReturnsNull()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        var request = new UpdateCampPhotoRequest("https://example.com/photo.jpg", null, 1, false);

        _repository.GetPhotoByIdAsync(photoId, Arg.Any<CancellationToken>()).Returns((CampPhoto?)null);

        // Act
        var result = await _sut.UpdatePhotoAsync(campId, photoId, request);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePhotoAsync_WhenPhotoDoesNotBelongToCamp_ReturnsNull()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var otherCampId = Guid.NewGuid();
        var photo = CreateTestPhoto(otherCampId);
        var request = new UpdateCampPhotoRequest("https://example.com/photo.jpg", null, 1, false);

        _repository.GetPhotoByIdAsync(photo.Id, Arg.Any<CancellationToken>()).Returns(photo);

        // Act
        var result = await _sut.UpdatePhotoAsync(campId, photo.Id, request);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePhotoAsync_WhenSettingPrimary_ClearsPreviousPrimary()
    {
        // Arrange
        var camp = CreateTestCamp();
        var photo = CreateTestPhoto(camp.Id, isPrimary: false);
        var request = new UpdateCampPhotoRequest("https://example.com/photo.jpg", null, 1, IsPrimary: true);

        _repository.GetPhotoByIdAsync(photo.Id, Arg.Any<CancellationToken>()).Returns(photo);
        _repository.UpdatePhotoAsync(Arg.Any<CampPhoto>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<CampPhoto>());

        // Act
        await _sut.UpdatePhotoAsync(camp.Id, photo.Id, request);

        // Assert
        await _repository.Received(1).ClearPrimaryPhotoAsync(camp.Id, Arg.Any<CancellationToken>());
    }

    #endregion

    #region DeletePhotoAsync

    [Fact]
    public async Task DeletePhotoAsync_WithValidIds_DeletesPhoto()
    {
        // Arrange
        var camp = CreateTestCamp();
        var photo = CreateTestPhoto(camp.Id);

        _repository.GetPhotoByIdAsync(photo.Id, Arg.Any<CancellationToken>()).Returns(photo);
        _repository.DeletePhotoAsync(photo.Id, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await _sut.DeletePhotoAsync(camp.Id, photo.Id);

        // Assert
        result.Should().BeTrue();
        await _repository.Received(1).DeletePhotoAsync(photo.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeletePhotoAsync_WhenPhotoNotFound_ReturnsFalse()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        _repository.GetPhotoByIdAsync(photoId, Arg.Any<CancellationToken>()).Returns((CampPhoto?)null);

        // Act
        var result = await _sut.DeletePhotoAsync(campId, photoId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeletePhotoAsync_WhenPhotoDoesNotBelongToCamp_ReturnsFalse()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var photo = CreateTestPhoto(Guid.NewGuid()); // different camp

        _repository.GetPhotoByIdAsync(photo.Id, Arg.Any<CancellationToken>()).Returns(photo);

        // Act
        var result = await _sut.DeletePhotoAsync(campId, photo.Id);

        // Assert
        result.Should().BeFalse();
        await _repository.DidNotReceive().DeletePhotoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region ReorderPhotosAsync

    [Fact]
    public async Task ReorderPhotosAsync_WithValidCamp_UpdatesOrders()
    {
        // Arrange
        var camp = CreateTestCamp();
        var request = new ReorderCampPhotosRequest(new List<PhotoOrderItem>
        {
            new(Guid.NewGuid(), 1),
            new(Guid.NewGuid(), 2)
        });

        _repository.GetByIdAsync(camp.Id, Arg.Any<CancellationToken>()).Returns(camp);

        // Act
        var result = await _sut.ReorderPhotosAsync(camp.Id, request);

        // Assert
        result.Should().BeTrue();
        await _repository.Received(1).UpdatePhotoOrdersAsync(
            Arg.Any<IEnumerable<(Guid, int)>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReorderPhotosAsync_WhenCampNotFound_ReturnsFalse()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var request = new ReorderCampPhotosRequest(new List<PhotoOrderItem>());

        _repository.GetByIdAsync(campId, Arg.Any<CancellationToken>()).Returns((Camp?)null);

        // Act
        var result = await _sut.ReorderPhotosAsync(campId, request);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region SetPrimaryPhotoAsync

    [Fact]
    public async Task SetPrimaryPhotoAsync_WithValidIds_SetsPrimaryAndClearsOthers()
    {
        // Arrange
        var camp = CreateTestCamp();
        var photo = CreateTestPhoto(camp.Id, isPrimary: false);

        _repository.GetPhotoByIdAsync(photo.Id, Arg.Any<CancellationToken>())
            .Returns(photo, photo); // called twice (before and after clear)
        _repository.UpdatePhotoAsync(Arg.Any<CampPhoto>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<CampPhoto>());

        // Act
        var result = await _sut.SetPrimaryPhotoAsync(camp.Id, photo.Id);

        // Assert
        result.Should().NotBeNull();
        result!.IsPrimary.Should().BeTrue();
        await _repository.Received(1).ClearPrimaryPhotoAsync(camp.Id, Arg.Any<CancellationToken>());
        await _repository.Received(1).UpdatePhotoAsync(
            Arg.Is<CampPhoto>(p => p.IsPrimary),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetPrimaryPhotoAsync_WhenPhotoNotFound_ReturnsNull()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        _repository.GetPhotoByIdAsync(photoId, Arg.Any<CancellationToken>()).Returns((CampPhoto?)null);

        // Act
        var result = await _sut.SetPrimaryPhotoAsync(campId, photoId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetPrimaryPhotoAsync_WhenPhotoDoesNotBelongToCamp_ReturnsNull()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var photo = CreateTestPhoto(Guid.NewGuid()); // different camp

        _repository.GetPhotoByIdAsync(photo.Id, Arg.Any<CancellationToken>()).Returns(photo);

        // Act
        var result = await _sut.SetPrimaryPhotoAsync(campId, photo.Id);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetPhotosAsync

    [Fact]
    public async Task GetPhotosAsync_ReturnsPhotosOrderedByDisplayOrder()
    {
        // Arrange
        var camp = CreateTestCamp();
        var photos = new List<CampPhoto>
        {
            CreateTestPhoto(camp.Id, displayOrder: 1),
            CreateTestPhoto(camp.Id, displayOrder: 2),
            CreateTestPhoto(camp.Id, displayOrder: 3)
        };

        _repository.GetPhotosForCampAsync(camp.Id, Arg.Any<CancellationToken>()).Returns(photos);

        // Act
        var result = await _sut.GetPhotosAsync(camp.Id);

        // Assert
        result.Should().HaveCount(3);
    }

    #endregion
}
