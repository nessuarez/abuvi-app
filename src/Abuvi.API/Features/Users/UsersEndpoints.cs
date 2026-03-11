using Abuvi.API.Common.Extensions;
using Abuvi.API.Common.Filters;
using Abuvi.API.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace Abuvi.API.Features.Users;

/// <summary>
/// Endpoints for user management (CRUD operations)
/// </summary>
public static class UsersEndpoints
{
    /// <summary>
    /// Maps all user-related endpoints to the application
    /// </summary>
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .WithOpenApi();

        // GET /api/users - Get all users (Admin/Board)
        group.MapGet("/", GetAllUsers)
            .WithName("GetAllUsers")
            .WithSummary("Get all users")
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .Produces<ApiResponse<List<UserResponse>>>()
            .Produces(401)
            .Produces(403);

        // GET /api/users/{id} - Get user by ID (Authenticated users)
        group.MapGet("/{id:guid}", GetUserById)
            .WithName("GetUserById")
            .WithSummary("Get user by ID")
            .RequireAuthorization()
            .Produces<ApiResponse<UserResponse>>()
            .Produces(401)
            .Produces(404);

        // POST /api/users - Create new user (Admin only)
        group.MapPost("/", CreateUser)
            .WithName("CreateUser")
            .WithSummary("Create a new user")
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .AddEndpointFilter<ValidationFilter<CreateUserRequest>>()
            .Produces<ApiResponse<UserResponse>>(201)
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(409);

        // PUT /api/users/{id} - Update existing user (Authenticated users)
        group.MapPut("/{id:guid}", UpdateUser)
            .WithName("UpdateUser")
            .WithSummary("Update an existing user")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<UpdateUserRequest>>()
            .Produces<ApiResponse<UserResponse>>()
            .Produces(400)
            .Produces(401)
            .Produces(404);

        // DELETE /api/users/{id} - Delete user (Admin only)
        group.MapDelete("/{id:guid}", DeleteUser)
            .WithName("DeleteUser")
            .WithSummary("Delete a user")
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .Produces(204)
            .Produces(401)
            .Produces(403)
            .Produces(404);

        // PATCH /api/users/{id}/role - Update user role (Admin/Board only)
        group.MapPatch("/{id:guid}/role", UpdateUserRole)
            .WithName("UpdateUserRole")
            .WithSummary("Update a user's role")
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .AddEndpointFilter<ValidationFilter<UpdateUserRoleRequest>>()
            .Produces<ApiResponse<UserResponse>>()
            .Produces(400)  // Bad Request - validation or self-change
            .Produces(401)  // Unauthorized - not authenticated
            .Produces(403)  // Forbidden - insufficient privileges
            .Produces(404); // Not Found - user doesn't exist

        return app;
    }

    /// <summary>
    /// Get all users with optional pagination
    /// </summary>
    private static async Task<IResult> GetAllUsers(
        [FromServices] UsersService service,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var users = await service.GetAllAsync(skip, take, cancellationToken);
        return Results.Ok(ApiResponse<List<UserResponse>>.Ok(users));
    }

    /// <summary>
    /// Get a user by their unique identifier
    /// </summary>
    private static async Task<IResult> GetUserById(
        [FromRoute] Guid id,
        [FromServices] UsersService service,
        CancellationToken cancellationToken = default)
    {
        var user = await service.GetByIdAsync(id, cancellationToken);

        if (user is null)
        {
            return Results.NotFound(
                ApiResponse<UserResponse>.NotFound($"User with ID {id} not found")
            );
        }

        return Results.Ok(ApiResponse<UserResponse>.Ok(user));
    }

    /// <summary>
    /// Create a new user
    /// </summary>
    private static async Task<IResult> CreateUser(
        [FromBody] CreateUserRequest request,
        [FromServices] UsersService service,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await service.CreateAsync(request, cancellationToken);
            return Results.Created($"/api/users/{user.Id}", ApiResponse<UserResponse>.Ok(user));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(
                ApiResponse<UserResponse>.Fail(ex.Message, "DUPLICATE_EMAIL")
            );
        }
    }

    /// <summary>
    /// Update an existing user
    /// </summary>
    private static async Task<IResult> UpdateUser(
        [FromRoute] Guid id,
        [FromBody] UpdateUserRequest request,
        [FromServices] UsersService service,
        CancellationToken cancellationToken = default)
    {
        var user = await service.UpdateAsync(id, request, cancellationToken);

        if (user is null)
        {
            return Results.NotFound(
                ApiResponse<UserResponse>.NotFound($"User with ID {id} not found")
            );
        }

        return Results.Ok(ApiResponse<UserResponse>.Ok(user));
    }

    /// <summary>
    /// Delete a user
    /// </summary>
    private static async Task<IResult> DeleteUser(
        [FromRoute] Guid id,
        [FromServices] UsersService service,
        CancellationToken cancellationToken = default)
    {
        var deleted = await service.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            return Results.NotFound(
                ApiResponse<object>.NotFound($"User with ID {id} not found")
            );
        }

        return Results.NoContent();
    }

    /// <summary>
    /// Update a user's role
    /// </summary>
    private static async Task<IResult> UpdateUserRole(
        [FromRoute] Guid id,
        [FromBody] UpdateUserRoleRequest request,
        [FromServices] UsersService service,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get requesting user ID from claims
            var requestingUserId = httpContext.User.GetUserId();
            if (requestingUserId is null)
                return Results.Unauthorized();

            // Get IP address for audit trail
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

            var user = await service.UpdateRoleAsync(
                id,
                request.NewRole,
                requestingUserId.Value,
                request.Reason,
                ipAddress,
                cancellationToken);

            if (user is null)
            {
                return Results.NotFound(
                    ApiResponse<UserResponse>.NotFound($"User with ID {id} not found")
                );
            }

            return Results.Ok(ApiResponse<UserResponse>.Ok(user));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(
                ApiResponse<UserResponse>.Fail(ex.Message, "INVALID_OPERATION")
            );
        }
        catch (UnauthorizedAccessException)
        {
            return Results.StatusCode(403); // Forbidden
        }
    }
}
