using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.BlobStorage;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.MediaSources;
using Abuvi.API.Features.MediaThemes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Abuvi.API.Features.MediaItems;

public class MediaItemsService(
    IMediaItemsRepository repository,
    ICampEditionsRepository campEditionsRepository,
    IMediaSourcesRepository mediaSourcesRepository,
    IMediaThemesRepository themesRepository,
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

        var sourceId = await ResolveSourceAsync(userId, request, ct);
        var (editionId, year, yearSource) = await ResolvePlacementAsync(request, ct);

        var mediaItem = new MediaItem
        {
            Id = Guid.NewGuid(),
            UploadedByUserId = userId,
            FileUrl = request.FileUrl,
            ThumbnailUrl = request.ThumbnailUrl,
            Type = request.Type,
            Title = request.Title,
            Description = request.Description,
            Year = year,
            Decade = MediaItemMappingExtensions.DeriveDecade(year),
            MemoryId = request.MemoryId,
            CampLocationId = request.CampLocationId,
            AccommodationId = request.AccommodationId,
            ZoneId = request.ZoneId,
            Context = request.Context,
            CampEditionId = editionId,
            YearSource = yearSource,
            MediaSourceId = sourceId,
            SourcePath = request.SourcePath,
            IsApproved = isInternalMedia,
            IsPublished = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(mediaItem, ct);

        if (request.ThemeIds is { Count: > 0 })
            await AttachThemesIgnoringUnknownAsync(mediaItem.Id, request.ThemeIds, userId, ct);

        logger.LogInformation(
            "MediaItem {MediaItemId} of type {Type} created by user {UserId} " +
            "(edition {EditionId}, yearSource {YearSource})",
            mediaItem.Id, mediaItem.Type, userId, editionId, yearSource);

        return mediaItem.ToResponse();
    }

    /// <summary>
    /// Resolves the contributor for an upload. An existing source or an inline new one —
    /// never both. Neither means the uploader is the provider, which is the common case
    /// for a member uploading their own material.
    /// </summary>
    private async Task<Guid?> ResolveSourceAsync(
        Guid userId, CreateMediaItemRequest request, CancellationToken ct)
    {
        if (request.MediaSourceId is not null && request.NewSource is not null)
            throw new ValidationException(
                "Indica un aportante existente o crea uno nuevo, pero no ambos");

        if (request.NewSource is not { } ns)
            return request.MediaSourceId;

        var source = new MediaSource
        {
            Id = Guid.NewGuid(),
            ContributorName = ns.ContributorName.Trim(),
            ContributorUserId = ns.ContributorUserId,
            ContributorContact = ns.ContributorContact,
            Notes = ns.Notes,
            ReceivedAt = ns.ReceivedAt,
            RegisteredByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await mediaSourcesRepository.AddAsync(source, ct);
        return source.Id;
    }

    /// <summary>
    /// Attaches themes supplied at upload time. Unknown or inactive ids are skipped rather
    /// than failing the upload — losing a photo because a theme id went stale would be a
    /// bad trade.
    /// </summary>
    private async Task AttachThemesIgnoringUnknownAsync(
        Guid mediaItemId, IReadOnlyList<Guid> themeIds, Guid userId, CancellationToken ct)
    {
        var known = await themesRepository.GetByIdsAsync(themeIds.Distinct().ToList(), ct);

        var tags = known
            .Where(t => t.IsActive)
            .Select(t => new MediaItemTheme
            {
                MediaItemId = mediaItemId,
                MediaThemeId = t.Id,
                TaggedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        await themesRepository.AttachManyAsync(tags, ct);
    }

    /// <summary>
    /// Works out which edition an upload belongs to.
    ///
    /// Every branch here is a valid outcome — including the one where nothing resolves.
    /// An upload with no edition and no year is NOT an error: it lands in the unplaced
    /// pile and becomes eligible for collaborative dating, which is exactly the flow that
    /// fills the archive. No validation rule may require either field.
    /// </summary>
    private async Task<(Guid? EditionId, int? Year, MediaItemYearSource YearSource)>
        ResolvePlacementAsync(CreateMediaItemRequest request, CancellationToken ct)
    {
        if (request.CampEditionId is { } requestedEditionId)
        {
            var edition = await campEditionsRepository.GetByIdAsync(requestedEditionId, ct)
                ?? throw new NotFoundException("edición", requestedEditionId);

            return (edition.Id, request.Year ?? edition.Year, MediaItemYearSource.Uploader);
        }

        if (request.Year is { } requestedYear)
        {
            // Exactly one edition per historical year, so a year usually determines the
            // edition — but verify rather than assume. An ambiguous year stays unplaced.
            var candidates = await campEditionsRepository.GetByYearAsync(requestedYear, ct);
            var resolved = candidates.Count == 1 ? candidates[0].Id : (Guid?)null;

            return (resolved, requestedYear, MediaItemYearSource.Uploader);
        }

        return (null, null, MediaItemYearSource.Unknown);
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
        Guid? campEditionId,
        bool unplacedOnly,
        Guid? themeId,
        CancellationToken ct)
    {
        var items = await repository.GetListAsync(
            year, approved, context, type, accommodationId, zoneId,
            campEditionId, unplacedOnly, themeId, ct);

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
