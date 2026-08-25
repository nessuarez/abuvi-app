using System.Security.Claims;
using Abuvi.API.Common.Extensions;
using Abuvi.API.Common.Filters;
using Abuvi.API.Common.Models;
using Abuvi.API.Features.MediaItems;
using Microsoft.AspNetCore.Mvc;

namespace Abuvi.API.Features.MediaThemes;

public static class MediaThemesEndpoints
{
    public static void MapMediaThemesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/media-themes")
            .WithTags("MediaThemes")
            .RequireAuthorization();

        group.MapGet("/", GetCatalogue)
            .WithName("ListMediaThemes")
            .Produces<ApiResponse<IReadOnlyList<MediaThemeSummaryResponse>>>();

        group.MapGet("/{slug}/items", GetThemeItems)
            .WithName("GetMediaThemeItems")
            .Produces<ApiResponse<ThemeItemsResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        // Only Admin/Board create themes: a catalogue anyone can extend degrades into
        // synonyms within a season. Members attach the themes that exist.
        group.MapPost("/", CreateTheme)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .AddEndpointFilter<ValidationFilter<CreateMediaThemeRequest>>()
            .WithName("CreateMediaTheme")
            .Produces<ApiResponse<MediaThemeSummaryResponse>>(StatusCodes.Status201Created);

        group.MapPut("/{id:guid}", UpdateTheme)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .AddEndpointFilter<ValidationFilter<UpdateMediaThemeRequest>>()
            .WithName("UpdateMediaTheme")
            .Produces<ApiResponse<MediaThemeSummaryResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteTheme)
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .WithName("DeleteMediaTheme")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetCatalogue(
        ClaimsPrincipal user,
        MediaThemesService service,
        CancellationToken ct,
        [FromQuery] bool includeInactive = false)
    {
        // Inactive themes are only of interest to the people who can reactivate them.
        var themes = await service.GetCatalogueAsync(
            includeInactive && user.IsAdminOrBoard(), ct);

        return Results.Ok(ApiResponse<IReadOnlyList<MediaThemeSummaryResponse>>.Ok(themes));
    }

    private static async Task<IResult> GetThemeItems(
        string slug,
        ClaimsPrincipal user,
        MediaThemesService service,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AlbumsService.DefaultPageSize,
        [FromQuery] int? year = null,
        [FromQuery] Guid? campEditionId = null,
        [FromQuery] bool undatedOnly = false,
        [FromQuery] MediaItemType? type = null)
    {
        var result = await service.GetItemsBySlugAsync(
            slug,
            AlbumsService.ClampPage(page),
            AlbumsService.ClampPageSize(pageSize),
            year, campEditionId, undatedOnly, type,
            user.IsAdminOrBoard(), ct);

        return Results.Ok(ApiResponse<ThemeItemsResponse>.Ok(result));
    }

    private static async Task<IResult> CreateTheme(
        [FromBody] CreateMediaThemeRequest request,
        MediaThemesService service,
        CancellationToken ct)
    {
        var theme = await service.CreateAsync(request, ct);
        return Results.Created(
            $"/api/media-themes/{theme.Slug}/items",
            ApiResponse<MediaThemeSummaryResponse>.Ok(theme));
    }

    private static async Task<IResult> UpdateTheme(
        Guid id,
        [FromBody] UpdateMediaThemeRequest request,
        MediaThemesService service,
        CancellationToken ct)
    {
        var theme = await service.UpdateAsync(id, request, ct);
        return Results.Ok(ApiResponse<MediaThemeSummaryResponse>.Ok(theme));
    }

    private static async Task<IResult> DeleteTheme(
        Guid id,
        MediaThemesService service,
        CancellationToken ct,
        [FromQuery] bool force = false)
    {
        await service.DeleteAsync(id, force, ct);
        return Results.NoContent();
    }
}
