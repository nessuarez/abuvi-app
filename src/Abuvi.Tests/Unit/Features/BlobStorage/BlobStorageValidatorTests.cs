namespace Abuvi.Tests.Unit.Features.BlobStorage;

using Abuvi.API.Features.BlobStorage;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

public class BlobStorageValidatorTests
{
    private static readonly BlobStorageOptions DefaultOptions = new()
    {
        MaxFileSizeBytes = 52_428_800,
        AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".webp"],
        AllowedVideoExtensions = [".mp4", ".mov"],
        AllowedAudioExtensions = [".mp3", ".wav"],
        AllowedDocumentExtensions = [".pdf", ".doc", ".docx"],
    };

    private static UploadBlobRequestValidator CreateSut(BlobStorageOptions? options = null) =>
        new(Options.Create(options ?? DefaultOptions));

    private static IFormFile CreateMockFile(string fileName, long sizeBytes = 1024)
    {
        var file = Substitute.For<IFormFile>();
        file.FileName.Returns(fileName);
        file.Length.Returns(sizeBytes);
        return file;
    }

    [Fact]
    public void Validate_WhenFileIsNull_Fails()
    {
        var sut = CreateSut();
        var request = new UploadBlobRequest { Folder = "photos" };
        sut.TestValidate(request).ShouldHaveValidationErrorFor(x => x.File);
    }

    [Fact]
    public void Validate_WhenFileExceedsMaxSize_Fails()
    {
        var sut = CreateSut();
        var request = new UploadBlobRequest
        {
            File = CreateMockFile("big.jpg", DefaultOptions.MaxFileSizeBytes + 1),
            Folder = "photos"
        };
        sut.TestValidate(request).ShouldHaveValidationErrorFor(x => x.File);
    }

    [Fact]
    public void Validate_WhenFolderIsEmpty_Fails()
    {
        var sut = CreateSut();
        var request = new UploadBlobRequest { File = CreateMockFile("photo.jpg"), Folder = "" };
        sut.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Folder);
    }

    [Theory]
    [InlineData("invalid-folder")]
    [InlineData("profile-photos")]
    [InlineData("../../etc")]
    public void Validate_WhenFolderNotInAllowedList_Fails(string folder)
    {
        var sut = CreateSut();
        var request = new UploadBlobRequest { File = CreateMockFile("photo.jpg"), Folder = folder };
        sut.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Folder);
    }

    [Fact]
    public void Validate_WhenPdfUploadedToPhotosFolder_Fails()
    {
        var sut = CreateSut();
        var request = new UploadBlobRequest { File = CreateMockFile("doc.pdf"), Folder = "photos" };
        sut.TestValidate(request).ShouldHaveValidationErrorFor(x => x.File);
    }

    [Fact]
    public void Validate_WhenAudioUploadedToPhotosFolder_Fails()
    {
        var sut = CreateSut();
        var request = new UploadBlobRequest { File = CreateMockFile("audio.mp3"), Folder = "photos" };
        sut.TestValidate(request).ShouldHaveValidationErrorFor(x => x.File);
    }

    [Fact]
    public void Validate_WhenValidImageInPhotosFolder_Passes()
    {
        var sut = CreateSut();
        var request = new UploadBlobRequest { File = CreateMockFile("photo.jpg"), Folder = "photos" };
        sut.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(".mp3")]
    [InlineData(".wav")]
    public void Validate_WhenValidAudioInMediaItemsFolder_Passes(string ext)
    {
        var sut = CreateSut();
        var request = new UploadBlobRequest
        {
            File = CreateMockFile($"recording{ext}"),
            Folder = "media-items"
        };
        sut.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenValidDocumentInMediaItemsFolder_Passes()
    {
        var sut = CreateSut();
        var request = new UploadBlobRequest { File = CreateMockFile("report.pdf"), Folder = "media-items" };
        sut.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("camp-locations")]
    [InlineData("camp-photos")]
    public void Validate_WhenValidImageInNonMediaFolder_Passes(string folder)
    {
        var sut = CreateSut();
        var request = new UploadBlobRequest { File = CreateMockFile("photo.webp"), Folder = folder };
        sut.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }
}
