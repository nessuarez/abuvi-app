using System.Security.Claims;
using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Extensions;
using Abuvi.API.Common.Filters;
using Abuvi.API.Common.Models;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.MediaThemes;
using Microsoft.AspNetCore.Mvc;

namespace Abuvi.API.Features.MediaItems;

public static class AlbumsEndpoints
{
    public static void MapAlbumsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/camp-editions")
            .WithTags("Albums")
            .RequireAuthorization();

        group.MapGet("/albums", GetAlbumIndex)
            .WithName("GetAlbumIndex")
            .Produces<ApiResponse<IReadOnlyList<AlbumSummaryResponse>>>();

        group.MapGet("/{editionId:guid}/album", GetAlbum)
            .WithName("GetAlbum")
            .Produces<ApiResponse<AlbumDetailResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        var itemsGroup = app.MapGroup("/api/media-items")
            .WithTags("Albums")
            .RequireAuthorization();

        itemsGroup.MapGet("/unplaced", GetUnplaced)
            .WithName("GetUnplacedMedia")
            .Produces<ApiResponse<UnplacedMediaResponse>>();

        itemsGroup.MapPatch("/{id:guid}/edition", SetEdition)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .WithName("SetMediaItemEdition")
            .Produces<ApiResponse<AlbumMediaItemResponse>>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        itemsGroup.MapPatch("/{id:guid}/source", SetSource)
            .WithName("SetMediaItemSource")
            .Produces<ApiResponse<AlbumMediaItemResponse>>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // Theme tagging is addressed per item: any member may attach an existing theme.
        itemsGroup.MapPost("/{id:guid}/themes", AttachTheme)
            .AddEndpointFilter<ValidationFilter<AttachThemeRequest>>()
            .WithName("AttachThemeToMediaItem")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        itemsGroup.MapDelete("/{id:guid}/themes/{themeId:guid}", DetachTheme)
            .WithName("DetachThemeFromMediaItem")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetAlbumIndex(
        ClaimsPrincipal user, AlbumsService service, CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var albums = await service.GetIndexAsync(userId, ct);
        return Results.Ok(ApiResponse<IReadOnlyList<AlbumSummaryResponse>>.Ok(albums));
    }

    private static async Task<IResult> GetAlbum(
        Guid editionId,
        ClaimsPrincipal user,
        AlbumsService service,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AlbumsService.DefaultPageSize,
        [FromQuery] MediaItemType? type = null,
        [FromQuery] Guid? themeId = null)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var album = await service.GetAlbumAsync(
            editionId, page, pageSize, type, themeId, userId, user.IsAdminOrBoard(), ct);

        return Results.Ok(ApiResponse<AlbumDetailResponse>.Ok(album));
    }

    private static async Task<IResult> GetUnplaced(
        ClaimsPrincipal user,
        AlbumsService service,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AlbumsService.DefaultPageSize,
        [FromQuery] MediaItemType? type = null,
        [FromQuery] Guid? mediaSourceId = null,
        [FromQuery] bool suggestedForMe = false)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var result = await service.GetUnplacedAsync(
            page, pageSize, type, mediaSourceId, suggestedForMe, userId, user.IsAdminOrBoard(), ct);

        return Results.Ok(ApiResponse<UnplacedMediaResponse>.Ok(result));
    }

    private static async Task<IResult> SetEdition(
        Guid id,
        [FromBody] SetMediaItemEditionRequest request,
        ClaimsPrincipal user,
        IMediaItemsRepository repository,
        ICampEditionsRepository editionsRepository,
        AlbumsService albumsService,
        CancellationToken ct)
    {
        var item = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("elemento multimedia", id);

        if (request.CampEditionId is { } editionId)
        {
            var edition = await editionsRepository.GetByIdAsync(editionId, ct)
                ?? throw new NotFoundException("edición", editionId);

            item.CampEditionId = edition.Id;
            item.Year = edition.Year;
            item.Decade = MediaItemMappingExtensions.DeriveDecade(edition.Year);
        }
        else
        {
            // Explicitly returning an item to the unplaced pile is a legitimate correction.
            item.CampEditionId = null;
            item.Year = null;
            item.Decade = null;
        }

        // A moderator's placement is final: it must not be overwritten by consensus later.
        item.YearSource = MediaItemYearSource.Admin;
        item.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(item, ct);

        var mapped = await albumsService.MapItemsAsync([item], user.IsAdminOrBoard(), ct);
        return Results.Ok(ApiResponse<AlbumMediaItemResponse>.Ok(mapped[0]));
    }

    private static async Task<IResult> SetSource(
        Guid id,
        [FromBody] SetMediaItemSourceRequest request,
        ClaimsPrincipal user,
        IMediaItemsRepository repository,
        AlbumsService albumsService,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var item = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("elemento multimedia", id);

        // The uploader can correct provenance on their own upload; moderators on anything.
        if (!user.IsAdminOrBoard() && item.UploadedByUserId != userId)
            return Results.Forbid();

        item.MediaSourceId = request.MediaSourceId;
        item.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(item, ct);

        var mapped = await albumsService.MapItemsAsync([item], user.IsAdminOrBoard(), ct);
        return Results.Ok(ApiResponse<AlbumMediaItemResponse>.Ok(mapped[0]));
    }

    private static async Task<IResult> AttachTheme(
        Guid id,
        [FromBody] AttachThemeRequest request,
        ClaimsPrincipal user,
        MediaThemesService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        await service.AttachAsync(id, request.ThemeId, userId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> DetachTheme(
        Guid id,
        Guid themeId,
        ClaimsPrincipal user,
        MediaThemesService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var detached = await service.DetachAsync(id, themeId, userId, user.IsAdminOrBoard(), ct);
        return detached ? Results.NoContent() : Results.Forbid();
    }
}
