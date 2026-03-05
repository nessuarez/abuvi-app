using Abuvi.API.Features.MediaItems;
using FluentAssertions;
using Xunit;

namespace Abuvi.Tests.Unit.Features.MediaItems;

public class MediaItemsValidatorTests
{
    private readonly CreateMediaItemRequestValidator _validator = new();

    [Fact]
    public void Validate_WithValidPhotoRequest_Passes()
    {
        // Arrange
        var request = new CreateMediaItemRequest(
            "https://example.com/photo.jpg",
            "https://example.com/thumb.webp",
            MediaItemType.Photo, "Beach Day", "A fun day", 1990, null, null, "camp");

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyFileUrl_Fails()
    {
        // Arrange
        var request = new CreateMediaItemRequest(
            "", "https://example.com/thumb.webp",
            MediaItemType.Photo, "Title", null, null, null, null, null);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FileUrl");
    }

    [Fact]
    public void Validate_WithFileUrlOver2048Chars_Fails()
    {
        // Arrange
        var longUrl = "https://example.com/" + new string('a', 2030);
        var request = new CreateMediaItemRequest(
            longUrl, "https://example.com/thumb.webp",
            MediaItemType.Photo, "Title", null, null, null, null, null);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FileUrl");
    }

    [Fact]
    public void Validate_WithInvalidType_Fails()
    {
        // Arrange
        var request = new CreateMediaItemRequest(
            "https://example.com/file.jpg", null,
            (MediaItemType)999, "Title", null, null, null, null, null);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Type");
    }

    [Fact]
    public void Validate_WithEmptyTitle_Fails()
    {
        // Arrange
        var request = new CreateMediaItemRequest(
            "https://example.com/file.jpg", null,
            MediaItemType.Audio, "", null, null, null, null, null);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title");
    }

    [Fact]
    public void Validate_WithDescriptionOver1000Chars_Fails()
    {
        // Arrange
        var longDescription = new string('A', 1001);
        var request = new CreateMediaItemRequest(
            "https://example.com/file.jpg", null,
            MediaItemType.Audio, "Title", longDescription, null, null, null, null);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Description");
    }

    [Fact]
    public void Validate_WithPhotoTypeAndNoThumbnail_Fails()
    {
        // Arrange
        var request = new CreateMediaItemRequest(
            "https://example.com/photo.jpg", null,
            MediaItemType.Photo, "Photo Title", null, null, null, null, null);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ThumbnailUrl");
    }

    [Fact]
    public void Validate_WithVideoTypeAndNoThumbnail_Fails()
    {
        // Arrange
        var request = new CreateMediaItemRequest(
            "https://example.com/video.mp4", null,
            MediaItemType.Video, "Video Title", null, null, null, null, null);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ThumbnailUrl");
    }

    [Fact]
    public void Validate_WithAudioTypeAndNoThumbnail_Passes()
    {
        // Arrange
        var request = new CreateMediaItemRequest(
            "https://example.com/audio.mp3", null,
            MediaItemType.Audio, "Audio Title", null, null, null, null, null);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithDocumentTypeAndNoThumbnail_Passes()
    {
        // Arrange
        var request = new CreateMediaItemRequest(
            "https://example.com/doc.pdf", null,
            MediaItemType.Document, "Document Title", null, null, null, null, null);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithYearOutOfRange_Fails()
    {
        // Arrange
        var request = new CreateMediaItemRequest(
            "https://example.com/file.jpg", null,
            MediaItemType.Audio, "Title", null, 1960, null, null, null);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Year");
    }

    [Fact]
    public void Validate_WithContextOver50Chars_Fails()
    {
        // Arrange
        var longContext = new string('A', 51);
        var request = new CreateMediaItemRequest(
            "https://example.com/file.jpg", null,
            MediaItemType.Audio, "Title", null, null, null, null, longContext);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Context");
    }
}
