using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Abuvi.API.Common.Models;
using Abuvi.API.Common.Filters;
using Abuvi.API.Common.Extensions;

namespace Abuvi.API.Features.MediaItems;

public static class MediaItemsEndpoints
{
    public static void MapMediaItemsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/media-items")
            .WithTags("MediaItems")
            .RequireAuthorization();

        group.MapPost("/", CreateMediaItem)
            .AddEndpointFilter<ValidationFilter<CreateMediaItemRequest>>()
            .WithName("CreateMediaItem")
            .Produces<ApiResponse<MediaItemResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/", ListMediaItems)
            .WithName("ListMediaItems")
            .Produces<ApiResponse<IReadOnlyList<MediaItemResponse>>>();

        group.MapGet("/{id:guid}", GetMediaItem)
            .WithName("GetMediaItem")
            .Produces<ApiResponse<MediaItemResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/approve", ApproveMediaItem)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .WithName("ApproveMediaItem")
            .Produces<ApiResponse<MediaItemResponse>>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/reject", RejectMediaItem)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .WithName("RejectMediaItem")
            .Produces<ApiResponse<MediaItemResponse>>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteMediaItem)
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .WithName("DeleteMediaItem")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateMediaItem(
        [FromBody] CreateMediaItemRequest request,
        ClaimsPrincipal user,
        MediaItemsService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var item = await service.CreateAsync(userId, request, ct);
        return Results.Created(
            $"/api/media-items/{item.Id}",
            ApiResponse<MediaItemResponse>.Ok(item));
    }

    private static async Task<IResult> ListMediaItems(
        [FromQuery] int? year,
        [FromQuery] bool? approved,
        [FromQuery] string? context,
        [FromQuery] MediaItemType? type,
        MediaItemsService service,
        CancellationToken ct)
    {
        var items = await service.GetListAsync(year, approved, context, type, ct);
        return Results.Ok(ApiResponse<IReadOnlyList<MediaItemResponse>>.Ok(items));
    }

    private static async Task<IResult> GetMediaItem(
        Guid id,
        MediaItemsService service,
        CancellationToken ct)
    {
        var item = await service.GetByIdAsync(id, ct);
        return Results.Ok(ApiResponse<MediaItemResponse>.Ok(item));
    }

    private static async Task<IResult> ApproveMediaItem(
        Guid id,
        MediaItemsService service,
        CancellationToken ct)
    {
        var item = await service.ApproveAsync(id, ct);
        return Results.Ok(ApiResponse<MediaItemResponse>.Ok(item));
    }

    private static async Task<IResult> RejectMediaItem(
        Guid id,
        MediaItemsService service,
        CancellationToken ct)
    {
        var item = await service.RejectAsync(id, ct);
        return Results.Ok(ApiResponse<MediaItemResponse>.Ok(item));
    }

    private static async Task<IResult> DeleteMediaItem(
        Guid id,
        MediaItemsService service,
        CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return Results.NoContent();
    }
}
