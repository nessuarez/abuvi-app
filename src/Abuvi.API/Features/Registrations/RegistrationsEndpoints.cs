using System.Security.Claims;
using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Filters;
using Abuvi.API.Common.Models;
using Abuvi.API.Common.Extensions;
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

        group.MapPost("/{id:guid}/cancel", CancelRegistration)
            .WithName("CancelRegistration")
            .WithSummary("Cancel registration (representative or Admin/Board)")
            .Produces<ApiResponse<CancelRegistrationResponse>>()
            .Produces(403).Produces(404).Produces(422);

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
}
