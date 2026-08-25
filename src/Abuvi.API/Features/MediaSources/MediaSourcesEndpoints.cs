using System.Security.Claims;
using Abuvi.API.Common.Extensions;
using Abuvi.API.Common.Filters;
using Abuvi.API.Common.Models;
using Abuvi.API.Features.MediaItems;
using Microsoft.AspNetCore.Mvc;

namespace Abuvi.API.Features.MediaSources;

public static class MediaSourcesEndpoints
{
    public static void MapMediaSourcesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/media-sources")
            .WithTags("MediaSources")
            .RequireAuthorization();

        group.MapGet("/", ListSources)
            .WithName("ListMediaSources")
            .Produces<ApiResponse<IReadOnlyList<MediaSourceResponse>>>();

        group.MapGet("/{id:guid}", GetSource)
            .WithName("GetMediaSource")
            .Produces<ApiResponse<MediaSourceResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/items", GetSourceItems)
            .WithName("GetMediaSourceItems")
            .Produces<ApiResponse<UnplacedMediaResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        // Any member may register a contributor — any member could be the one collecting
        // a neighbour's shoebox of photos.
        group.MapPost("/", CreateSource)
            .AddEndpointFilter<ValidationFilter<CreateMediaSourceRequest>>()
            .WithName("CreateMediaSource")
            .Produces<ApiResponse<MediaSourceResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}", UpdateSource)
            .AddEndpointFilter<ValidationFilter<UpdateMediaSourceRequest>>()
            .WithName("UpdateMediaSource")
            .Produces<ApiResponse<MediaSourceResponse>>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/merge", MergeSource)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .AddEndpointFilter<ValidationFilter<MergeMediaSourceRequest>>()
            .WithName("MergeMediaSource")
            .Produces<ApiResponse<object>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/anonymise", AnonymiseSource)
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .WithName("AnonymiseMediaSource")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteSource)
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .WithName("DeleteMediaSource")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ListSources(
        ClaimsPrincipal user, MediaSourcesService service, CancellationToken ct)
    {
        var sources = await service.GetListAsync(user.IsAdminOrBoard(), ct);
        return Results.Ok(ApiResponse<IReadOnlyList<MediaSourceResponse>>.Ok(sources));
    }

    private static async Task<IResult> GetSource(
        Guid id, ClaimsPrincipal user, MediaSourcesService service, CancellationToken ct)
    {
        var source = await service.GetByIdAsync(id, user.IsAdminOrBoard(), ct);
        return Results.Ok(ApiResponse<MediaSourceResponse>.Ok(source));
    }

    private static async Task<IResult> GetSourceItems(
        Guid id,
        ClaimsPrincipal user,
        MediaSourcesService service,
        AlbumsService albumsService,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AlbumsService.DefaultPageSize)
    {
        var isAdminOrBoard = user.IsAdminOrBoard();
        page = AlbumsService.ClampPage(page);
        pageSize = AlbumsService.ClampPageSize(pageSize);

        var (items, total) = await service.GetItemsAsync(id, page, pageSize, ct);
        var mapped = await albumsService.MapItemsAsync(items, isAdminOrBoard, ct);

        return Results.Ok(ApiResponse<UnplacedMediaResponse>.Ok(
            new UnplacedMediaResponse(mapped, total, page, pageSize)));
    }

    private static async Task<IResult> CreateSource(
        [FromBody] CreateMediaSourceRequest request,
        ClaimsPrincipal user,
        MediaSourcesService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var source = await service.CreateAsync(userId, request, ct);
        return Results.Created(
            $"/api/media-sources/{source.Id}",
            ApiResponse<MediaSourceResponse>.Ok(source));
    }

    private static async Task<IResult> UpdateSource(
        Guid id,
        [FromBody] UpdateMediaSourceRequest request,
        ClaimsPrincipal user,
        MediaSourcesService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var updated = await service.UpdateAsync(id, userId, user.IsAdminOrBoard(), request, ct);

        // Only Admin/Board or whoever registered the source may edit it.
        return updated is null
            ? Results.Forbid()
            : Results.Ok(ApiResponse<MediaSourceResponse>.Ok(updated));
    }

    private static async Task<IResult> MergeSource(
        Guid id,
        [FromBody] MergeMediaSourceRequest request,
        MediaSourcesService service,
        CancellationToken ct)
    {
        var moved = await service.MergeAsync(id, request.TargetId, ct);
        return Results.Ok(ApiResponse<object>.Ok(new { movedItems = moved }));
    }

    private static async Task<IResult> AnonymiseSource(
        Guid id, MediaSourcesService service, CancellationToken ct)
    {
        await service.AnonymiseAsync(id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteSource(
        Guid id, MediaSourcesService service, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return Results.NoContent();
    }
}
