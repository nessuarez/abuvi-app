using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.BlobStorage;
using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Abuvi.API.Features.MediaItems;

public class MediaItemsService(
    IMediaItemsRepository repository,
    IBlobStorageService blobStorageService,
    IOptions<BlobStorageOptions> blobOptions,
    ILogger<MediaItemsService> logger)
{
    private readonly string _publicBaseUrl = blobOptions.Value.PublicBaseUrl;
    private const int MaxMediaItemsPerOwner = 10;

    public async Task<MediaItemResponse> CreateAsync(
        Guid userId,
        CreateMediaItemRequest request,
        CancellationToken ct)
    {
        var isInternalMedia = request.AccommodationId.HasValue || request.ZoneId.HasValue;
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
            AccommodationId = request.AccommodationId,
            ZoneId = request.ZoneId,
            Context = request.Context,
            IsApproved = isInternalMedia,
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
        Guid? accommodationId,
        Guid? zoneId,
        CancellationToken ct)
    {
        var items = await repository.GetListAsync(year, approved, context, type, accommodationId, zoneId, ct);
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

    // ── Accommodation media ──────────────────────────────────────────────────

    public async Task<IReadOnlyList<MediaItemResponse>> GetAccommodationMediaAsync(
        Guid accommodationId,
        CancellationToken ct)
    {
        var items = await repository.GetByAccommodationIdAsync(accommodationId, ct);
        return items.Select(m => m.ToResponse()).ToList();
    }

    public async Task<MediaItemResponse> AddToAccommodationAsync(
        Guid userId,
        Guid accommodationId,
        AddAccommodationMediaRequest request,
        CancellationToken ct)
    {
        var count = await repository.CountByAccommodationAsync(accommodationId, ct);
        if (count >= MaxMediaItemsPerOwner)
            throw new BusinessRuleException(
                $"No se pueden añadir más de {MaxMediaItemsPerOwner} elementos multimedia por alojamiento");

        var isFirstItem = count == 0;
        var mediaItem = new MediaItem
        {
            Id = Guid.NewGuid(),
            UploadedByUserId = userId,
            FileUrl = request.FileUrl,
            ThumbnailUrl = request.ThumbnailUrl,
            Type = MediaItemType.Photo,
            Title = string.Empty,
            Description = request.Description,
            AccommodationId = accommodationId,
            DisplayOrder = request.DisplayOrder,
            IsPrimary = isFirstItem,
            IsApproved = true,
            IsPublished = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(mediaItem, ct);
        return mediaItem.ToResponse();
    }

    public async Task<MediaItemResponse> SetPrimaryForAccommodationAsync(
        Guid accommodationId,
        Guid mediaId,
        CancellationToken ct)
    {
        var item = await repository.GetByIdAsync(mediaId, ct)
            ?? throw new NotFoundException(nameof(MediaItem), mediaId);

        if (item.AccommodationId != accommodationId)
            throw new NotFoundException(nameof(MediaItem), mediaId);

        await repository.ClearPrimaryForAccommodationAsync(accommodationId, ct);

        item.IsPrimary = true;
        item.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(item, ct);

        return item.ToResponse();
    }

    public async Task DeleteAccommodationMediaAsync(Guid accommodationId, Guid mediaId, CancellationToken ct)
    {
        var item = await repository.GetByIdAsync(mediaId, ct)
            ?? throw new NotFoundException(nameof(MediaItem), mediaId);

        if (item.AccommodationId != accommodationId)
            throw new NotFoundException(nameof(MediaItem), mediaId);

        await repository.DeleteAsync(item, ct);
    }

    // ── Zone media ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<MediaItemResponse>> GetZoneMediaAsync(
        Guid zoneId,
        CancellationToken ct)
    {
        var items = await repository.GetByZoneIdAsync(zoneId, ct);
        return items.Select(m => m.ToResponse()).ToList();
    }

    public async Task<MediaItemResponse> AddToZoneAsync(
        Guid userId,
        Guid zoneId,
        AddAccommodationMediaRequest request,
        CancellationToken ct)
    {
        var count = await repository.CountByZoneAsync(zoneId, ct);
        if (count >= MaxMediaItemsPerOwner)
            throw new BusinessRuleException(
                $"No se pueden añadir más de {MaxMediaItemsPerOwner} elementos multimedia por zona");

        var isFirstItem = count == 0;
        var mediaItem = new MediaItem
        {
            Id = Guid.NewGuid(),
            UploadedByUserId = userId,
            FileUrl = request.FileUrl,
            ThumbnailUrl = request.ThumbnailUrl,
            Type = MediaItemType.Photo,
            Title = string.Empty,
            Description = request.Description,
            ZoneId = zoneId,
            DisplayOrder = request.DisplayOrder,
            IsPrimary = isFirstItem,
            IsApproved = true,
            IsPublished = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(mediaItem, ct);
        return mediaItem.ToResponse();
    }

    public async Task<MediaItemResponse> SetPrimaryForZoneAsync(
        Guid zoneId,
        Guid mediaId,
        CancellationToken ct)
    {
        var item = await repository.GetByIdAsync(mediaId, ct)
            ?? throw new NotFoundException(nameof(MediaItem), mediaId);

        if (item.ZoneId != zoneId)
            throw new NotFoundException(nameof(MediaItem), mediaId);

        await repository.ClearPrimaryForZoneAsync(zoneId, ct);

        item.IsPrimary = true;
        item.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(item, ct);

        return item.ToResponse();
    }

    public async Task DeleteZoneMediaAsync(Guid zoneId, Guid mediaId, CancellationToken ct)
    {
        var item = await repository.GetByIdAsync(mediaId, ct)
            ?? throw new NotFoundException(nameof(MediaItem), mediaId);

        if (item.ZoneId != zoneId)
            throw new NotFoundException(nameof(MediaItem), mediaId);

        await repository.DeleteAsync(item, ct);
    }

    private string ExtractBlobKey(string fileUrl)
    {
        return fileUrl.Replace(_publicBaseUrl, "").TrimStart('/');
    }
}
