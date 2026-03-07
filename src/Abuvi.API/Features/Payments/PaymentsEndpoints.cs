using System.Security.Claims;
using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Extensions;
using Abuvi.API.Common.Filters;
using Abuvi.API.Common.Models;
using Abuvi.API.Features.Registrations;

namespace Abuvi.API.Features.Payments;

public static class PaymentsEndpoints
{
    public static IEndpointRouteBuilder MapPaymentsEndpoints(this IEndpointRouteBuilder app)
    {
        // User-facing endpoints
        var payments = app.MapGroup("/api/payments")
            .WithTags("Payments")
            .WithOpenApi()
            .RequireAuthorization();

        payments.MapGet("/{paymentId:guid}", GetPaymentById)
            .WithName("GetPaymentById")
            .WithSummary("Get a payment by ID")
            .Produces<ApiResponse<PaymentResponse>>()
            .Produces(403).Produces(404);

        payments.MapPost("/{paymentId:guid}/upload-proof", UploadProof)
            .WithName("UploadPaymentProof")
            .WithSummary("Upload proof of bank transfer")
            .DisableAntiforgery()
            .Produces<ApiResponse<PaymentResponse>>()
            .Produces(403).Produces(404).Produces(422);

        payments.MapDelete("/{paymentId:guid}/proof", RemoveProof)
            .WithName("RemovePaymentProof")
            .WithSummary("Remove uploaded proof")
            .Produces<ApiResponse<PaymentResponse>>()
            .Produces(403).Produces(404).Produces(422);

        // Registration payments
        var regPayments = app.MapGroup("/api/registrations")
            .WithTags("Payments")
            .WithOpenApi()
            .RequireAuthorization();

        regPayments.MapGet("/{registrationId:guid}/payments", GetRegistrationPayments)
            .WithName("GetRegistrationPayments")
            .WithSummary("Get all payments for a registration")
            .Produces<ApiResponse<List<PaymentResponse>>>()
            .Produces(403).Produces(404);

        // Admin endpoints
        var admin = app.MapGroup("/api/admin/payments")
            .WithTags("Payments Admin")
            .WithOpenApi()
            .RequireAuthorization();

        admin.MapGet("/", GetAllPayments)
            .WithName("GetAllPayments")
            .WithSummary("Get all payments with filters (admin)")
            .Produces<ApiResponse<object>>();

        admin.MapGet("/pending-review", GetPendingReview)
            .WithName("GetPendingReviewPayments")
            .WithSummary("Get payments awaiting review (admin)")
            .Produces<ApiResponse<List<AdminPaymentResponse>>>();

        admin.MapPost("/{paymentId:guid}/confirm", ConfirmPayment)
            .WithName("ConfirmPayment")
            .WithSummary("Confirm a payment (admin)")
            .Produces<ApiResponse<PaymentResponse>>()
            .Produces(403).Produces(404).Produces(422);

        admin.MapPost("/{paymentId:guid}/reject", RejectPayment)
            .WithName("RejectPayment")
            .WithSummary("Reject a payment (admin)")
            .AddEndpointFilter<ValidationFilter<RejectPaymentRequest>>()
            .Produces<ApiResponse<PaymentResponse>>()
            .Produces(403).Produces(404).Produces(422);

        // Payment settings
        var settings = app.MapGroup("/api/settings/payment")
            .WithTags("Payment Settings")
            .WithOpenApi();

        settings.MapGet("/", GetPaymentSettings)
            .WithName("GetPaymentSettings")
            .WithSummary("Get payment settings (public)")
            .Produces<ApiResponse<PaymentSettingsResponse>>();

        settings.MapPut("/", UpdatePaymentSettings)
            .WithName("UpdatePaymentSettings")
            .WithSummary("Update payment settings (admin)")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<PaymentSettingsRequest>>()
            .Produces<ApiResponse<PaymentSettingsResponse>>()
            .Produces(403);

        return app;
    }

    private static async Task<IResult> GetPaymentById(
        Guid paymentId,
        ClaimsPrincipal user,
        IPaymentsService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");
        var userRole = user.GetUserRole();

        try
        {
            var result = await service.GetByIdAsync(paymentId, userId, userRole, ct);
            return TypedResults.Ok(ApiResponse<PaymentResponse>.Ok(result));
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

    private static async Task<IResult> UploadProof(
        Guid paymentId,
        IFormFile file,
        ClaimsPrincipal user,
        IPaymentsService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");

        try
        {
            var result = await service.UploadProofAsync(paymentId, userId, file, ct);
            return TypedResults.Ok(ApiResponse<PaymentResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return TypedResults.UnprocessableEntity(
                ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE"));
        }
    }

    private static async Task<IResult> RemoveProof(
        Guid paymentId,
        ClaimsPrincipal user,
        IPaymentsService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");

        try
        {
            var result = await service.RemoveProofAsync(paymentId, userId, ct);
            return TypedResults.Ok(ApiResponse<PaymentResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return TypedResults.UnprocessableEntity(
                ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE"));
        }
    }

    private static async Task<IResult> GetRegistrationPayments(
        Guid registrationId,
        ClaimsPrincipal user,
        IPaymentsService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");
        var userRole = user.GetUserRole();

        try
        {
            var result = await service.GetByRegistrationAsync(registrationId, userId, userRole, ct);
            return TypedResults.Ok(ApiResponse<List<PaymentResponse>>.Ok(result));
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

    private static async Task<IResult> GetAllPayments(
        [AsParameters] PaymentFilterRequest filter,
        ClaimsPrincipal user,
        IPaymentsService service,
        CancellationToken ct)
    {
        var userRole = user.GetUserRole();
        if (userRole is not ("Admin" or "Board"))
            return TypedResults.Forbid();

        var (items, totalCount) = await service.GetAllPaymentsAsync(filter, ct);
        return TypedResults.Ok(ApiResponse<object>.Ok(new
        {
            Items = items,
            TotalCount = totalCount,
            filter.Page,
            filter.PageSize
        }));
    }

    private static async Task<IResult> GetPendingReview(
        ClaimsPrincipal user,
        IPaymentsService service,
        CancellationToken ct)
    {
        var userRole = user.GetUserRole();
        if (userRole is not ("Admin" or "Board"))
            return TypedResults.Forbid();

        var result = await service.GetPendingReviewAsync(ct);
        return TypedResults.Ok(ApiResponse<List<AdminPaymentResponse>>.Ok(result));
    }

    private static async Task<IResult> ConfirmPayment(
        Guid paymentId,
        ConfirmPaymentRequest? request,
        ClaimsPrincipal user,
        IPaymentsService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");
        var userRole = user.GetUserRole();

        if (userRole is not ("Admin" or "Board"))
            return TypedResults.Forbid();

        try
        {
            var result = await service.ConfirmPaymentAsync(paymentId, userId, request?.Notes, ct);
            return TypedResults.Ok(ApiResponse<PaymentResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return TypedResults.UnprocessableEntity(
                ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE"));
        }
    }

    private static async Task<IResult> RejectPayment(
        Guid paymentId,
        RejectPaymentRequest request,
        ClaimsPrincipal user,
        IPaymentsService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");
        var userRole = user.GetUserRole();

        if (userRole is not ("Admin" or "Board"))
            return TypedResults.Forbid();

        try
        {
            var result = await service.RejectPaymentAsync(paymentId, userId, request.Notes, ct);
            return TypedResults.Ok(ApiResponse<PaymentResponse>.Ok(result));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return TypedResults.UnprocessableEntity(
                ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE"));
        }
    }

    private static async Task<IResult> GetPaymentSettings(
        IPaymentsService service,
        CancellationToken ct)
    {
        var result = await service.GetPaymentSettingsAsync(ct);
        return TypedResults.Ok(ApiResponse<PaymentSettingsResponse>.Ok(result));
    }

    private static async Task<IResult> UpdatePaymentSettings(
        PaymentSettingsRequest request,
        ClaimsPrincipal user,
        IPaymentsService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("Usuario no autenticado");
        var userRole = user.GetUserRole();

        if (userRole is not ("Admin" or "Board"))
            return TypedResults.Forbid();

        var result = await service.UpdatePaymentSettingsAsync(request, userId, ct);
        return TypedResults.Ok(ApiResponse<PaymentSettingsResponse>.Ok(result));
    }
}
