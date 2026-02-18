namespace Abuvi.API.Features.GooglePlaces;

using Abuvi.API.Common.Models;

public static class GooglePlacesEndpoints
{
    public static void MapGooglePlacesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/places")
            .WithTags("Google Places")
            .RequireAuthorization(); // Only authenticated users

        group.MapPost("/autocomplete", SearchPlaces)
            .WithName("SearchPlaces")
            .Produces<ApiResponse<IReadOnlyList<PlaceAutocomplete>>>();

        group.MapPost("/details", GetPlaceDetails)
            .WithName("GetPlaceDetails")
            .Produces<ApiResponse<PlaceDetails>>();
    }

    private static async Task<IResult> SearchPlaces(
        AutocompleteRequest request,
        IGooglePlacesService service,
        CancellationToken ct)
    {
        try
        {
            var results = await service.SearchPlacesAsync(request.Input, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<PlaceAutocomplete>>.Ok(results));
        }
        catch (ExternalServiceException)
        {
            return Results.Json(
                ApiResponse<IReadOnlyList<PlaceAutocomplete>>.Fail(
                    "El servicio de ubicaciones no está disponible. Por favor intenta más tarde.",
                    "PLACES_SERVICE_UNAVAILABLE"
                ),
                statusCode: 503
            );
        }
    }

    private static async Task<IResult> GetPlaceDetails(
        PlaceDetailsRequest request,
        IGooglePlacesService service,
        CancellationToken ct)
    {
        try
        {
            var details = await service.GetPlaceDetailsAsync(request.PlaceId, ct);
            if (details == null)
            {
                return Results.NotFound(ApiResponse<PlaceDetails>.NotFound(
                    "No se encontró información para este lugar"
                ));
            }

            return Results.Ok(ApiResponse<PlaceDetails>.Ok(details));
        }
        catch (ExternalServiceException)
        {
            return Results.Json(
                ApiResponse<PlaceDetails>.Fail(
                    "El servicio de ubicaciones no está disponible. Por favor intenta más tarde.",
                    "PLACES_SERVICE_UNAVAILABLE"
                ),
                statusCode: 503
            );
        }
    }
}

public record AutocompleteRequest(string Input);
public record PlaceDetailsRequest(string PlaceId);
