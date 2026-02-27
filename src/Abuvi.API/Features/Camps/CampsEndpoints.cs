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

        // Camp Photos endpoints (Admin/Board only)
        var photosGroup = app.MapGroup("/api/camps/{campId:guid}/photos")
            .WithTags("Camp Photos")
            .WithOpenApi()
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

        // GET /api/camps/{campId}/photos - List photos for a camp
        photosGroup.MapGet("/", GetCampPhotos)
            .WithName("GetCampPhotos")
            .WithSummary("Get all photos for a camp ordered by display order")
            .Produces<ApiResponse<List<CampPhotoResponse>>>()
            .Produces(401)
            .Produces(403);

        // POST /api/camps/{campId}/photos - Add a photo to a camp
        photosGroup.MapPost("/", AddCampPhoto)
            .WithName("AddCampPhoto")
            .WithSummary("Add a manually-uploaded photo to a camp")
            .AddEndpointFilter<ValidationFilter<AddCampPhotoRequest>>()
            .Produces<ApiResponse<CampPhotoResponse>>(201)
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(404);

        // PUT /api/camps/{campId}/photos/{photoId} - Update a photo
        photosGroup.MapPut("/{photoId:guid}", UpdateCampPhoto)
            .WithName("UpdateCampPhoto")
            .WithSummary("Update a camp photo")
            .AddEndpointFilter<ValidationFilter<UpdateCampPhotoRequest>>()
            .Produces<ApiResponse<CampPhotoResponse>>()
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(404);

        // DELETE /api/camps/{campId}/photos/{photoId} - Delete a photo
        photosGroup.MapDelete("/{photoId:guid}", DeleteCampPhoto)
            .WithName("DeleteCampPhoto")
            .WithSummary("Delete a camp photo")
            .Produces(204)
            .Produces(401)
            .Produces(403)
            .Produces(404);

        // PUT /api/camps/{campId}/photos/reorder - Reorder photos
        photosGroup.MapPut("/reorder", ReorderCampPhotos)
            .WithName("ReorderCampPhotos")
            .WithSummary("Bulk reorder camp photos")
            .AddEndpointFilter<ValidationFilter<ReorderCampPhotosRequest>>()
            .Produces(204)
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(404);

        // PUT /api/camps/{campId}/photos/{photoId}/primary - Set primary photo
        photosGroup.MapPut("/{photoId:guid}/primary", SetPrimaryPhoto)
            .WithName("SetPrimaryPhoto")
            .WithSummary("Set a photo as the primary display photo for a camp")
            .Produces<ApiResponse<CampPhotoResponse>>()
            .Produces(401)
            .Produces(403)
            .Produces(404);

        // ── Camp Edition Extras (Board+ write — edition-scoped) ───────────────
        var extrasWriteGroup = app.MapGroup("/api/camps/editions/{editionId:guid}/extras")
            .WithTags("Camp Edition Extras")
            .WithOpenApi()
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

        // POST /api/camps/editions/{editionId}/extras - Create extra
        extrasWriteGroup.MapPost("/", CreateExtra)
            .WithName("CreateCampEditionExtra")
            .WithSummary("Create a new extra for a camp edition")
            .AddEndpointFilter<ValidationFilter<CreateCampEditionExtraRequest>>()
            .Produces<ApiResponse<CampEditionExtraResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // ── Camp Edition Extras (Member+ read — edition-scoped) ───────────────
        var extrasReadGroup = app.MapGroup("/api/camps/editions/{editionId:guid}/extras")
            .WithTags("Camp Edition Extras")
            .WithOpenApi()
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board", "Member"));

        // GET /api/camps/editions/{editionId}/extras - List extras for edition
        extrasReadGroup.MapGet("/", GetExtrasByEdition)
            .WithName("GetCampEditionExtras")
            .WithSummary("List all extras for a camp edition")
            .Produces<ApiResponse<List<CampEditionExtraResponse>>>()
            .Produces(StatusCodes.Status401Unauthorized);

        // ── Camp Edition Extras by ID (Board+ write) ──────────────────────────
        var extrasByIdWriteGroup = app.MapGroup("/api/camps/editions/extras")
            .WithTags("Camp Edition Extras")
            .WithOpenApi()
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

        // PUT /api/camps/editions/extras/{id} - Update extra
        extrasByIdWriteGroup.MapPut("/{id:guid}", UpdateExtra)
            .WithName("UpdateCampEditionExtra")
            .WithSummary("Update a camp edition extra")
            .AddEndpointFilter<ValidationFilter<UpdateCampEditionExtraRequest>>()
            .Produces<ApiResponse<CampEditionExtraResponse>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // DELETE /api/camps/editions/extras/{id} - Delete extra
        extrasByIdWriteGroup.MapDelete("/{id:guid}", DeleteExtra)
            .WithName("DeleteCampEditionExtra")
            .WithSummary("Delete a camp edition extra (only if not sold)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // PATCH /api/camps/editions/extras/{id}/activate - Activate extra
        extrasByIdWriteGroup.MapPatch("/{id:guid}/activate", ActivateExtra)
            .WithName("ActivateCampEditionExtra")
            .WithSummary("Activate a camp edition extra")
            .Produces<ApiResponse<CampEditionExtraResponse>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // PATCH /api/camps/editions/extras/{id}/deactivate - Deactivate extra
        extrasByIdWriteGroup.MapPatch("/{id:guid}/deactivate", DeactivateExtra)
            .WithName("DeactivateCampEditionExtra")
            .WithSummary("Deactivate a camp edition extra")
            .Produces<ApiResponse<CampEditionExtraResponse>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // ── Camp Edition Extras by ID (Member+ read) ──────────────────────────
        var extrasByIdReadGroup = app.MapGroup("/api/camps/editions/extras")
            .WithTags("Camp Edition Extras")
            .WithOpenApi()
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board", "Member"));

        // GET /api/camps/editions/extras/{id} - Get extra by ID
        extrasByIdReadGroup.MapGet("/{id:guid}", GetExtraById)
            .WithName("GetCampEditionExtraById")
            .WithSummary("Get a specific camp edition extra by ID")
            .Produces<ApiResponse<CampEditionExtraResponse>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        // ── Camp Edition Accommodations (Board+ write — edition-scoped) ──────────
        var accommodationsWriteGroup = app.MapGroup("/api/camps/editions/{editionId:guid}/accommodations")
            .WithTags("Camp Edition Accommodations")
            .WithOpenApi()
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

        // POST /api/camps/editions/{editionId}/accommodations - Create accommodation
        accommodationsWriteGroup.MapPost("/", CreateAccommodation)
            .WithName("CreateCampEditionAccommodation")
            .WithSummary("Create a new accommodation option for a camp edition")
            .AddEndpointFilter<ValidationFilter<CreateCampEditionAccommodationRequest>>()
            .Produces<ApiResponse<CampEditionAccommodationResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // ── Camp Edition Accommodations (Member+ read — edition-scoped) ──────────
        var accommodationsReadGroup = app.MapGroup("/api/camps/editions/{editionId:guid}/accommodations")
            .WithTags("Camp Edition Accommodations")
            .WithOpenApi()
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board", "Member"));

        // GET /api/camps/editions/{editionId}/accommodations - List accommodations
        accommodationsReadGroup.MapGet("/", GetAccommodationsByEdition)
            .WithName("GetCampEditionAccommodations")
            .WithSummary("List all accommodations for a camp edition")
            .Produces<ApiResponse<List<CampEditionAccommodationResponse>>>()
            .Produces(StatusCodes.Status401Unauthorized);

        // ── Camp Edition Accommodations by ID (Board+ write) ────────────────────
        var accommodationsByIdWriteGroup = app.MapGroup("/api/camps/editions/accommodations")
            .WithTags("Camp Edition Accommodations")
            .WithOpenApi()
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

        // PUT /api/camps/editions/accommodations/{id} - Update accommodation
        accommodationsByIdWriteGroup.MapPut("/{id:guid}", UpdateAccommodation)
            .WithName("UpdateCampEditionAccommodation")
            .WithSummary("Update a camp edition accommodation")
            .AddEndpointFilter<ValidationFilter<UpdateCampEditionAccommodationRequest>>()
            .Produces<ApiResponse<CampEditionAccommodationResponse>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // DELETE /api/camps/editions/accommodations/{id} - Delete accommodation
        accommodationsByIdWriteGroup.MapDelete("/{id:guid}", DeleteAccommodation)
            .WithName("DeleteCampEditionAccommodation")
            .WithSummary("Delete a camp edition accommodation (only if no preferences exist)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // PATCH /api/camps/editions/accommodations/{id}/activate
        accommodationsByIdWriteGroup.MapPatch("/{id:guid}/activate", ActivateAccommodation)
            .WithName("ActivateCampEditionAccommodation")
            .WithSummary("Activate a camp edition accommodation")
            .Produces<ApiResponse<CampEditionAccommodationResponse>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // PATCH /api/camps/editions/accommodations/{id}/deactivate
        accommodationsByIdWriteGroup.MapPatch("/{id:guid}/deactivate", DeactivateAccommodation)
            .WithName("DeactivateCampEditionAccommodation")
            .WithSummary("Deactivate a camp edition accommodation")
            .Produces<ApiResponse<CampEditionAccommodationResponse>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // ── Camp Edition Accommodations by ID (Member+ read) ────────────────────
        var accommodationsByIdReadGroup = app.MapGroup("/api/camps/editions/accommodations")
            .WithTags("Camp Edition Accommodations")
            .WithOpenApi()
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board", "Member"));

        // GET /api/camps/editions/accommodations/{id} - Get accommodation by ID
        accommodationsByIdReadGroup.MapGet("/{id:guid}", GetAccommodationById)
            .WithName("GetCampEditionAccommodationById")
            .WithSummary("Get a specific camp edition accommodation by ID")
            .Produces<ApiResponse<CampEditionAccommodationResponse>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

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
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = user.IsInRole("Admin");

        // Open → Draft is Admin-only: fetch current status to verify the role guard
        if (request.Status == CampEditionStatus.Draft)
        {
            var current = await service.GetByIdAsync(id, cancellationToken);
            if (current?.Status == CampEditionStatus.Open && !isAdmin)
                return Results.Forbid();
        }

        // force flag is Admin-only
        if (request.Force && !isAdmin)
            return Results.Forbid();

        try
        {
            var edition = await service.ChangeStatusAsync(id, request.Status, request.Force, cancellationToken);
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

    /// <summary>
    /// Get all photos for a camp
    /// </summary>
    private static async Task<IResult> GetCampPhotos(
        [FromServices] CampPhotosService service,
        Guid campId,
        CancellationToken cancellationToken = default)
    {
        var photos = await service.GetPhotosAsync(campId, cancellationToken);
        return Results.Ok(ApiResponse<List<CampPhotoResponse>>.Ok(photos));
    }

    /// <summary>
    /// Add a manually-uploaded photo to a camp
    /// </summary>
    private static async Task<IResult> AddCampPhoto(
        [FromServices] CampPhotosService service,
        Guid campId,
        AddCampPhotoRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var photo = await service.AddPhotoAsync(campId, request, cancellationToken);
            return Results.Created($"/api/camps/{campId}/photos/{photo.Id}", ApiResponse<CampPhotoResponse>.Ok(photo));
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(ApiResponse<CampPhotoResponse>.NotFound(ex.Message));
        }
    }

    /// <summary>
    /// Update a camp photo
    /// </summary>
    private static async Task<IResult> UpdateCampPhoto(
        [FromServices] CampPhotosService service,
        Guid campId,
        Guid photoId,
        UpdateCampPhotoRequest request,
        CancellationToken cancellationToken = default)
    {
        var photo = await service.UpdatePhotoAsync(campId, photoId, request, cancellationToken);
        if (photo == null)
            return Results.NotFound(ApiResponse<CampPhotoResponse>.NotFound("Photo not found"));

        return Results.Ok(ApiResponse<CampPhotoResponse>.Ok(photo));
    }

    /// <summary>
    /// Delete a camp photo
    /// </summary>
    private static async Task<IResult> DeleteCampPhoto(
        [FromServices] CampPhotosService service,
        Guid campId,
        Guid photoId,
        CancellationToken cancellationToken = default)
    {
        var deleted = await service.DeletePhotoAsync(campId, photoId, cancellationToken);
        if (!deleted)
            return Results.NotFound(ApiResponse<object>.NotFound("Photo not found"));

        return Results.NoContent();
    }

    /// <summary>
    /// Bulk reorder camp photos
    /// </summary>
    private static async Task<IResult> ReorderCampPhotos(
        [FromServices] CampPhotosService service,
        Guid campId,
        ReorderCampPhotosRequest request,
        CancellationToken cancellationToken = default)
    {
        var success = await service.ReorderPhotosAsync(campId, request, cancellationToken);
        if (!success)
            return Results.NotFound(ApiResponse<object>.NotFound("Camp not found"));

        return Results.NoContent();
    }

    /// <summary>
    /// Set the primary photo for a camp
    /// </summary>
    private static async Task<IResult> SetPrimaryPhoto(
        [FromServices] CampPhotosService service,
        Guid campId,
        Guid photoId,
        CancellationToken cancellationToken = default)
    {
        var photo = await service.SetPrimaryPhotoAsync(campId, photoId, cancellationToken);
        if (photo == null)
            return Results.NotFound(ApiResponse<CampPhotoResponse>.NotFound("Photo not found"));

        return Results.Ok(ApiResponse<CampPhotoResponse>.Ok(photo));
    }

    // ── Camp Edition Extras handlers ──────────────────────────────────────────

    private static async Task<IResult> GetExtrasByEdition(
        Guid editionId,
        [FromQuery] bool? activeOnly,
        [FromServices] CampEditionExtrasService service,
        CancellationToken ct)
    {
        var extras = await service.GetByEditionAsync(editionId, activeOnly, ct);
        return Results.Ok(ApiResponse<List<CampEditionExtraResponse>>.Ok(extras));
    }

    private static async Task<IResult> GetExtraById(
        Guid id,
        [FromServices] CampEditionExtrasService service,
        CancellationToken ct)
    {
        var extra = await service.GetByIdAsync(id, ct);
        return extra is not null
            ? Results.Ok(ApiResponse<CampEditionExtraResponse>.Ok(extra))
            : Results.NotFound(ApiResponse<CampEditionExtraResponse>.NotFound(
                $"Extra con ID '{id}' no encontrado"));
    }

    private static async Task<IResult> CreateExtra(
        Guid editionId,
        CreateCampEditionExtraRequest request,
        [FromServices] CampEditionExtrasService service,
        CancellationToken ct)
    {
        try
        {
            var extra = await service.CreateAsync(editionId, request, ct);
            return Results.Created(
                $"/api/camps/editions/extras/{extra.Id}",
                ApiResponse<CampEditionExtraResponse>.Ok(extra));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(
                ApiResponse<CampEditionExtraResponse>.Fail(ex.Message, "OPERATION_ERROR"));
        }
    }

    private static async Task<IResult> UpdateExtra(
        Guid id,
        UpdateCampEditionExtraRequest request,
        [FromServices] CampEditionExtrasService service,
        CancellationToken ct)
    {
        try
        {
            var extra = await service.UpdateAsync(id, request, ct);
            return Results.Ok(ApiResponse<CampEditionExtraResponse>.Ok(extra));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(
                ApiResponse<CampEditionExtraResponse>.Fail(ex.Message, "OPERATION_ERROR"));
        }
    }

    private static async Task<IResult> DeleteExtra(
        Guid id,
        [FromServices] CampEditionExtrasService service,
        CancellationToken ct)
    {
        try
        {
            var deleted = await service.DeleteAsync(id, ct);
            return deleted
                ? Results.NoContent()
                : Results.NotFound(ApiResponse<object>.NotFound(
                    $"Extra con ID '{id}' no encontrado"));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ApiResponse<object>.Fail(ex.Message, "OPERATION_ERROR"));
        }
    }

    private static async Task<IResult> ActivateExtra(
        Guid id,
        [FromServices] CampEditionExtrasService service,
        CancellationToken ct)
    {
        try
        {
            var extra = await service.ActivateAsync(id, ct);
            return Results.Ok(ApiResponse<CampEditionExtraResponse>.Ok(extra));
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
    }

    private static async Task<IResult> DeactivateExtra(
        Guid id,
        [FromServices] CampEditionExtrasService service,
        CancellationToken ct)
    {
        try
        {
            var extra = await service.DeactivateAsync(id, ct);
            return Results.Ok(ApiResponse<CampEditionExtraResponse>.Ok(extra));
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
    }

    // ── Camp Edition Accommodations handlers ─────────────────────────────────

    private static async Task<IResult> GetAccommodationsByEdition(
        Guid editionId,
        [FromQuery] bool? activeOnly,
        [FromServices] CampEditionAccommodationsService service,
        CancellationToken ct)
    {
        var accommodations = await service.GetByEditionAsync(editionId, activeOnly, ct);
        return Results.Ok(ApiResponse<List<CampEditionAccommodationResponse>>.Ok(accommodations));
    }

    private static async Task<IResult> GetAccommodationById(
        Guid id,
        [FromServices] CampEditionAccommodationsService service,
        CancellationToken ct)
    {
        var accommodation = await service.GetByIdAsync(id, ct);
        return accommodation is not null
            ? Results.Ok(ApiResponse<CampEditionAccommodationResponse>.Ok(accommodation))
            : Results.NotFound(ApiResponse<CampEditionAccommodationResponse>.NotFound(
                $"Alojamiento con ID '{id}' no encontrado"));
    }

    private static async Task<IResult> CreateAccommodation(
        Guid editionId,
        CreateCampEditionAccommodationRequest request,
        [FromServices] CampEditionAccommodationsService service,
        CancellationToken ct)
    {
        try
        {
            var accommodation = await service.CreateAsync(editionId, request, ct);
            return Results.Created(
                $"/api/camps/editions/accommodations/{accommodation.Id}",
                ApiResponse<CampEditionAccommodationResponse>.Ok(accommodation));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(
                ApiResponse<CampEditionAccommodationResponse>.Fail(ex.Message, "OPERATION_ERROR"));
        }
    }

    private static async Task<IResult> UpdateAccommodation(
        Guid id,
        UpdateCampEditionAccommodationRequest request,
        [FromServices] CampEditionAccommodationsService service,
        CancellationToken ct)
    {
        try
        {
            var accommodation = await service.UpdateAsync(id, request, ct);
            return Results.Ok(ApiResponse<CampEditionAccommodationResponse>.Ok(accommodation));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(
                ApiResponse<CampEditionAccommodationResponse>.Fail(ex.Message, "OPERATION_ERROR"));
        }
    }

    private static async Task<IResult> DeleteAccommodation(
        Guid id,
        [FromServices] CampEditionAccommodationsService service,
        CancellationToken ct)
    {
        try
        {
            var deleted = await service.DeleteAsync(id, ct);
            return deleted
                ? Results.NoContent()
                : Results.NotFound(ApiResponse<object>.NotFound(
                    $"Alojamiento con ID '{id}' no encontrado"));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ApiResponse<object>.Fail(ex.Message, "OPERATION_ERROR"));
        }
    }

    private static async Task<IResult> ActivateAccommodation(
        Guid id,
        [FromServices] CampEditionAccommodationsService service,
        CancellationToken ct)
    {
        try
        {
            var accommodation = await service.ActivateAsync(id, ct);
            return Results.Ok(ApiResponse<CampEditionAccommodationResponse>.Ok(accommodation));
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
    }

    private static async Task<IResult> DeactivateAccommodation(
        Guid id,
        [FromServices] CampEditionAccommodationsService service,
        CancellationToken ct)
    {
        try
        {
            var accommodation = await service.DeactivateAsync(id, ct);
            return Results.Ok(ApiResponse<CampEditionAccommodationResponse>.Ok(accommodation));
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
    }
}
