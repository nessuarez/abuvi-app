using Abuvi.API.Common.Extensions;
using Abuvi.API.Common.Filters;
using Abuvi.API.Common.Models;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abuvi.API.Features.Camps;

/// <summary>
/// Endpoints for camp location management (CRUD operations)
/// </summary>
public static class CampsEndpoints
{
    /// <summary>
    /// Maps all camp-related endpoints to the application
    /// </summary>
    public static IEndpointRouteBuilder MapCampsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/camps")
            .WithTags("Camps")
            .WithOpenApi()
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board")); // Board+ only

        // GET /api/camps - Get all camps
        group.MapGet("/", GetAllCamps)
            .WithName("GetAllCamps")
            .WithSummary("Get all camp locations")
            .Produces<ApiResponse<List<CampResponse>>>()
            .Produces(401)
            .Produces(403);

        // GET /api/camps/{id} - Get camp by ID
        group.MapGet("/{id:guid}", GetCampById)
            .WithName("GetCampById")
            .WithSummary("Get camp location by ID")
            .Produces<ApiResponse<CampDetailResponse>>()
            .Produces(401)
            .Produces(403)
            .Produces(404);

        // POST /api/camps - Create new camp
        group.MapPost("/", CreateCamp)
            .WithName("CreateCamp")
            .WithSummary("Create a new camp location")
            .AddEndpointFilter<ValidationFilter<CreateCampRequest>>()
            .Produces<ApiResponse<CampDetailResponse>>(201)
            .Produces(400)
            .Produces(401)
            .Produces(403);

        // PUT /api/camps/{id} - Update existing camp
        group.MapPut("/{id:guid}", UpdateCamp)
            .WithName("UpdateCamp")
            .WithSummary("Update an existing camp location")
            .AddEndpointFilter<ValidationFilter<UpdateCampRequest>>()
            .Produces<ApiResponse<CampDetailResponse>>()
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(404);

        // DELETE /api/camps/{id} - Delete camp
        group.MapDelete("/{id:guid}", DeleteCamp)
            .WithName("DeleteCamp")
            .WithSummary("Delete a camp location")
            .Produces(204)
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(404);

        // Age Ranges Configuration endpoints (separate group for different authorization)
        var settingsGroup = app.MapGroup("/api/settings")
            .WithTags("Settings")
            .WithOpenApi();

        // GET /api/settings/age-ranges - Get age ranges configuration (Board+ only)
        settingsGroup.MapGet("/age-ranges", GetAgeRanges)
            .WithName("GetAgeRanges")
            .WithSummary("Get age ranges configuration")
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .Produces<ApiResponse<AgeRangesResponse>>()
            .Produces(401)
            .Produces(403)
            .Produces(404);

        // PUT /api/settings/age-ranges - Update age ranges configuration (Board+ only)
        settingsGroup.MapPut("/age-ranges", UpdateAgeRanges)
            .WithName("UpdateAgeRanges")
            .WithSummary("Update age ranges configuration")
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .AddEndpointFilter<ValidationFilter<UpdateAgeRangesRequest>>()
            .Produces<ApiResponse<AgeRangesResponse>>()
            .Produces(400)
            .Produces(401)
            .Produces(403);

        // Camp Editions endpoints (proposal workflow)
        var editionsGroup = app.MapGroup("/api/camps/editions")
            .WithTags("Camp Editions")
            .WithOpenApi()
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board")); // Board+ only

        // POST /api/camps/editions/propose - Propose new camp edition
        editionsGroup.MapPost("/propose", ProposeCampEdition)
            .WithName("ProposeCampEdition")
            .WithSummary("Propose a new camp edition")
            .AddEndpointFilter<ValidationFilter<ProposeCampEditionRequest>>()
            .Produces<ApiResponse<CampEditionResponse>>(201)
            .Produces(400)
            .Produces(401)
            .Produces(403);

        // GET /api/camps/editions/proposed - Get proposed editions
        editionsGroup.MapGet("/proposed", GetProposedEditions)
            .WithName("GetProposedEditions")
            .WithSummary("Get all proposed camp editions for a year")
            .Produces<ApiResponse<List<CampEditionResponse>>>()
            .Produces(401)
            .Produces(403);

        // POST /api/camps/editions/{id}/promote - Promote to draft
        editionsGroup.MapPost("/{id:guid}/promote", PromoteEditionToDraft)
            .WithName("PromoteEditionToDraft")
            .WithSummary("Promote proposed edition to draft status")
            .Produces<ApiResponse<CampEditionResponse>>()
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(404);

        // DELETE /api/camps/editions/{id}/reject - Reject proposal
        editionsGroup.MapDelete("/{id:guid}/reject", RejectProposal)
            .WithName("RejectProposal")
            .WithSummary("Reject a proposed edition (soft delete)")
            .Produces(204)
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(404);

        // PATCH /api/camps/editions/{id}/status - Change edition status (Board+)
        editionsGroup.MapPatch("/{id:guid}/status", ChangeEditionStatus)
            .WithName("ChangeEditionStatus")
            .WithSummary("Change the status of a camp edition (Proposed→Draft→Open→Closed→Completed)")
            .AddEndpointFilter<ValidationFilter<ChangeEditionStatusRequest>>()
            .Produces<ApiResponse<CampEditionResponse>>()
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(404);

        // Member+ read-only edition endpoints — separate group to override the Board-only group policy
        var editionsMemberGroup = app.MapGroup("/api/camps/editions")
            .WithTags("Camp Editions")
            .WithOpenApi()
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board", "Member"));

        // GET /api/camps/current - Get the current best-available camp edition (Member+)
        // This is a separate group to keep routing clean (different base path: /api/camps vs /api/camps/editions)
        var campCurrentGroup = app.MapGroup("/api/camps")
            .WithTags("Camp Editions")
            .WithOpenApi()
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board", "Member"));

        // NOTE: "current" is a literal string segment and does not conflict with /{id:guid}
        // because the :guid constraint prevents non-GUID strings from matching.
        campCurrentGroup.MapGet("/current", GetCurrentCampEdition)
            .WithName("GetCurrentCampEdition")
            .WithSummary("Get the current (best-available) camp edition")
            .Produces<ApiResponse<CurrentCampEditionResponse>>()
            .Produces(404)
            .Produces(401);

        // GET /api/camps/editions/active - Get active (Open) edition (Member+)
        // NOTE: Must be registered before /{id:guid} to avoid "active" being treated as a GUID
        editionsMemberGroup.MapGet("/active", GetActiveEdition)
            .WithName("GetActiveEdition")
            .WithSummary("Get the currently open camp edition for the given year")
            .Produces<ApiResponse<ActiveCampEditionResponse?>>()
            .Produces(401);

        // GET /api/camps/editions/{id} - Get edition by ID (Member+)
        editionsMemberGroup.MapGet("/{id:guid}", GetEditionById)
            .WithName("GetEditionById")
            .WithSummary("Get a camp edition by ID")
            .Produces<ApiResponse<CampEditionResponse>>()
            .Produces(401)
            .Produces(404);

        // PUT /api/camps/editions/{id} - Update edition (Board+)
        editionsGroup.MapPut("/{id:guid}", UpdateEdition)
            .WithName("UpdateEdition")
            .WithSummary("Update a camp edition (restrictions apply based on status)")
            .AddEndpointFilter<ValidationFilter<UpdateCampEditionRequest>>()
            .Produces<ApiResponse<CampEditionResponse>>()
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(404);

        // GET /api/camps/editions - List all editions with optional filtering (Board+)
        editionsGroup.MapGet("/", GetAllEditions)
            .WithName("GetAllEditions")
            .WithSummary("Get all camp editions with optional filtering by year, status, and campId")
            .Produces<ApiResponse<List<CampEditionResponse>>>()
            .Produces(401)
            .Produces(403);

        return app;
    }

    /// <summary>
    /// Get all camps with optional filtering and pagination
    /// </summary>
    private static async Task<IResult> GetAllCamps(
        [FromServices] CampsService service,
        [FromQuery] bool? isActive = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var camps = await service.GetAllAsync(isActive, skip, take, cancellationToken);
        return Results.Ok(ApiResponse<List<CampResponse>>.Ok(camps));
    }

    /// <summary>
    /// Get a camp by ID
    /// </summary>
    private static async Task<IResult> GetCampById(
        [FromServices] CampsService service,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var camp = await service.GetByIdAsync(id, cancellationToken);

        if (camp == null)
        {
            return Results.NotFound(ApiResponse<CampDetailResponse>.NotFound("Campamento no encontrado"));
        }

        return Results.Ok(ApiResponse<CampDetailResponse>.Ok(camp));
    }

    /// <summary>
    /// Create a new camp
    /// </summary>
    private static async Task<IResult> CreateCamp(
        [FromServices] CampsService service,
        CreateCampRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var camp = await service.CreateAsync(request, cancellationToken);
            return Results.Created($"/api/camps/{camp.Id}", ApiResponse<CampDetailResponse>.Ok(camp));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ApiResponse<CampDetailResponse>.Fail(ex.Message, "VALIDATION_ERROR"));
        }
    }

    /// <summary>
    /// Update an existing camp
    /// </summary>
    private static async Task<IResult> UpdateCamp(
        [FromServices] CampsService service,
        Guid id,
        UpdateCampRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var camp = await service.UpdateAsync(id, request, cancellationToken);

            if (camp == null)
            {
                return Results.NotFound(ApiResponse<CampDetailResponse>.NotFound("Campamento no encontrado"));
            }

            return Results.Ok(ApiResponse<CampDetailResponse>.Ok(camp));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ApiResponse<CampDetailResponse>.Fail(ex.Message, "VALIDATION_ERROR"));
        }
    }

    /// <summary>
    /// Delete a camp
    /// </summary>
    private static async Task<IResult> DeleteCamp(
        [FromServices] CampsService service,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deleted = await service.DeleteAsync(id, cancellationToken);

            if (!deleted)
            {
                return Results.NotFound(ApiResponse<object>.NotFound("Camp not found"));
            }

            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ApiResponse<object>.Fail(ex.Message, "OPERATION_ERROR"));
        }
    }

    /// <summary>
    /// Get age ranges configuration
    /// </summary>
    private static async Task<IResult> GetAgeRanges(
        [FromServices] AssociationSettingsService service,
        CancellationToken cancellationToken = default)
    {
        var ageRanges = await service.GetAgeRangesAsync(cancellationToken);

        if (ageRanges == null)
        {
            return Results.NotFound(ApiResponse<AgeRangesResponse>.NotFound("Age ranges configuration not found"));
        }

        return Results.Ok(ApiResponse<AgeRangesResponse>.Ok(ageRanges));
    }

    /// <summary>
    /// Update age ranges configuration
    /// </summary>
    private static async Task<IResult> UpdateAgeRanges(
        [FromServices] AssociationSettingsService service,
        ClaimsPrincipal user,
        UpdateAgeRangesRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get user ID from claims
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var ageRanges = await service.UpdateAgeRangesAsync(request, userId, cancellationToken);
            return Results.Ok(ApiResponse<AgeRangesResponse>.Ok(ageRanges));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ApiResponse<AgeRangesResponse>.Fail(ex.Message, "VALIDATION_ERROR"));
        }
    }

    /// <summary>
    /// Propose a new camp edition
    /// </summary>
    private static async Task<IResult> ProposeCampEdition(
        [FromServices] CampEditionsService service,
        ProposeCampEditionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var edition = await service.ProposeAsync(request, cancellationToken);
            return Results.Created($"/api/camps/editions/{edition.Id}", ApiResponse<CampEditionResponse>.Ok(edition));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ApiResponse<CampEditionResponse>.Fail(ex.Message, "OPERATION_ERROR"));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ApiResponse<CampEditionResponse>.Fail(ex.Message, "VALIDATION_ERROR"));
        }
    }

    /// <summary>
    /// Get all proposed camp editions for a year
    /// </summary>
    private static async Task<IResult> GetProposedEditions(
        [FromServices] CampEditionsService service,
        [FromQuery] int year,
        CancellationToken cancellationToken = default)
    {
        var editions = await service.GetProposedAsync(year, cancellationToken);
        return Results.Ok(ApiResponse<List<CampEditionResponse>>.Ok(editions));
    }

    /// <summary>
    /// Promote proposed edition to draft status
    /// </summary>
    private static async Task<IResult> PromoteEditionToDraft(
        [FromServices] CampEditionsService service,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var edition = await service.PromoteToDraftAsync(id, cancellationToken);
            return Results.Ok(ApiResponse<CampEditionResponse>.Ok(edition));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ApiResponse<CampEditionResponse>.Fail(ex.Message, "OPERATION_ERROR"));
        }
    }

    /// <summary>
    /// Reject a proposed edition (soft delete)
    /// </summary>
    private static async Task<IResult> RejectProposal(
        [FromServices] CampEditionsService service,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deleted = await service.RejectProposalAsync(id, cancellationToken);

            if (!deleted)
            {
                return Results.NotFound(ApiResponse<object>.NotFound("Camp edition not found"));
            }

            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ApiResponse<object>.Fail(ex.Message, "OPERATION_ERROR"));
        }
    }

    /// <summary>
    /// Change the status of a camp edition
    /// </summary>
    private static async Task<IResult> ChangeEditionStatus(
        [FromServices] CampEditionsService service,
        Guid id,
        ChangeEditionStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var edition = await service.ChangeStatusAsync(id, request.Status, cancellationToken);
            return Results.Ok(ApiResponse<CampEditionResponse>.Ok(edition));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("no fue encontrada"))
        {
            return Results.NotFound(ApiResponse<CampEditionResponse>.NotFound(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ApiResponse<CampEditionResponse>.Fail(ex.Message, "OPERATION_ERROR"));
        }
    }

    /// <summary>
    /// Get the current best-available camp edition (status priority + year fallback)
    /// </summary>
    private static async Task<IResult> GetCurrentCampEdition(
        [FromServices] CampEditionsService service,
        CancellationToken cancellationToken = default)
    {
        var edition = await service.GetCurrentAsync(cancellationToken);

        if (edition == null)
            return Results.NotFound(
                ApiResponse<CurrentCampEditionResponse>.NotFound(
                    "No hay ninguna edición de campamento disponible"));

        return Results.Ok(ApiResponse<CurrentCampEditionResponse>.Ok(edition));
    }

    /// <summary>
    /// Get the currently active (Open) camp edition for the given year
    /// </summary>
    private static async Task<IResult> GetActiveEdition(
        [FromServices] CampEditionsService service,
        [FromQuery] int? year = null,
        CancellationToken cancellationToken = default)
    {
        var edition = await service.GetActiveEditionAsync(year, cancellationToken);
        return Results.Ok(ApiResponse<ActiveCampEditionResponse?>.Ok(edition));
    }

    /// <summary>
    /// Get a camp edition by ID
    /// </summary>
    private static async Task<IResult> GetEditionById(
        [FromServices] CampEditionsService service,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var edition = await service.GetByIdAsync(id, cancellationToken);
        if (edition == null)
            return Results.NotFound(ApiResponse<CampEditionResponse>.NotFound("La edición de campamento no fue encontrada"));

        return Results.Ok(ApiResponse<CampEditionResponse>.Ok(edition));
    }

    /// <summary>
    /// Update a camp edition (restrictions apply based on status)
    /// </summary>
    private static async Task<IResult> UpdateEdition(
        [FromServices] CampEditionsService service,
        Guid id,
        UpdateCampEditionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var edition = await service.UpdateAsync(id, request, cancellationToken);
            return Results.Ok(ApiResponse<CampEditionResponse>.Ok(edition));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("no fue encontrada"))
        {
            return Results.NotFound(ApiResponse<CampEditionResponse>.NotFound(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ApiResponse<CampEditionResponse>.Fail(ex.Message, "OPERATION_ERROR"));
        }
    }

    /// <summary>
    /// Get all camp editions with optional filtering
    /// </summary>
    private static async Task<IResult> GetAllEditions(
        [FromServices] CampEditionsService service,
        [FromQuery] int? year = null,
        [FromQuery] CampEditionStatus? status = null,
        [FromQuery] Guid? campId = null,
        CancellationToken cancellationToken = default)
    {
        var editions = await service.GetAllAsync(year, status, campId, cancellationToken);
        return Results.Ok(ApiResponse<List<CampEditionResponse>>.Ok(editions));
    }
}
