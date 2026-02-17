using Microsoft.AspNetCore.Mvc;
using Abuvi.API.Common.Models;
using Abuvi.API.Common.Filters;

namespace Abuvi.API.Features.Guests;

public static class GuestsEndpoints
{
    public static void MapGuestsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/family-units/{familyUnitId:guid}/guests")
            .WithTags("Guests")
            .RequireAuthorization();

        group.MapPost("/", CreateGuest)
            .WithName("CreateGuest")
            .AddEndpointFilter<ValidationFilter<CreateGuestRequest>>()
            .Produces<ApiResponse<GuestResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/", ListGuests)
            .WithName("ListGuests")
            .Produces<ApiResponse<IReadOnlyList<GuestResponse>>>();

        group.MapGet("/{guestId:guid}", GetGuest)
            .WithName("GetGuest")
            .Produces<ApiResponse<GuestResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{guestId:guid}", UpdateGuest)
            .WithName("UpdateGuest")
            .AddEndpointFilter<ValidationFilter<UpdateGuestRequest>>()
            .Produces<ApiResponse<GuestResponse>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{guestId:guid}", DeleteGuest)
            .WithName("DeleteGuest")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateGuest(
        [FromRoute] Guid familyUnitId,
        [FromBody] CreateGuestRequest request,
        GuestsService service,
        CancellationToken ct)
    {
        var guest = await service.CreateAsync(familyUnitId, request, ct);
        return Results.Created(
            $"/api/family-units/{familyUnitId}/guests/{guest.Id}",
            ApiResponse<GuestResponse>.Ok(guest));
    }

    private static async Task<IResult> ListGuests(
        [FromRoute] Guid familyUnitId,
        GuestsService service,
        CancellationToken ct)
    {
        var guests = await service.GetByFamilyUnitAsync(familyUnitId, ct);
        return Results.Ok(ApiResponse<IReadOnlyList<GuestResponse>>.Ok(guests));
    }

    private static async Task<IResult> GetGuest(
        [FromRoute] Guid familyUnitId,
        [FromRoute] Guid guestId,
        GuestsService service,
        CancellationToken ct)
    {
        var guest = await service.GetByIdAsync(guestId, ct);
        return Results.Ok(ApiResponse<GuestResponse>.Ok(guest));
    }

    private static async Task<IResult> UpdateGuest(
        [FromRoute] Guid familyUnitId,
        [FromRoute] Guid guestId,
        [FromBody] UpdateGuestRequest request,
        GuestsService service,
        CancellationToken ct)
    {
        var guest = await service.UpdateAsync(guestId, request, ct);
        return Results.Ok(ApiResponse<GuestResponse>.Ok(guest));
    }

    private static async Task<IResult> DeleteGuest(
        [FromRoute] Guid familyUnitId,
        [FromRoute] Guid guestId,
        GuestsService service,
        CancellationToken ct)
    {
        await service.DeleteAsync(guestId, ct);
        return Results.NoContent();
    }
}
