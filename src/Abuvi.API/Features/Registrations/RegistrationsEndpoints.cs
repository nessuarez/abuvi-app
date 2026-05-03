using System.Security.Claims;
using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Filters;
using Abuvi.API.Common.Models;
using Abuvi.API.Common.Extensions;
using Abuvi.API.Features.Camps;
using Microsoft.AspNetCore.Mvc;

namespace Abuvi.API.Features.Registrations;

public static class RegistrationsEndpoints
{
    public static IEndpointRouteBuilder MapRegistrationsEndpoints(this IEndpointRouteBuilder app)
    {
        // Available camp editions — any authenticated user
        var campsGroup = app.MapGroup("/api/camps/editions")
            .WithTags("Camp Editions")
            .WithOpenApi()
            .RequireAuthorization();

        campsGroup.MapGet("/available", GetAvailableEditions)
            .WithName("GetAvailableEditions")
            .WithSummary("Get open camp editions available for registration")
            .Produces<ApiResponse<List<AvailableCampEditionResponse>>>();

        // Registrations
        var group = app.MapGroup("/api/registrations")
            .WithTags("Registrations")
            .WithOpenApi()
            .RequireAuthorization();

        group.MapGet("/", GetMyRegistrations)
            .WithName("GetMyRegistrations")
            .WithSummary("Get registrations for the current user's family")
            .Produces<ApiResponse<List<RegistrationListResponse>>>();

        group.MapGet("/{id:guid}", GetRegistrationById)
            .WithName("GetRegistrationById")
            .WithSummary("Get registration detail with full pricing breakdown")
            .Produces<ApiResponse<RegistrationResponse>>()
            .Produces(403).Produces(404);

        group.MapPost("/", CreateRegistration)
            .WithName("CreateRegistration")
            .WithSummary("Register a family for a camp edition (representative only)")
            .AddEndpointFilter<ValidationFilter<CreateRegistrationRequest>>()
            .Produces<ApiResponse<RegistrationResponse>>(201)
            .Produces(400).Produces(403).Produces(409);

        group.MapPut("/{id:guid}/members", UpdateRegistrationMembers)
            .WithName("UpdateRegistrationMembers")
            .WithSummary("Update attending family members (representative only)")
            .AddEndpointFilter<ValidationFilter<UpdateRegistrationMembersRequest>>()
            .Produces<ApiResponse<RegistrationResponse>>()
            .Produces(400).Produces(403).Produces(404).Produces(422);

        group.MapPost("/{id:guid}/extras", SetRegistrationExtras)
            .WithName("SetRegistrationExtras")
            .WithSummary("Set extras selection (representative only)")
            .AddEndpointFilter<ValidationFilter<UpdateRegistrationExtrasRequest>>()
            .Produces<ApiResponse<RegistrationResponse>>()
            .Produces(400).Produces(403).Produces(404).Produces(422);

        group.MapPatch("/{id:guid}/info", UpdateRegistrationInfo)
            .WithName("UpdateRegistrationInfo")
            .WithSummary("Update special needs and pet flag (representative only)")
            .Produces<ApiResponse<RegistrationResponse>>()
            .Produces(403).Produces(404).Produces(422);

        group.MapPost("/{id:guid}/cancel", CancelRegistration)
            .WithName("CancelRegistration")
            .WithSummary("Cancel registration (representative or Admin/Board)")
            .Produces<ApiResponse<CancelRegistrationResponse>>()
            .Produces(403).Produces(404).Produces(422);

        group.MapPost("/{id:guid}/confirm-changes", ConfirmRegistrationChanges)
            .WithName("ConfirmRegistrationChanges")
            .WithSummary("Confirm pending Draft changes (own registration or Admin/Board force-confirm)")
            .Produces<ApiResponse<RegistrationResponse>>()
            .Produces(401).Produces(403).Produces(404).Produces(422);

        group.MapDelete("/{id:guid}", DeleteRegistration)
            .WithName("DeleteRegistration")
            .WithSummary("Permanently delete a registration (representative within 24h or Admin/Board)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status422UnprocessableEntity);

        group.MapPut("/{id:guid}/accommodation-preferences", SetAccommodationPreferences)
            .WithName("SetAccommodationPreferences")
            .WithSummary("Set accommodation preferences ranked 1-3 (representative or Admin/Board)")
            .AddEndpointFilter<ValidationFilter<UpdateRegistrationAccommodationPreferencesRequest>>()
            .Produces<ApiResponse<List<AccommodationPreferenceResponse>>>()
            .Produces(400).Produces(403).Produces(404).Produces(422);

        group.MapGet("/{id:guid}/accommodation-preferences", GetAccommodationPreferences)
            .WithName("GetAccommodationPreferences")
            .WithSummary("Get accommodation preferences for a registration")
            .Produces<ApiResponse<List<AccommodationPreferenceResponse>>>()
            .Produces(404);

        // Admin endpoints — Board and Admin only
        var adminListGroup = app.MapGroup("/api/camp-editions/{campEditionId:guid}/registrations")
            .WithTags("Registrations Admin")
            .WithOpenApi()
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

        adminListGroup.MapGet("/export/csv", ExportRegistrationsToCsv)
            .WithName("ExportRegistrationsToCsv")
            .WithSummary("Export registrations for a camp edition as CSV (Admin/Board only)")
            .Produces(200)
            .Produces(401).Produces(403).Produces(404);

        adminListGroup.MapGet("/", GetAdminRegistrations)
            .WithName("GetAdminRegistrations")
            .WithSummary("Get paginated registrations for a camp edition (Admin/Board only)")
            .Produces<ApiResponse<AdminRegistrationListResponse>>()
            .Produces(401).Produces(403).Produces(404);

        var adminEditGroup = app.MapGroup("/api/registrations")
            .WithTags("Registrations Admin")
            .WithOpenApi()
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

        adminEditGroup.MapPut("/{id:guid}/admin-edit", AdminEditRegistration)
            .WithName("AdminEditRegistration")
            .WithSummary("Edit registration as Admin/Board (sets status to Draft)")
            .AddEndpointFilter<ValidationFilter<AdminEditRegistrationRequest>>()
            .Produces<ApiResponse<RegistrationResponse>>()
            .Produces(400).Produces(401).Produces(403).Produces(404).Produces(422);

        adminEditGroup.MapPatch("/{id:guid}/status", ChangeRegistrationStatus)
            .WithName("ChangeRegistrationStatus")
            .WithSummary("Change registration status manually (Admin/Board only)")
            .AddEndpointFilter<ValidationFilter<ChangeRegistrationStatusRequest>>()
            .Produces<ApiResponse<RegistrationResponse>>()
            .Produces(400).Produces(401).Produces(403).Produces(404).Produces(422);

        return app;
    }

    private static async Task<IResult> GetAvailableEditions(
        RegistrationsService service, CancellationToken ct)
    {
        var result = await service.GetAvailableEditionsAsync(ct);
        return TypedResults.Ok(ApiResponse<List<AvailableCampEditionResponse>>.Ok(result));
    }

    private static async Task<IResult> GetMyRegistrations(
        RegistrationsService service, ClaimsPrincipal user, CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");
        var result = await service.GetByFamilyUnitAsync(userId, ct);
        return TypedResults.Ok(ApiResponse<List<RegistrationListResponse>>.Ok(result));
    }

    private static async Task<IResult> GetRegistrationById(
        Guid id, RegistrationsService service, ClaimsPrincipal user, CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");
        var userRole = user.GetUserRole();
        var isAdminOrBoard = userRole is "Admin" or "Board";

        try
        {
            var result = await service.GetByIdAsync(id, userId, isAdminOrBoard, ct);
            return TypedResults.Ok(ApiResponse<RegistrationResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (BusinessRuleException)
        {
            return TypedResults.Forbid();
        }
    }

    private static async Task<IResult> CreateRegistration(
        CreateRegistrationRequest request,
        RegistrationsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");

        try
        {
            var result = await service.CreateAsync(userId, request, ct);
            return TypedResults.Created(
                $"/api/registrations/{result.Id}",
                ApiResponse<RegistrationResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (BusinessRuleException ex) when (
            ex.Message.Contains("Ya existe") || ex.Message.Contains("capacidad"))
        {
            return TypedResults.Conflict(ApiResponse<object>.Fail(ex.Message, "REGISTRATION_CONFLICT"));
        }
        catch (BusinessRuleException ex)
        {
            return TypedResults.UnprocessableEntity(
                ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE_VIOLATION"));
        }
    }

    private static async Task<IResult> UpdateRegistrationInfo(
        Guid id,
        UpdateRegistrationInfoRequest request,
        RegistrationsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");

        try
        {
            var result = await service.UpdateInfoAsync(id, userId, request, ct);
            return TypedResults.Ok(ApiResponse<RegistrationResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return TypedResults.UnprocessableEntity(
                ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE_VIOLATION"));
        }
    }

    private static async Task<IResult> UpdateRegistrationMembers(
        Guid id,
        UpdateRegistrationMembersRequest request,
        RegistrationsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");

        try
        {
            var result = await service.UpdateMembersAsync(id, userId, request, ct);
            return TypedResults.Ok(ApiResponse<RegistrationResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return TypedResults.UnprocessableEntity(
                ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE_VIOLATION"));
        }
    }

    private static async Task<IResult> SetRegistrationExtras(
        Guid id,
        UpdateRegistrationExtrasRequest request,
        RegistrationsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");

        try
        {
            var result = await service.SetExtrasAsync(id, userId, request, ct);
            return TypedResults.Ok(ApiResponse<RegistrationResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return TypedResults.UnprocessableEntity(
                ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE_VIOLATION"));
        }
    }

    private static async Task<IResult> CancelRegistration(
        Guid id,
        RegistrationsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");
        var userRole = user.GetUserRole();
        var isAdminOrBoard = userRole is "Admin" or "Board";

        try
        {
            var result = await service.CancelAsync(id, userId, isAdminOrBoard, ct);
            return TypedResults.Ok(ApiResponse<CancelRegistrationResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return TypedResults.UnprocessableEntity(
                ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE_VIOLATION"));
        }
    }

    private static async Task<IResult> DeleteRegistration(
        Guid id,
        RegistrationsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");
        var userRole = user.GetUserRole();
        var isAdminOrBoard = userRole is "Admin" or "Board";

        try
        {
            await service.DeleteAsync(id, userId, isAdminOrBoard, ct);
            return Results.NoContent();
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return TypedResults.Forbid();
        }
        catch (BusinessRuleException ex)
        {
            if (ex.Message.Contains("payments", StringComparison.OrdinalIgnoreCase))
                return TypedResults.Conflict(ApiResponse<object>.Fail(ex.Message, "REGISTRATION_HAS_PAYMENTS"));

            return TypedResults.UnprocessableEntity(ApiResponse<object>.Fail(ex.Message, "REGISTRATION_DELETE_BLOCKED"));
        }
    }

    private static async Task<IResult> SetAccommodationPreferences(
        Guid id,
        UpdateRegistrationAccommodationPreferencesRequest request,
        RegistrationsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");
        var userRole = user.GetUserRole();
        var isAdminOrBoard = userRole is "Admin" or "Board";

        try
        {
            var result = await service.SetAccommodationPreferencesAsync(
                id, userId, isAdminOrBoard, request, ct);
            return TypedResults.Ok(ApiResponse<List<AccommodationPreferenceResponse>>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return TypedResults.UnprocessableEntity(
                ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE_VIOLATION"));
        }
    }

    private static async Task<IResult> GetAccommodationPreferences(
        Guid id,
        RegistrationsService service,
        CancellationToken ct)
    {
        try
        {
            var result = await service.GetAccommodationPreferencesAsync(id, ct);
            return TypedResults.Ok(ApiResponse<List<AccommodationPreferenceResponse>>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
    }

    private static async Task<IResult> GetAdminRegistrations(
        Guid campEditionId,
        RegistrationsService service,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid[]? accommodationIds = null,
        [FromQuery] int[]? accommodationPreferenceOrders = null,
        [FromQuery] Guid[]? extraIds = null,
        [FromQuery] string[]? attendancePeriods = null,
        [FromQuery] string[]? ageCategories = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken ct = default)
    {
        try
        {
            var accommodationPreferences = BuildAccommodationPreferences(accommodationIds, accommodationPreferenceOrders);

            var parsedAttendancePeriods = attendancePeriods?
                .Select(p => Enum.TryParse<AttendancePeriod>(p, true, out var parsed) ? parsed : (AttendancePeriod?)null)
                .Where(p => p.HasValue).Select(p => p!.Value).Distinct().ToList();

            var parsedAgeCategories = ageCategories?
                .Select(c => Enum.TryParse<AgeCategory>(c, true, out var parsed) ? parsed : (AgeCategory?)null)
                .Where(c => c.HasValue).Select(c => c!.Value).Distinct().ToList();

            var parsedSortBy = sortBy?.ToLowerInvariant() == "familyname"
                ? AdminRegistrationSortBy.FamilyName
                : AdminRegistrationSortBy.CreatedAt;

            var sortDescending = sortDirection?.ToLowerInvariant() != "asc";

            var result = await service.GetAdminListAsync(
                campEditionId, page, pageSize, search, status,
                accommodationPreferences,
                extraIds?.Distinct().ToList(),
                parsedAttendancePeriods?.Count > 0 ? parsedAttendancePeriods : null,
                parsedAgeCategories?.Count > 0 ? parsedAgeCategories : null,
                parsedSortBy, sortDescending,
                ct);
            return TypedResults.Ok(ApiResponse<AdminRegistrationListResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
    }

    private static async Task<IResult> ExportRegistrationsToCsv(
        Guid campEditionId,
        RegistrationsService service,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid[]? accommodationIds = null,
        [FromQuery] int[]? accommodationPreferenceOrders = null,
        [FromQuery] Guid[]? extraIds = null,
        [FromQuery] string[]? attendancePeriods = null,
        [FromQuery] string[]? ageCategories = null,
        CancellationToken ct = default)
    {
        try
        {
            var accommodationPreferences = BuildAccommodationPreferences(accommodationIds, accommodationPreferenceOrders);

            var parsedAttendancePeriods = attendancePeriods?
                .Select(p => Enum.TryParse<AttendancePeriod>(p, true, out var parsed) ? parsed : (AttendancePeriod?)null)
                .Where(p => p.HasValue).Select(p => p!.Value).Distinct().ToList();

            var parsedAgeCategories = ageCategories?
                .Select(c => Enum.TryParse<AgeCategory>(c, true, out var parsed) ? parsed : (AgeCategory?)null)
                .Where(c => c.HasValue).Select(c => c!.Value).Distinct().ToList();

            var (content, fileName) = await service.ExportToCsvAsync(
                campEditionId, search, status,
                accommodationPreferences,
                extraIds?.Distinct().ToList(),
                parsedAttendancePeriods?.Count > 0 ? parsedAttendancePeriods : null,
                parsedAgeCategories?.Count > 0 ? parsedAgeCategories : null,
                ct);

            return Results.File(
                content,
                contentType: "text/csv; charset=utf-8",
                fileDownloadName: fileName);
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
    }

    private static List<AccommodationPreferenceFilter>? BuildAccommodationPreferences(
        Guid[]? accommodationIds,
        int[]? preferenceOrders)
    {
        if (accommodationIds is not { Length: > 0 }) return null;
        if (preferenceOrders?.Length != accommodationIds.Length) return null;

        return accommodationIds
            .Zip(preferenceOrders, (id, order) => new AccommodationPreferenceFilter(id, order))
            .ToList();
    }

    private static async Task<IResult> AdminEditRegistration(
        Guid id,
        AdminEditRegistrationRequest request,
        RegistrationsService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var adminUserId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");

        try
        {
            var result = await service.AdminUpdateAsync(id, adminUserId, request, ct);
            return TypedResults.Ok(ApiResponse<RegistrationResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return TypedResults.UnprocessableEntity(
                ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE_VIOLATION"));
        }
    }

    private static async Task<IResult> ChangeRegistrationStatus(
        Guid id, ChangeRegistrationStatusRequest request,
        RegistrationsService service, ClaimsPrincipal user, CancellationToken ct)
    {
        var adminUserId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");

        try
        {
            var result = await service.ChangeStatusAsync(id, adminUserId, request, ct);
            return TypedResults.Ok(ApiResponse<RegistrationResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return TypedResults.UnprocessableEntity(
                ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE_VIOLATION"));
        }
    }

    private static async Task<IResult> ConfirmRegistrationChanges(
        Guid id, RegistrationsService service, ClaimsPrincipal user, CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");
        var userRole = user.GetUserRole();
        var isAdminOrBoard = userRole is "Admin" or "Board";

        try
        {
            var result = await service.ConfirmChangesAsync(id, userId, isAdminOrBoard, ct);
            return TypedResults.Ok(ApiResponse<RegistrationResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return TypedResults.UnprocessableEntity(
                ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE_VIOLATION"));
        }
        catch (UnauthorizedAccessException)
        {
            return TypedResults.Forbid();
        }
    }
}
