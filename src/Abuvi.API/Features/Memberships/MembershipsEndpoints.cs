using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Abuvi.API.Common.Models;
using Abuvi.API.Common.Filters;
using Abuvi.API.Common.Extensions;

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

        var bulkGroup = app.MapGroup("/api/family-units/{familyUnitId:guid}/membership")
            .WithTags("Memberships")
            .RequireAuthorization();

        bulkGroup.MapPost("/bulk", BulkActivateMemberships)
            .WithName("BulkActivateMemberships")
            .WithSummary("Bulk activate memberships for all family members without one")
            .AddEndpointFilter<ValidationFilter<BulkActivateMembershipRequest>>()
            .Produces<ApiResponse<BulkActivateMembershipResponse>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
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

    private static async Task<IResult> BulkActivateMemberships(
        [FromRoute] Guid familyUnitId,
        [FromBody] BulkActivateMembershipRequest request,
        ClaimsPrincipal user,
        MembershipsService service,
        CancellationToken ct)
    {
        var userRole = user.GetUserRole();
        var isAdminOrBoard = userRole == "Admin" || userRole == "Board";
        if (!isAdminOrBoard)
            return Results.Forbid();

        var result = await service.BulkActivateAsync(familyUnitId, request, ct);
        return Results.Ok(ApiResponse<BulkActivateMembershipResponse>.Ok(result));
    }

    public static void MapMembershipAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var adminGroup = app.MapGroup("/api/memberships/{membershipId:guid}")
            .WithTags("Memberships")
            .RequireAuthorization();

        adminGroup.MapPut("/member-number", UpdateMemberNumber)
            .WithName("UpdateMemberNumber")
            .WithSummary("Update member number (Admin/Board only)")
            .AddEndpointFilter<ValidationFilter<UpdateMemberNumberRequest>>()
            .Produces<ApiResponse<MembershipResponse>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> UpdateMemberNumber(
        [FromRoute] Guid membershipId,
        [FromBody] UpdateMemberNumberRequest request,
        ClaimsPrincipal user,
        MembershipsService service,
        CancellationToken ct)
    {
        var userRole = user.GetUserRole();
        var isAdminOrBoard = userRole == "Admin" || userRole == "Board";
        if (!isAdminOrBoard)
            return Results.Forbid();

        var result = await service.UpdateMemberNumberAsync(membershipId, request, ct);
        return Results.Ok(ApiResponse<MembershipResponse>.Ok(result));
    }

    public static void MapMembershipFeeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/memberships/{membershipId:guid}/fees")
            .WithTags("Membership Fees")
            .RequireAuthorization();

        group.MapGet("/", GetFees)
            .WithName("GetMembershipFees")
            .Produces<ApiResponse<IReadOnlyList<MembershipFeeResponse>>>();

        group.MapGet("/current", GetCurrentYearFee)
            .WithName("GetCurrentYearFee")
            .Produces<ApiResponse<MembershipFeeResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{feeId:guid}/pay", PayFee)
            .WithName("PayFee")
            .AddEndpointFilter<ValidationFilter<PayFeeRequest>>()
            .Produces<ApiResponse<MembershipFeeResponse>>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> GetFees(
        [FromRoute] Guid membershipId,
        MembershipsService service,
        CancellationToken ct)
    {
        var fees = await service.GetFeesAsync(membershipId, ct);
        return Results.Ok(ApiResponse<IReadOnlyList<MembershipFeeResponse>>.Ok(fees));
    }

    private static async Task<IResult> GetCurrentYearFee(
        [FromRoute] Guid membershipId,
        MembershipsService service,
        CancellationToken ct)
    {
        var fee = await service.GetCurrentYearFeeAsync(membershipId, ct);
        return Results.Ok(ApiResponse<MembershipFeeResponse>.Ok(fee));
    }

    private static async Task<IResult> PayFee(
        [FromRoute] Guid membershipId,
        [FromRoute] Guid feeId,
        [FromBody] PayFeeRequest request,
        MembershipsService service,
        CancellationToken ct)
    {
        var fee = await service.PayFeeAsync(feeId, request, ct);
        return Results.Ok(ApiResponse<MembershipFeeResponse>.Ok(fee));
    }
}
