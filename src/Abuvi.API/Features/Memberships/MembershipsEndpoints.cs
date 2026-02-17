using Microsoft.AspNetCore.Mvc;
using Abuvi.API.Common.Models;
using Abuvi.API.Common.Filters;

namespace Abuvi.API.Features.Memberships;

public static class MembershipsEndpoints
{
    public static void MapMembershipsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/family-units/{familyUnitId:guid}/members/{memberId:guid}/membership")
            .WithTags("Memberships")
            .RequireAuthorization(); // All require authentication

        group.MapPost("/", CreateMembership)
            .WithName("CreateMembership")
            .AddEndpointFilter<ValidationFilter<CreateMembershipRequest>>()
            .Produces<ApiResponse<MembershipResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapGet("/", GetMembership)
            .WithName("GetMembership")
            .Produces<ApiResponse<MembershipResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/", DeactivateMembership)
            .WithName("DeactivateMembership")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateMembership(
        [FromRoute] Guid familyUnitId,
        [FromRoute] Guid memberId,
        [FromBody] CreateMembershipRequest request,
        MembershipsService service,
        CancellationToken ct)
    {
        // TODO: Add authorization check (Representative of family or Admin/Board)

        var membership = await service.CreateAsync(memberId, request, ct);
        return Results.Created(
            $"/api/family-units/{familyUnitId}/members/{memberId}/membership",
            ApiResponse<MembershipResponse>.Ok(membership));
    }

    private static async Task<IResult> GetMembership(
        [FromRoute] Guid familyUnitId,
        [FromRoute] Guid memberId,
        MembershipsService service,
        CancellationToken ct)
    {
        // TODO: Add authorization check

        var membership = await service.GetByFamilyMemberIdAsync(memberId, ct);
        return Results.Ok(ApiResponse<MembershipResponse>.Ok(membership));
    }

    private static async Task<IResult> DeactivateMembership(
        [FromRoute] Guid familyUnitId,
        [FromRoute] Guid memberId,
        MembershipsService service,
        CancellationToken ct)
    {
        // TODO: Add authorization check

        await service.DeactivateAsync(memberId, ct);
        return Results.NoContent();
    }
}
