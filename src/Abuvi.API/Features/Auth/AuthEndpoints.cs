using Microsoft.AspNetCore.Mvc;
using Abuvi.API.Common.Models;
using Abuvi.API.Common.Filters;
using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.Users;

namespace Abuvi.API.Features.Auth;

/// <summary>
/// Authentication endpoints for login and registration
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/login", Login)
            .AddEndpointFilter<ValidationFilter<LoginRequest>>()
            .WithName("Login")
            .Produces<ApiResponse<LoginResponse>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/register", Register)
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>()
            .WithName("Register")
            .Produces<ApiResponse<UserInfo>>()
            .Produces(StatusCodes.Status400BadRequest);

        // New registration workflow with email verification
        group.MapPost("/register-user", RegisterUser)
            .AddEndpointFilter<ValidationFilter<RegisterUserRequest>>()
            .WithName("RegisterUser")
            .Produces<ApiResponse<UserResponse>>()
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/verify-email", VerifyEmail)
            .AddEndpointFilter<ValidationFilter<VerifyEmailRequest>>()
            .WithName("VerifyEmail")
            .Produces<ApiResponse<object>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/resend-verification", ResendVerification)
            .AddEndpointFilter<ValidationFilter<ResendVerificationRequest>>()
            .WithName("ResendVerification")
            .Produces<ApiResponse<object>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Authenticates a user and returns a JWT token
    /// </summary>
    private static async Task<IResult> Login(
        [FromBody] LoginRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);

        if (result == null)
        {
            return Results.Json(
                ApiResponse<LoginResponse>.Fail(
                    "Invalid email or password",
                    "INVALID_CREDENTIALS"
                ),
                statusCode: 401
            );
        }

        return Results.Ok(ApiResponse<LoginResponse>.Ok(result));
    }

    /// <summary>
    /// Registers a new user with Member role
    /// </summary>
    private static async Task<IResult> Register(
        [FromBody] RegisterRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await authService.RegisterAsync(request, cancellationToken);
            return Results.Ok(ApiResponse<UserInfo>.Ok(user));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Json(
                ApiResponse<UserInfo>.Fail(ex.Message, "EMAIL_EXISTS"),
                statusCode: 400
            );
        }
    }

    /// <summary>
    /// Registers a new user with email verification workflow
    /// </summary>
    internal static async Task<IResult> RegisterUser(
        [FromBody] RegisterUserRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await authService.RegisterUserAsync(request, cancellationToken);
            return Results.Ok(ApiResponse<UserResponse>.Ok(user));
        }
        catch (BusinessRuleException ex)
        {
            var errorCode = ex.Message.Contains("email") ? "EMAIL_EXISTS" : "DOCUMENT_EXISTS";
            return Results.Json(
                ApiResponse<UserResponse>.Fail(ex.Message, errorCode),
                statusCode: 400
            );
        }
    }

    /// <summary>
    /// Verifies user email with token
    /// </summary>
    internal static async Task<IResult> VerifyEmail(
        [FromBody] VerifyEmailRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        try
        {
            await authService.VerifyEmailAsync(request.Token, cancellationToken);
            return Results.Ok(ApiResponse<object>.Ok(new { message = "Email verified successfully" }));
        }
        catch (NotFoundException ex)
        {
            return Results.Json(
                ApiResponse<object>.Fail(ex.Message, "NOT_FOUND"),
                statusCode: 404
            );
        }
        catch (BusinessRuleException ex)
        {
            return Results.Json(
                ApiResponse<object>.Fail(ex.Message, "VERIFICATION_FAILED"),
                statusCode: 400
            );
        }
    }

    /// <summary>
    /// Resends verification email to user
    /// </summary>
    internal static async Task<IResult> ResendVerification(
        [FromBody] ResendVerificationRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        try
        {
            await authService.ResendVerificationAsync(request.Email, cancellationToken);
            return Results.Ok(ApiResponse<object>.Ok(new { message = "Verification email sent" }));
        }
        catch (NotFoundException ex)
        {
            return Results.Json(
                ApiResponse<object>.Fail(ex.Message, "NOT_FOUND"),
                statusCode: 404
            );
        }
        catch (BusinessRuleException ex)
        {
            return Results.Json(
                ApiResponse<object>.Fail(ex.Message, "RESEND_FAILED"),
                statusCode: 400
            );
        }
    }
}
