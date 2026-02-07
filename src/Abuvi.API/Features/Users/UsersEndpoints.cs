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

        // GET /api/users - Get all users
        group.MapGet("/", GetAllUsers)
            .WithName("GetAllUsers")
            .WithSummary("Get all users")
            .Produces<ApiResponse<List<UserResponse>>>();

        // GET /api/users/{id} - Get user by ID
        group.MapGet("/{id:guid}", GetUserById)
            .WithName("GetUserById")
            .WithSummary("Get user by ID")
            .Produces<ApiResponse<UserResponse>>()
            .Produces(404);

        // POST /api/users - Create new user
        group.MapPost("/", CreateUser)
            .WithName("CreateUser")
            .WithSummary("Create a new user")
            .AddEndpointFilter<ValidationFilter<CreateUserRequest>>()
            .Produces<ApiResponse<UserResponse>>(201)
            .Produces(400)
            .Produces(409);

        // PUT /api/users/{id} - Update existing user
        group.MapPut("/{id:guid}", UpdateUser)
            .WithName("UpdateUser")
            .WithSummary("Update an existing user")
            .AddEndpointFilter<ValidationFilter<UpdateUserRequest>>()
            .Produces<ApiResponse<UserResponse>>()
            .Produces(400)
            .Produces(404);

        // DELETE /api/users/{id} - Delete user
        group.MapDelete("/{id:guid}", DeleteUser)
            .WithName("DeleteUser")
            .WithSummary("Delete a user")
            .Produces(204)
            .Produces(404);

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
}
