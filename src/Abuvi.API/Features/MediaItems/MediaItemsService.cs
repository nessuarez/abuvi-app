using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.BlobStorage;
using Microsoft.Extensions.Options;

namespace Abuvi.API.Features.MediaItems;

public class MediaItemsService(
    IMediaItemsRepository repository,
    IBlobStorageService blobStorageService,
    IOptions<BlobStorageOptions> blobOptions,
    ILogger<MediaItemsService> logger)
{
    private readonly string _publicBaseUrl = blobOptions.Value.PublicBaseUrl;

    public async Task<MediaItemResponse> CreateAsync(
        Guid userId,
        CreateMediaItemRequest request,
        CancellationToken ct)
    {
        var mediaItem = new MediaItem
        {
            Id = Guid.NewGuid(),
            UploadedByUserId = userId,
            FileUrl = request.FileUrl,
            ThumbnailUrl = request.ThumbnailUrl,
            Type = request.Type,
            Title = request.Title,
            Description = request.Description,
            Year = request.Year,
            Decade = MediaItemMappingExtensions.DeriveDecade(request.Year),
            MemoryId = request.MemoryId,
            CampLocationId = request.CampLocationId,
            Context = request.Context,
            IsApproved = false,
            IsPublished = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(mediaItem, ct);

        logger.LogInformation(
            "MediaItem {MediaItemId} of type {Type} created by user {UserId}",
            mediaItem.Id, mediaItem.Type, userId);

        return mediaItem.ToResponse();
    }

    public async Task<MediaItemResponse> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var item = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(MediaItem), id);

        return item.ToResponse();
    }

    public async Task<IReadOnlyList<MediaItemResponse>> GetListAsync(
        int? year,
        bool? approved,
        string? context,
        MediaItemType? type,
        CancellationToken ct)
    {
        var items = await repository.GetListAsync(year, approved, context, type, ct);
        return items.Select(m => m.ToResponse()).ToList();
    }

    public async Task<MediaItemResponse> ApproveAsync(Guid id, CancellationToken ct)
    {
        var item = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(MediaItem), id);

        item.IsApproved = true;
        item.IsPublished = true;
        item.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(item, ct);

        logger.LogInformation(
            "MediaItem {MediaItemId} approved",
            id);

        return item.ToResponse();
    }

    public async Task<MediaItemResponse> RejectAsync(Guid id, CancellationToken ct)
    {
        var item = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(MediaItem), id);

        item.IsApproved = false;
        item.IsPublished = false;
        item.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(item, ct);

        logger.LogInformation(
            "MediaItem {MediaItemId} rejected",
            id);

        return item.ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var item = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(MediaItem), id);

        // Delete blobs from storage
        var blobKeys = new List<string> { ExtractBlobKey(item.FileUrl) };
        if (!string.IsNullOrEmpty(item.ThumbnailUrl))
            blobKeys.Add(ExtractBlobKey(item.ThumbnailUrl));

        await blobStorageService.DeleteManyAsync(blobKeys, ct);

        // Delete from database
        await repository.DeleteAsync(item, ct);

        logger.LogInformation(
            "MediaItem {MediaItemId} deleted with {BlobCount} blob(s)",
            id, blobKeys.Count);
    }

    private string ExtractBlobKey(string fileUrl)
    {
        return fileUrl.Replace(_publicBaseUrl, "").TrimStart('/');
    }
}
