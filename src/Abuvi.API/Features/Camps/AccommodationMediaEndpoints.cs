using System.Security.Claims;
using Abuvi.API.Common.Extensions;
using Abuvi.API.Common.Models;
using Abuvi.API.Features.MediaItems;

namespace Abuvi.API.Features.Camps;

public static class AccommodationMediaEndpoints
{
    public static void MapAccommodationMediaEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Zone media ───────────────────────────────────────────────────────

        var zoneMedia = app.MapGroup(
                "/api/camps/editions/{editionId:guid}/accommodation-zones/{zoneId:guid}/media")
            .WithTags("AccommodationMedia")
            .RequireAuthorization();

        zoneMedia.MapGet("/", GetZoneMedia)
            .WithName("GetZoneMedia")
            .Produces<ApiResponse<IReadOnlyList<MediaItemResponse>>>();

        zoneMedia.MapPost("/", AddZoneMedia)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .WithName("AddZoneMedia")
            .Produces<ApiResponse<MediaItemResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status422UnprocessableEntity);

        zoneMedia.MapDelete("/{mediaId:guid}", DeleteZoneMedia)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .WithName("DeleteZoneMedia")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        zoneMedia.MapPatch("/{mediaId:guid}/primary", SetZoneMediaPrimary)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .WithName("SetZoneMediaPrimary")
            .Produces<ApiResponse<MediaItemResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        // ── Accommodation media ──────────────────────────────────────────────

        var accommodationMedia = app.MapGroup(
                "/api/camps/editions/{editionId:guid}/accommodations/{accommodationId:guid}/media")
            .WithTags("AccommodationMedia")
            .RequireAuthorization();

        accommodationMedia.MapGet("/", GetAccommodationMedia)
            .WithName("GetAccommodationMedia")
            .Produces<ApiResponse<IReadOnlyList<MediaItemResponse>>>();

        accommodationMedia.MapPost("/", AddAccommodationMedia)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .WithName("AddAccommodationMedia")
            .Produces<ApiResponse<MediaItemResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status422UnprocessableEntity);

        accommodationMedia.MapDelete("/{mediaId:guid}", DeleteAccommodationMedia)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .WithName("DeleteAccommodationMedia")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        accommodationMedia.MapPatch("/{mediaId:guid}/primary", SetAccommodationMediaPrimary)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .WithName("SetAccommodationMediaPrimary")
            .Produces<ApiResponse<MediaItemResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        // ── Accommodation type defaults ──────────────────────────────────────

        var typeMedia = app.MapGroup("/api/accommodation-types")
            .WithTags("AccommodationMedia")
            .RequireAuthorization();

        typeMedia.MapGet("/media", GetAllTypeMedia)
            .WithName("GetAllAccommodationTypeMedia")
            .Produces<ApiResponse<IReadOnlyList<AccommodationTypeMediaResponse>>>();

        typeMedia.MapGet("/{type}/media", GetTypeMedia)
            .WithName("GetAccommodationTypeMedia")
            .Produces<ApiResponse<IReadOnlyList<AccommodationTypeMediaResponse>>>()
            .Produces(StatusCodes.Status400BadRequest);

        typeMedia.MapPost("/{type}/media", AddTypeMedia)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .WithName("AddAccommodationTypeMedia")
            .Produces<ApiResponse<AccommodationTypeMediaResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status422UnprocessableEntity);

        typeMedia.MapDelete("/media/{mediaId:guid}", DeleteTypeMedia)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .WithName("DeleteAccommodationTypeMedia")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        typeMedia.MapPatch("/media/{mediaId:guid}/primary", SetTypeMediaPrimary)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .WithName("SetAccommodationTypeMediaPrimary")
            .Produces<ApiResponse<AccommodationTypeMediaResponse>>()
            .Produces(StatusCodes.Status404NotFound);
    }

    // ── Zone media handlers ──────────────────────────────────────────────────

    private static async Task<IResult> GetZoneMedia(
        Guid editionId,
        Guid zoneId,
        MediaItemsService service,
        CancellationToken ct)
    {
        var items = await service.GetZoneMediaAsync(zoneId, ct);
        return Results.Ok(ApiResponse<IReadOnlyList<MediaItemResponse>>.Ok(items));
    }

    private static async Task<IResult> AddZoneMedia(
        Guid editionId,
        Guid zoneId,
        AddAccommodationMediaRequest request,
        ClaimsPrincipal user,
        MediaItemsService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var item = await service.AddToZoneAsync(userId, zoneId, request, ct);
        return Results.Created(
            $"/api/camps/editions/{editionId}/accommodation-zones/{zoneId}/media/{item.Id}",
            ApiResponse<MediaItemResponse>.Ok(item));
    }

    private static async Task<IResult> DeleteZoneMedia(
        Guid editionId,
        Guid zoneId,
        Guid mediaId,
        MediaItemsService service,
        CancellationToken ct)
    {
        await service.DeleteZoneMediaAsync(zoneId, mediaId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SetZoneMediaPrimary(
        Guid editionId,
        Guid zoneId,
        Guid mediaId,
        MediaItemsService service,
        CancellationToken ct)
    {
        var item = await service.SetPrimaryForZoneAsync(zoneId, mediaId, ct);
        return Results.Ok(ApiResponse<MediaItemResponse>.Ok(item));
    }

    // ── Accommodation media handlers ─────────────────────────────────────────

    private static async Task<IResult> GetAccommodationMedia(
        Guid editionId,
        Guid accommodationId,
        MediaItemsService service,
        CancellationToken ct)
    {
        var items = await service.GetAccommodationMediaAsync(accommodationId, ct);
        return Results.Ok(ApiResponse<IReadOnlyList<MediaItemResponse>>.Ok(items));
    }

    private static async Task<IResult> AddAccommodationMedia(
        Guid editionId,
        Guid accommodationId,
        AddAccommodationMediaRequest request,
        ClaimsPrincipal user,
        MediaItemsService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var item = await service.AddToAccommodationAsync(userId, accommodationId, request, ct);
        return Results.Created(
            $"/api/camps/editions/{editionId}/accommodations/{accommodationId}/media/{item.Id}",
            ApiResponse<MediaItemResponse>.Ok(item));
    }

    private static async Task<IResult> DeleteAccommodationMedia(
        Guid editionId,
        Guid accommodationId,
        Guid mediaId,
        MediaItemsService service,
        CancellationToken ct)
    {
        await service.DeleteAccommodationMediaAsync(accommodationId, mediaId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SetAccommodationMediaPrimary(
        Guid editionId,
        Guid accommodationId,
        Guid mediaId,
        MediaItemsService service,
        CancellationToken ct)
    {
        var item = await service.SetPrimaryForAccommodationAsync(accommodationId, mediaId, ct);
        return Results.Ok(ApiResponse<MediaItemResponse>.Ok(item));
    }

    // ── Type media handlers ──────────────────────────────────────────────────

    private static async Task<IResult> GetAllTypeMedia(
        AccommodationTypeMediaService service,
        CancellationToken ct)
    {
        var items = await service.GetAllAsync(ct);
        return Results.Ok(ApiResponse<IReadOnlyList<AccommodationTypeMediaResponse>>.Ok(items));
    }

    private static async Task<IResult> GetTypeMedia(
        string type,
        AccommodationTypeMediaService service,
        CancellationToken ct)
    {
        if (!Enum.TryParse<AccommodationType>(type, ignoreCase: true, out var accommodationType))
            return Results.BadRequest(ApiResponse<object>.Fail($"Tipo de alojamiento inválido: {type}", "INVALID_TYPE"));

        var items = await service.GetByTypeAsync(accommodationType, ct);
        return Results.Ok(ApiResponse<IReadOnlyList<AccommodationTypeMediaResponse>>.Ok(items));
    }

    private static async Task<IResult> AddTypeMedia(
        string type,
        AddAccommodationMediaRequest request,
        ClaimsPrincipal user,
        AccommodationTypeMediaService service,
        CancellationToken ct)
    {
        if (!Enum.TryParse<AccommodationType>(type, ignoreCase: true, out var accommodationType))
            return Results.BadRequest(ApiResponse<object>.Fail($"Tipo de alojamiento inválido: {type}", "INVALID_TYPE"));

        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var item = await service.AddAsync(userId, accommodationType, request, ct);
        return Results.Created(
            $"/api/accommodation-types/{type}/media/{item.Id}",
            ApiResponse<AccommodationTypeMediaResponse>.Ok(item));
    }

    private static async Task<IResult> DeleteTypeMedia(
        Guid mediaId,
        AccommodationTypeMediaService service,
        CancellationToken ct)
    {
        await service.DeleteAsync(mediaId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SetTypeMediaPrimary(
        Guid mediaId,
        AccommodationTypeMediaService service,
        CancellationToken ct)
    {
        var item = await service.SetPrimaryAsync(mediaId, ct);
        return Results.Ok(ApiResponse<AccommodationTypeMediaResponse>.Ok(item));
    }
}
