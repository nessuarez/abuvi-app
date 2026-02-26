namespace Abuvi.Tests.Helpers.Builders;

using Abuvi.API.Features.BlobStorage;

public class BlobUploadResultBuilder
{
    private string _fileUrl = "https://cdn.example.com/photos/abc/test.jpg";
    private string? _thumbnailUrl = null;
    private string _fileName = "test.jpg";
    private string _contentType = "image/jpeg";
    private long _sizeBytes = 1024;

    public BlobUploadResultBuilder WithFileUrl(string url) { _fileUrl = url; return this; }
    public BlobUploadResultBuilder WithThumbnailUrl(string? url) { _thumbnailUrl = url; return this; }
    public BlobUploadResultBuilder WithFileName(string name) { _fileName = name; return this; }
    public BlobUploadResultBuilder WithContentType(string type) { _contentType = type; return this; }
    public BlobUploadResultBuilder WithSizeBytes(long size) { _sizeBytes = size; return this; }

    public BlobUploadResultBuilder AsImageWithThumbnail()
    {
        _thumbnailUrl = "https://cdn.example.com/photos/abc/thumbs/test.webp";
        return this;
    }

    public BlobUploadResultBuilder AsAudio()
    {
        _fileUrl = "https://cdn.example.com/media-items/audio.mp3";
        _fileName = "audio.mp3";
        _contentType = "audio/mpeg";
        _thumbnailUrl = null;
        return this;
    }

    public BlobUploadResult Build() => new(
        FileUrl: _fileUrl,
        ThumbnailUrl: _thumbnailUrl,
        FileName: _fileName,
        ContentType: _contentType,
        SizeBytes: _sizeBytes);
}
