using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Abuvi.API.Common.Models;
using Abuvi.API.Common.Filters;
using Abuvi.API.Common.Extensions;

namespace Abuvi.API.Features.Memories;

public static class MemoriesEndpoints
{
    public static void MapMemoriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/memories")
            .WithTags("Memories")
            .RequireAuthorization();

        group.MapPost("/", CreateMemory)
            .AddEndpointFilter<ValidationFilter<CreateMemoryRequest>>()
            .WithName("CreateMemory")
            .Produces<ApiResponse<MemoryResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/", ListMemories)
            .WithName("ListMemories")
            .Produces<ApiResponse<IReadOnlyList<MemoryResponse>>>();

        group.MapGet("/{id:guid}", GetMemory)
            .WithName("GetMemory")
            .Produces<ApiResponse<MemoryResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/approve", ApproveMemory)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .WithName("ApproveMemory")
            .Produces<ApiResponse<MemoryResponse>>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/reject", RejectMemory)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .WithName("RejectMemory")
            .Produces<ApiResponse<MemoryResponse>>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateMemory(
        [FromBody] CreateMemoryRequest request,
        ClaimsPrincipal user,
        MemoriesService service,
        CancellationToken ct)
    {
        var userId = user.GetUserId()
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        var memory = await service.CreateAsync(userId, request, ct);
        return Results.Created(
            $"/api/memories/{memory.Id}",
            ApiResponse<MemoryResponse>.Ok(memory));
    }

    private static async Task<IResult> ListMemories(
        [FromQuery] int? year,
        [FromQuery] bool? approved,
        MemoriesService service,
        CancellationToken ct)
    {
        var memories = await service.GetListAsync(year, approved, ct);
        return Results.Ok(ApiResponse<IReadOnlyList<MemoryResponse>>.Ok(memories));
    }

    private static async Task<IResult> GetMemory(
        Guid id,
        MemoriesService service,
        CancellationToken ct)
    {
        var memory = await service.GetByIdAsync(id, ct);
        return Results.Ok(ApiResponse<MemoryResponse>.Ok(memory));
    }

    private static async Task<IResult> ApproveMemory(
        Guid id,
        MemoriesService service,
        CancellationToken ct)
    {
        var memory = await service.ApproveAsync(id, ct);
        return Results.Ok(ApiResponse<MemoryResponse>.Ok(memory));
    }

    private static async Task<IResult> RejectMemory(
        Guid id,
        MemoriesService service,
        CancellationToken ct)
    {
        var memory = await service.RejectAsync(id, ct);
        return Results.Ok(ApiResponse<MemoryResponse>.Ok(memory));
    }
}
