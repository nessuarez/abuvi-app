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
            .Produces<ApiResponse<CampResponse>>()
            .Produces(401)
            .Produces(403)
            .Produces(404);

        // POST /api/camps - Create new camp
        group.MapPost("/", CreateCamp)
            .WithName("CreateCamp")
            .WithSummary("Create a new camp location")
            .AddEndpointFilter<ValidationFilter<CreateCampRequest>>()
            .Produces<ApiResponse<CampResponse>>(201)
            .Produces(400)
            .Produces(401)
            .Produces(403);

        // PUT /api/camps/{id} - Update existing camp
        group.MapPut("/{id:guid}", UpdateCamp)
            .WithName("UpdateCamp")
            .WithSummary("Update an existing camp location")
            .AddEndpointFilter<ValidationFilter<UpdateCampRequest>>()
            .Produces<ApiResponse<CampResponse>>()
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
            return Results.NotFound(ApiResponse<CampResponse>.NotFound("Camp not found"));
        }

        return Results.Ok(ApiResponse<CampResponse>.Ok(camp));
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
            return Results.Created($"/api/camps/{camp.Id}", ApiResponse<CampResponse>.Ok(camp));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ApiResponse<CampResponse>.Fail(ex.Message, "VALIDATION_ERROR"));
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
                return Results.NotFound(ApiResponse<CampResponse>.NotFound("Camp not found"));
            }

            return Results.Ok(ApiResponse<CampResponse>.Ok(camp));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ApiResponse<CampResponse>.Fail(ex.Message, "VALIDATION_ERROR"));
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
}
