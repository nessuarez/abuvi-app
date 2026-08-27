using System.Security.Claims;
using Abuvi.API.Common.Extensions;
using Abuvi.API.Common.Filters;
using Abuvi.API.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace Abuvi.API.Features.MediaComments;

public static class MediaCommentsEndpoints
{
    /// <summary>Rate-limiting policy name, defined in Program.cs.</summary>
    public const string CommentsRateLimitPolicy = "comments";

    public static void MapMediaCommentsEndpoints(this IEndpointRouteBuilder app)
    {
        // Thread endpoints hang off the media item they belong to.
        var itemGroup = app.MapGroup("/api/media-items/{mediaItemId:guid}/comments")
            .WithTags("MediaComments")
            .RequireAuthorization();

        itemGroup.MapGet("/", GetThread)
            .WithName("GetMediaCommentThread")
            .Produces<ApiResponse<IReadOnlyList<MediaCommentResponse>>>()
            .Produces(StatusCodes.Status404NotFound);

        itemGroup.MapPost("/", CreateComment)
            .AddEndpointFilter<ValidationFilter<CreateMediaCommentRequest>>()
            .RequireRateLimiting(CommentsRateLimitPolicy)
            .WithName("CreateMediaComment")
            .Produces<ApiResponse<MediaCommentResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status429TooManyRequests);

        // Operations on an existing comment are addressed directly.
        var group = app.MapGroup("/api/media-comments")
            .WithTags("MediaComments")
            .RequireAuthorization();

        group.MapPut("/{id:guid}", UpdateComment)
            .AddEndpointFilter<ValidationFilter<UpdateMediaCommentRequest>>()
            .WithName("UpdateMediaComment")
            .Produces<ApiResponse<MediaCommentResponse>>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteComment)
            .WithName("DeleteMediaComment")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/report", ReportComment)
            .AddEndpointFilter<ValidationFilter<ReportMediaCommentRequest>>()
            .WithName("ReportMediaComment")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/reports", GetReports)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .WithName("ListMediaCommentReports")
            .Produces<ApiResponse<IReadOnlyList<MediaCommentReportResponse>>>()
            .Produces(StatusCodes.Status403Forbidden);

        group.MapPatch("/reports/{id:guid}", ReviewReport)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .WithName("ReviewMediaCommentReport")
            .Produces<ApiResponse<MediaCommentReportResponse>>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetThread(
        Guid mediaItemId,
        ClaimsPrincipal user,
        MediaCommentsService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var thread = await service.GetThreadAsync(mediaItemId, userId, user.IsAdminOrBoard(), ct);
        return Results.Ok(ApiResponse<IReadOnlyList<MediaCommentResponse>>.Ok(thread));
    }

    private static async Task<IResult> CreateComment(
        Guid mediaItemId,
        [FromBody] CreateMediaCommentRequest request,
        ClaimsPrincipal user,
        MediaCommentsService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var comment = await service.CreateAsync(
            mediaItemId, userId, user.IsAdminOrBoard(), request, ct);

        // Null means the item is not approved yet and the caller is not a moderator.
        return comment is null
            ? Results.Forbid()
            : Results.Created(
                $"/api/media-comments/{comment.Id}",
                ApiResponse<MediaCommentResponse>.Ok(comment));
    }

    private static async Task<IResult> UpdateComment(
        Guid id,
        [FromBody] UpdateMediaCommentRequest request,
        ClaimsPrincipal user,
        MediaCommentsService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var updated = await service.UpdateAsync(id, userId, request, ct);

        // Null means not the author, or the 15-minute window has closed.
        return updated is null
            ? Results.Forbid()
            : Results.Ok(ApiResponse<MediaCommentResponse>.Ok(updated));
    }

    private static async Task<IResult> DeleteComment(
        Guid id,
        ClaimsPrincipal user,
        MediaCommentsService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var deleted = await service.DeleteAsync(id, userId, user.IsAdminOrBoard(), ct);
        return deleted ? Results.NoContent() : Results.Forbid();
    }

    private static async Task<IResult> ReportComment(
        Guid id,
        [FromBody] ReportMediaCommentRequest request,
        ClaimsPrincipal user,
        MediaCommentsService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        await service.ReportAsync(id, userId, request, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetReports(
        MediaCommentsService service,
        CancellationToken ct,
        [FromQuery] MediaCommentReportStatus? status = null)
    {
        var reports = await service.GetReportsAsync(status, ct);
        return Results.Ok(ApiResponse<IReadOnlyList<MediaCommentReportResponse>>.Ok(reports));
    }

    private static async Task<IResult> ReviewReport(
        Guid id,
        [FromBody] ReviewReportRequest request,
        ClaimsPrincipal user,
        MediaCommentsService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var report = await service.ReviewReportAsync(id, userId, request.Status, ct);
        return Results.Ok(ApiResponse<MediaCommentReportResponse>.Ok(report));
    }
}
