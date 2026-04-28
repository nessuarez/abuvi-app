using Abuvi.API.Common.Filters;
using Abuvi.API.Common.Models;

namespace Abuvi.API.Features.Camps;

public static class AccommodationFeaturesEndpoints
{
    public static void MapAccommodationFeaturesEndpoints(this IEndpointRouteBuilder app)
    {
        var catalogue = app.MapGroup("/api/accommodation-features")
            .WithTags("AccommodationFeatures")
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

        catalogue.MapGet("/", GetAll).WithName("GetAllAccommodationFeatures")
            .Produces<ApiResponse<IReadOnlyList<AccommodationFeatureResponse>>>();

        catalogue.MapGet("/{id:guid}", GetById).WithName("GetAccommodationFeatureById")
            .Produces<ApiResponse<AccommodationFeatureResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        catalogue.MapPost("/", Create).WithName("CreateAccommodationFeature")
            .Produces<ApiResponse<AccommodationFeatureResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .AddEndpointFilter<ValidationFilter<CreateAccommodationFeatureRequest>>();

        catalogue.MapPut("/{id:guid}", Update).WithName("UpdateAccommodationFeature")
            .Produces<ApiResponse<AccommodationFeatureResponse>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .AddEndpointFilter<ValidationFilter<UpdateAccommodationFeatureRequest>>();

        catalogue.MapDelete("/{id:guid}", Delete).WithName("DeleteAccommodationFeature")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        var accommodationFeatures = app.MapGroup(
                "/api/camps/editions/{editionId:guid}/accommodations/{accommodationId:guid}/features")
            .WithTags("AccommodationFeatures")
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

        accommodationFeatures.MapGet("/", GetAccommodationFeatures)
            .WithName("GetAccommodationFeatureAssignments")
            .Produces<ApiResponse<IReadOnlyList<AccommodationFeatureResponse>>>();

        accommodationFeatures.MapPut("/", SetAccommodationFeatures)
            .WithName("SetAccommodationFeatureAssignments")
            .Produces<ApiResponse<IReadOnlyList<AccommodationFeatureResponse>>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .AddEndpointFilter<ValidationFilter<SetFeatureAssignmentsRequest>>();

        var zoneFeatures = app.MapGroup(
                "/api/camps/editions/{editionId:guid}/accommodation-zones/{zoneId:guid}/features")
            .WithTags("AccommodationFeatures")
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

        zoneFeatures.MapGet("/", GetZoneFeatures)
            .WithName("GetZoneFeatureAssignments")
            .Produces<ApiResponse<IReadOnlyList<AccommodationFeatureResponse>>>();

        zoneFeatures.MapPut("/", SetZoneFeatures)
            .WithName("SetZoneFeatureAssignments")
            .Produces<ApiResponse<IReadOnlyList<AccommodationFeatureResponse>>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .AddEndpointFilter<ValidationFilter<SetFeatureAssignmentsRequest>>();
    }

    private static async Task<IResult> GetAll(
        AccommodationFeaturesService service, bool? activeOnly, CancellationToken ct)
    {
        var features = await service.GetAllAsync(activeOnly, ct);
        return Results.Ok(ApiResponse<IReadOnlyList<AccommodationFeatureResponse>>.Ok(features));
    }

    private static async Task<IResult> GetById(
        Guid id, AccommodationFeaturesService service, CancellationToken ct)
        => Results.Ok(ApiResponse<AccommodationFeatureResponse>.Ok(
            await service.GetByIdAsync(id, ct)));

    private static async Task<IResult> Create(
        CreateAccommodationFeatureRequest request,
        AccommodationFeaturesService service, CancellationToken ct)
    {
        var feature = await service.CreateAsync(request, ct);
        return Results.Created($"/api/accommodation-features/{feature.Id}",
            ApiResponse<AccommodationFeatureResponse>.Ok(feature));
    }

    private static async Task<IResult> Update(
        Guid id, UpdateAccommodationFeatureRequest request,
        AccommodationFeaturesService service, CancellationToken ct)
        => Results.Ok(ApiResponse<AccommodationFeatureResponse>.Ok(
            await service.UpdateAsync(id, request, ct)));

    private static async Task<IResult> Delete(
        Guid id, AccommodationFeaturesService service, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetAccommodationFeatures(
        Guid accommodationId, IAccommodationFeaturesRepository repo, CancellationToken ct)
    {
        var features = await repo.GetForAccommodationAsync(accommodationId, ct);
        return Results.Ok(ApiResponse<IReadOnlyList<AccommodationFeatureResponse>>.Ok(
            features.Select(f => f.ToResponse()).ToList().AsReadOnly()));
    }

    private static async Task<IResult> SetAccommodationFeatures(
        Guid accommodationId, SetFeatureAssignmentsRequest request,
        AccommodationFeatureAssignmentService service, CancellationToken ct)
        => Results.Ok(ApiResponse<IReadOnlyList<AccommodationFeatureResponse>>.Ok(
            await service.SetAccommodationFeaturesAsync(accommodationId, request, ct)));

    private static async Task<IResult> GetZoneFeatures(
        Guid zoneId, IAccommodationFeaturesRepository repo, CancellationToken ct)
    {
        var features = await repo.GetForZoneAsync(zoneId, ct);
        return Results.Ok(ApiResponse<IReadOnlyList<AccommodationFeatureResponse>>.Ok(
            features.Select(f => f.ToResponse()).ToList().AsReadOnly()));
    }

    private static async Task<IResult> SetZoneFeatures(
        Guid zoneId, SetFeatureAssignmentsRequest request,
        AccommodationFeatureAssignmentService service, CancellationToken ct)
        => Results.Ok(ApiResponse<IReadOnlyList<AccommodationFeatureResponse>>.Ok(
            await service.SetZoneFeaturesAsync(zoneId, request, ct)));
}
