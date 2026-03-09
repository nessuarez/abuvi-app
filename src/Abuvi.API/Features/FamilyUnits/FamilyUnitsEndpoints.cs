using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Extensions;
using Abuvi.API.Common.Filters;
using Abuvi.API.Common.Models;
using System.Security.Claims;

namespace Abuvi.API.Features.FamilyUnits;

/// <summary>
/// Endpoints for family units and family members management
/// </summary>
public static class FamilyUnitsEndpoints
{
    /// <summary>
    /// Maps all family unit related endpoints to the application
    /// </summary>
    public static IEndpointRouteBuilder MapFamilyUnitsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/family-units")
            .WithTags("Family Units")
            .WithOpenApi()
            .RequireAuthorization();

        // Board/Admin only group for administrative read operations
        var adminGroup = app.MapGroup("/api/family-units")
            .WithTags("Family Units")
            .WithOpenApi()
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

        adminGroup.MapGet("/", GetAllFamilyUnits)
            .WithName("GetAllFamilyUnits")
            .WithSummary("Get paginated list of all family units (Admin/Board only)")
            .Produces<ApiResponse<PagedFamilyUnitsResponse>>()
            .Produces(401)
            .Produces(403);

        adminGroup.MapPut("/{id:guid}/family-number", UpdateFamilyNumber)
            .WithName("UpdateFamilyNumber")
            .WithSummary("Update family number (Admin/Board only)")
            .AddEndpointFilter<ValidationFilter<UpdateFamilyNumberRequest>>()
            .Produces<ApiResponse<FamilyUnitResponse>>()
            .Produces(400)
            .Produces(404)
            .Produces(409);

        // Family Unit endpoints
        group.MapPost("/", CreateFamilyUnit)
            .WithName("CreateFamilyUnit")
            .WithSummary("Create a new family unit (one per user)")
            .AddEndpointFilter<ValidationFilter<CreateFamilyUnitRequest>>()
            .Produces<ApiResponse<FamilyUnitResponse>>(201)
            .Produces(400)
            .Produces(409);

        group.MapGet("/me", GetCurrentUserFamilyUnit)
            .WithName("GetCurrentUserFamilyUnit")
            .WithSummary("Get the family unit for the current user")
            .Produces<ApiResponse<FamilyUnitResponse>>()
            .Produces(404);

        group.MapGet("/{id:guid}", GetFamilyUnitById)
            .WithName("GetFamilyUnitById")
            .WithSummary("Get family unit by ID (representative or Admin/Board)")
            .Produces<ApiResponse<FamilyUnitResponse>>()
            .Produces(403)
            .Produces(404);

        group.MapPut("/{id:guid}", UpdateFamilyUnit)
            .WithName("UpdateFamilyUnit")
            .WithSummary("Update family unit (representative only)")
            .AddEndpointFilter<ValidationFilter<UpdateFamilyUnitRequest>>()
            .Produces<ApiResponse<FamilyUnitResponse>>()
            .Produces(400)
            .Produces(403)
            .Produces(404);

        group.MapDelete("/{id:guid}", DeleteFamilyUnit)
            .WithName("DeleteFamilyUnit")
            .WithSummary("Delete family unit and all members (representative only)")
            .Produces(204)
            .Produces(403)
            .Produces(404);

        // Family Member endpoints
        group.MapPost("/{familyUnitId:guid}/members", CreateFamilyMember)
            .WithName("CreateFamilyMember")
            .WithSummary("Add a new family member (representative only)")
            .AddEndpointFilter<ValidationFilter<CreateFamilyMemberRequest>>()
            .Produces<ApiResponse<FamilyMemberResponse>>(201)
            .Produces(400)
            .Produces(403)
            .Produces(404);

        group.MapGet("/{familyUnitId:guid}/members", GetFamilyMembers)
            .WithName("GetFamilyMembers")
            .WithSummary("Get all family members (representative or Admin/Board)")
            .Produces<ApiResponse<IReadOnlyList<FamilyMemberResponse>>>()
            .Produces(403)
            .Produces(404);

        group.MapGet("/{familyUnitId:guid}/members/{memberId:guid}", GetFamilyMemberById)
            .WithName("GetFamilyMemberById")
            .WithSummary("Get a single family member (representative or Admin/Board)")
            .Produces<ApiResponse<FamilyMemberResponse>>()
            .Produces(403)
            .Produces(404);

        group.MapPut("/{familyUnitId:guid}/members/{memberId:guid}", UpdateFamilyMember)
            .WithName("UpdateFamilyMember")
            .WithSummary("Update a family member (representative only)")
            .AddEndpointFilter<ValidationFilter<UpdateFamilyMemberRequest>>()
            .Produces<ApiResponse<FamilyMemberResponse>>()
            .Produces(400)
            .Produces(403)
            .Produces(404);

        group.MapDelete("/{familyUnitId:guid}/members/{memberId:guid}", DeleteFamilyMember)
            .WithName("DeleteFamilyMember")
            .WithSummary("Delete a family member (representative only, cannot delete own record)")
            .Produces(204)
            .Produces(403)
            .Produces(404)
            .Produces(409);

        // Profile photo endpoints — Family Member
        group.MapPut("/{familyUnitId:guid}/members/{memberId:guid}/profile-photo", UploadMemberProfilePhoto)
            .WithName("UploadMemberProfilePhoto")
            .WithSummary("Upload a profile photo for a family member")
            .DisableAntiforgery()
            .Produces<ApiResponse<FamilyMemberResponse>>()
            .Produces(400)
            .Produces(403)
            .Produces(404);

        group.MapDelete("/{familyUnitId:guid}/members/{memberId:guid}/profile-photo", RemoveMemberProfilePhoto)
            .WithName("RemoveMemberProfilePhoto")
            .WithSummary("Remove the profile photo of a family member")
            .Produces(204)
            .Produces(403)
            .Produces(404);

        // Profile photo endpoints — Family Unit
        group.MapPut("/{id:guid}/profile-photo", UploadUnitProfilePhoto)
            .WithName("UploadUnitProfilePhoto")
            .WithSummary("Upload a profile photo for a family unit")
            .DisableAntiforgery()
            .Produces<ApiResponse<FamilyUnitResponse>>()
            .Produces(400)
            .Produces(403)
            .Produces(404);

        group.MapDelete("/{id:guid}/profile-photo", RemoveUnitProfilePhoto)
            .WithName("RemoveUnitProfilePhoto")
            .WithSummary("Remove the profile photo of a family unit")
            .Produces(204)
            .Produces(403)
            .Produces(404);

        return app;
    }

    // Family Unit endpoint handlers

    private static async Task<IResult> CreateFamilyUnit(
        CreateFamilyUnitRequest request,
        FamilyUnitsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");

        try
        {
            var result = await service.CreateFamilyUnitAsync(userId, request, ct);
            return TypedResults.Created(
                $"/api/family-units/{result.Id}",
                ApiResponse<FamilyUnitResponse>.Ok(result));
        }
        catch (BusinessRuleException ex)
        {
            return TypedResults.Conflict(
                ApiResponse<object>.Fail(ex.Message, "FAMILY_UNIT_EXISTS"));
        }
    }

    private static async Task<IResult> GetCurrentUserFamilyUnit(
        FamilyUnitsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");

        try
        {
            var result = await service.GetCurrentUserFamilyUnitAsync(userId, ct);
            return TypedResults.Ok(ApiResponse<FamilyUnitResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
    }

    private static async Task<IResult> GetFamilyUnitById(
        Guid id,
        FamilyUnitsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");
        var userRole = user.GetUserRole();

        try
        {
            var result = await service.GetFamilyUnitByIdAsync(id, ct);

            // Authorization: Representative OR Admin/Board
            var isRepresentative = result.RepresentativeUserId == userId;
            var isAdminOrBoard = userRole == "Admin" || userRole == "Board";

            if (!isRepresentative && !isAdminOrBoard)
            {
                return TypedResults.Forbid();
            }

            return TypedResults.Ok(ApiResponse<FamilyUnitResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
    }

    private static async Task<IResult> UpdateFamilyUnit(
        Guid id,
        UpdateFamilyUnitRequest request,
        FamilyUnitsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");

        try
        {
            // Check authorization
            var isRepresentative = await service.IsRepresentativeAsync(id, userId, ct);
            if (!isRepresentative)
            {
                return TypedResults.Forbid();
            }

            var result = await service.UpdateFamilyUnitAsync(id, request, ct);
            return TypedResults.Ok(ApiResponse<FamilyUnitResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
    }

    private static async Task<IResult> DeleteFamilyUnit(
        Guid id,
        FamilyUnitsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");

        try
        {
            // Check authorization
            var isRepresentative = await service.IsRepresentativeAsync(id, userId, ct);
            if (!isRepresentative)
            {
                return TypedResults.Forbid();
            }

            await service.DeleteFamilyUnitAsync(id, ct);
            return TypedResults.NoContent();
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
    }

    // Family Member endpoint handlers

    private static async Task<IResult> CreateFamilyMember(
        Guid familyUnitId,
        CreateFamilyMemberRequest request,
        FamilyUnitsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");

        try
        {
            // Check authorization
            var isRepresentative = await service.IsRepresentativeAsync(familyUnitId, userId, ct);
            if (!isRepresentative)
            {
                return TypedResults.Forbid();
            }

            var result = await service.CreateFamilyMemberAsync(familyUnitId, request, ct);
            return TypedResults.Created(
                $"/api/family-units/{familyUnitId}/members/{result.Id}",
                ApiResponse<FamilyMemberResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
    }

    private static async Task<IResult> GetFamilyMembers(
        Guid familyUnitId,
        FamilyUnitsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");
        var userRole = user.GetUserRole();

        try
        {
            // Verify family unit exists and check authorization
            var familyUnit = await service.GetFamilyUnitByIdAsync(familyUnitId, ct);
            var isRepresentative = familyUnit.RepresentativeUserId == userId;
            var isAdminOrBoard = userRole == "Admin" || userRole == "Board";

            if (!isRepresentative && !isAdminOrBoard)
            {
                return TypedResults.Forbid();
            }

            var result = await service.GetFamilyMembersByFamilyUnitIdAsync(familyUnitId, ct);
            return TypedResults.Ok(ApiResponse<IReadOnlyList<FamilyMemberResponse>>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
    }

    private static async Task<IResult> GetFamilyMemberById(
        Guid familyUnitId,
        Guid memberId,
        FamilyUnitsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");
        var userRole = user.GetUserRole();

        try
        {
            // Verify family unit exists and check authorization
            var familyUnit = await service.GetFamilyUnitByIdAsync(familyUnitId, ct);
            var isRepresentative = familyUnit.RepresentativeUserId == userId;
            var isAdminOrBoard = userRole == "Admin" || userRole == "Board";

            if (!isRepresentative && !isAdminOrBoard)
            {
                return TypedResults.Forbid();
            }

            var result = await service.GetFamilyMemberByIdAsync(memberId, ct);
            return TypedResults.Ok(ApiResponse<FamilyMemberResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
    }

    private static async Task<IResult> UpdateFamilyMember(
        Guid familyUnitId,
        Guid memberId,
        UpdateFamilyMemberRequest request,
        FamilyUnitsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");

        try
        {
            // Check authorization
            var isRepresentative = await service.IsRepresentativeAsync(familyUnitId, userId, ct);
            if (!isRepresentative)
            {
                return TypedResults.Forbid();
            }

            var result = await service.UpdateFamilyMemberAsync(memberId, request, ct);
            return TypedResults.Ok(ApiResponse<FamilyMemberResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
    }

    private static async Task<IResult> GetAllFamilyUnits(
        FamilyUnitsService service,
        [Microsoft.AspNetCore.Mvc.FromQuery] int page = 1,
        [Microsoft.AspNetCore.Mvc.FromQuery] int pageSize = 20,
        [Microsoft.AspNetCore.Mvc.FromQuery] string? search = null,
        [Microsoft.AspNetCore.Mvc.FromQuery] string? sortBy = null,
        [Microsoft.AspNetCore.Mvc.FromQuery] string? sortOrder = null,
        [Microsoft.AspNetCore.Mvc.FromQuery] string? membershipStatus = null,
        CancellationToken ct = default)
    {
        var result = await service.GetAllFamilyUnitsAsync(page, pageSize, search, sortBy, sortOrder, membershipStatus, ct);
        return TypedResults.Ok(ApiResponse<PagedFamilyUnitsResponse>.Ok(result));
    }

    private static async Task<IResult> UpdateFamilyNumber(
        [Microsoft.AspNetCore.Mvc.FromRoute] Guid id,
        [Microsoft.AspNetCore.Mvc.FromBody] UpdateFamilyNumberRequest request,
        FamilyUnitsService service,
        CancellationToken ct)
    {
        try
        {
            var result = await service.UpdateFamilyNumberAsync(id, request, ct);
            return TypedResults.Ok(ApiResponse<FamilyUnitResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return TypedResults.Conflict(ApiResponse<object>.Fail(ex.Message, "DUPLICATE_FAMILY_NUMBER"));
        }
    }

    private static async Task<IResult> DeleteFamilyMember(
        Guid familyUnitId,
        Guid memberId,
        FamilyUnitsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");

        try
        {
            // Check authorization
            var isRepresentative = await service.IsRepresentativeAsync(familyUnitId, userId, ct);
            if (!isRepresentative)
            {
                return TypedResults.Forbid();
            }

            await service.DeleteFamilyMemberAsync(memberId, ct);
            return TypedResults.NoContent();
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return TypedResults.Conflict(
                ApiResponse<object>.Fail(ex.Message, "CANNOT_DELETE_REPRESENTATIVE"));
        }
    }

    // Profile photo endpoint handlers

    private static async Task<IResult> UploadMemberProfilePhoto(
        Guid familyUnitId,
        Guid memberId,
        IFormFile file,
        FamilyUnitsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");
        var isAdmin = user.IsInRole("Admin");

        try
        {
            var result = await service.UploadFamilyMemberProfilePhotoAsync(
                familyUnitId, memberId, userId, isAdmin, file, ct);
            return TypedResults.Ok(ApiResponse<FamilyMemberResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return TypedResults.BadRequest(ApiResponse<object>.Fail(ex.Message, "VALIDATION_ERROR"));
        }
    }

    private static async Task<IResult> RemoveMemberProfilePhoto(
        Guid familyUnitId,
        Guid memberId,
        FamilyUnitsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");
        var isAdmin = user.IsInRole("Admin");

        try
        {
            await service.RemoveFamilyMemberProfilePhotoAsync(
                familyUnitId, memberId, userId, isAdmin, ct);
            return TypedResults.NoContent();
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return TypedResults.BadRequest(ApiResponse<object>.Fail(ex.Message, "VALIDATION_ERROR"));
        }
    }

    private static async Task<IResult> UploadUnitProfilePhoto(
        [Microsoft.AspNetCore.Mvc.FromRoute(Name = "id")] Guid familyUnitId,
        IFormFile file,
        FamilyUnitsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");
        var isAdmin = user.IsInRole("Admin");

        try
        {
            var result = await service.UploadFamilyUnitProfilePhotoAsync(
                familyUnitId, userId, isAdmin, file, ct);
            return TypedResults.Ok(ApiResponse<FamilyUnitResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return TypedResults.BadRequest(ApiResponse<object>.Fail(ex.Message, "VALIDATION_ERROR"));
        }
    }

    private static async Task<IResult> RemoveUnitProfilePhoto(
        [Microsoft.AspNetCore.Mvc.FromRoute(Name = "id")] Guid familyUnitId,
        FamilyUnitsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");
        var isAdmin = user.IsInRole("Admin");

        try
        {
            await service.RemoveFamilyUnitProfilePhotoAsync(
                familyUnitId, userId, isAdmin, ct);
            return TypedResults.NoContent();
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return TypedResults.BadRequest(ApiResponse<object>.Fail(ex.Message, "VALIDATION_ERROR"));
        }
    }
}
