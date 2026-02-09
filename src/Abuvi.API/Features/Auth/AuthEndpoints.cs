using Microsoft.AspNetCore.Mvc;
using Abuvi.API.Common.Models;
using Abuvi.API.Common.Filters;

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
    }

    /// <summary>
    /// Authenticates a user and returns a JWT token
    /// </summary>
    private static async Task<IResult> Login(
        [FromBody] LoginRequest request,
        AuthService authService,
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
        AuthService authService,
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
}
