using System.Security.Claims;
using Abuvi.API.Common.Extensions;
using Abuvi.API.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace Abuvi.API.Features.Camps;

public static class CampEditionAttendanceEndpoints
{
    public static void MapCampEditionAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/camp-editions/{editionId:guid}/attendance")
            .WithTags("CampEditionAttendance")
            .RequireAuthorization();

        group.MapPost("/", DeclareAttendance)
            .WithName("DeclareCampAttendance")
            .Produces<ApiResponse<object>>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/", WithdrawAttendance)
            .WithName("WithdrawCampAttendance")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/", GetAttendance)
            .WithName("GetCampAttendance")
            .Produces<ApiResponse<IReadOnlyList<AttendanceEntryResponse>>>()
            .Produces(StatusCodes.Status404NotFound);

        var timelineGroup = app.MapGroup("/api/users/me")
            .WithTags("CampEditionAttendance")
            .RequireAuthorization();

        timelineGroup.MapGet("/camp-timeline", GetTimeline)
            .WithName("GetMyCampTimeline")
            .Produces<ApiResponse<CampTimelineResponse>>();
    }

    private static async Task<IResult> DeclareAttendance(
        Guid editionId,
        [FromBody] DeclareAttendanceRequest? request,
        ClaimsPrincipal user,
        CampEditionAttendanceService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var declared = await service.DeclareAsync(editionId, userId, request?.FamilyMemberId, ct);

        // False means the family member does not belong to the caller's family unit.
        if (!declared)
            return Results.Forbid();

        // Declaring twice is intentionally idempotent — a toggle that errors when pressed
        // twice is worse than one that shrugs.
        return Results.Ok(ApiResponse<object>.Ok(new { attended = true }));
    }

    private static async Task<IResult> WithdrawAttendance(
        Guid editionId,
        ClaimsPrincipal user,
        CampEditionAttendanceService service,
        CancellationToken ct,
        [FromQuery] Guid? familyMemberId = null)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        await service.WithdrawAsync(editionId, userId, familyMemberId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetAttendance(
        Guid editionId,
        CampEditionAttendanceService service,
        CancellationToken ct)
    {
        var entries = await service.GetForEditionAsync(editionId, ct);
        return Results.Ok(ApiResponse<IReadOnlyList<AttendanceEntryResponse>>.Ok(entries));
    }

    private static async Task<IResult> GetTimeline(
        ClaimsPrincipal user,
        CampEditionAttendanceService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var timeline = await service.GetTimelineAsync(userId, ct);
        return Results.Ok(ApiResponse<CampTimelineResponse>.Ok(timeline));
    }
}
