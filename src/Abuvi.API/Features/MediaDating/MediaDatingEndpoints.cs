using System.Security.Claims;
using Abuvi.API.Common.Extensions;
using Abuvi.API.Common.Filters;
using Abuvi.API.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace Abuvi.API.Features.MediaDating;

public static class MediaDatingEndpoints
{
    public static void MapMediaDatingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/media-items/{mediaItemId:guid}/year-proposals")
            .WithTags("MediaDating")
            .RequireAuthorization();

        group.MapGet("/", GetTally)
            .WithName("GetYearProposalTally")
            .Produces<ApiResponse<YearProposalTallyResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        // PUT, not POST: one vote per user per item, so proposing again replaces the
        // previous answer rather than stacking a second one.
        group.MapPut("/", UpsertProposal)
            .AddEndpointFilter<ValidationFilter<UpsertYearProposalRequest>>()
            .WithName("UpsertYearProposal")
            .Produces<ApiResponse<YearProposalTallyResponse>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/", WithdrawProposal)
            .WithName("WithdrawYearProposal")
            .Produces<ApiResponse<YearProposalTallyResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        // Admin override lives on the item, not on the proposals: it settles the date
        // rather than casting a vote.
        var itemGroup = app.MapGroup("/api/media-items")
            .WithTags("MediaDating")
            .RequireAuthorization();

        itemGroup.MapPatch("/{id:guid}/year", SetYear)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .AddEndpointFilter<ValidationFilter<SetYearRequest>>()
            .WithName("SetMediaItemYear")
            .Produces<ApiResponse<YearProposalTallyResponse>>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetTally(
        Guid mediaItemId,
        ClaimsPrincipal user,
        MediaDatingService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var tally = await service.GetTallyAsync(mediaItemId, userId, user.IsAdminOrBoard(), ct);
        return Results.Ok(ApiResponse<YearProposalTallyResponse>.Ok(tally));
    }

    private static async Task<IResult> UpsertProposal(
        Guid mediaItemId,
        [FromBody] UpsertYearProposalRequest request,
        ClaimsPrincipal user,
        MediaDatingService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var tally = await service.UpsertAsync(
            mediaItemId, userId, user.IsAdminOrBoard(), request, ct);

        return Results.Ok(ApiResponse<YearProposalTallyResponse>.Ok(tally));
    }

    private static async Task<IResult> WithdrawProposal(
        Guid mediaItemId,
        ClaimsPrincipal user,
        MediaDatingService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var tally = await service.WithdrawAsync(mediaItemId, userId, user.IsAdminOrBoard(), ct);
        return Results.Ok(ApiResponse<YearProposalTallyResponse>.Ok(tally));
    }

    private static async Task<IResult> SetYear(
        Guid id,
        [FromBody] SetYearRequest request,
        ClaimsPrincipal user,
        MediaDatingService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var tally = await service.SetYearAsAdminAsync(id, userId, request, ct);
        return Results.Ok(ApiResponse<YearProposalTallyResponse>.Ok(tally));
    }
}
