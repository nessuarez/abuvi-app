namespace Abuvi.API.Features.GooglePlaces;

public interface IGooglePlacesService
{
    Task<IReadOnlyList<PlaceAutocomplete>> SearchPlacesAsync(string input, CancellationToken ct);
    Task<PlaceDetails?> GetPlaceDetailsAsync(string placeId, CancellationToken ct);
}

public class GooglePlacesService(HttpClient httpClient, IConfiguration configuration, ILogger<GooglePlacesService> logger) : IGooglePlacesService
{
    private readonly string _apiKey = configuration["GooglePlaces:ApiKey"]
        ?? throw new InvalidOperationException("GooglePlaces:ApiKey is required");
    private readonly string _autocompleteUrl = configuration["GooglePlaces:AutocompleteUrl"]
        ?? "https://maps.googleapis.com/maps/api/place/autocomplete/json";
    private readonly string _detailsUrl = configuration["GooglePlaces:DetailsUrl"]
        ?? "https://maps.googleapis.com/maps/api/place/details/json";

    public async Task<IReadOnlyList<PlaceAutocomplete>> SearchPlacesAsync(string input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Array.Empty<PlaceAutocomplete>();

        var url = $"{_autocompleteUrl}?input={Uri.EscapeDataString(input)}&key={_apiKey}&language=es&components=country:es";

        try
        {
            var response = await httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GoogleAutocompleteResponse>(ct);
            if (result?.Predictions == null)
                return Array.Empty<PlaceAutocomplete>();

            return result.Predictions.Select(p => new PlaceAutocomplete(
                p.PlaceId,
                p.Description,
                p.StructuredFormatting.MainText,
                p.StructuredFormatting.SecondaryText
            )).ToList();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to call Google Places Autocomplete API");
            throw new ExternalServiceException("Google Places Autocomplete API is unavailable");
        }
    }

    public async Task<PlaceDetails?> GetPlaceDetailsAsync(string placeId, CancellationToken ct)
    {
        var fields = "place_id,name,formatted_address,geometry,types";
        var url = $"{_detailsUrl}?place_id={Uri.EscapeDataString(placeId)}&key={_apiKey}&language=es&fields={fields}";

        try
        {
            var response = await httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GooglePlaceDetailsResponse>(ct);
            if (result?.Result == null)
                return null;

            var place = result.Result;
            return new PlaceDetails(
                place.PlaceId,
                place.Name,
                place.FormattedAddress,
                (decimal)place.Geometry.Location.Lat,
                (decimal)place.Geometry.Location.Lng,
                place.Types
            );
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to call Google Places Details API for placeId {PlaceId}", placeId);
            throw new ExternalServiceException("Google Places Details API is unavailable");
        }
    }
}

// DTOs
public record PlaceAutocomplete(
    string PlaceId,
    string Description,
    string MainText,
    string SecondaryText
);

public record PlaceDetails(
    string PlaceId,
    string Name,
    string FormattedAddress,
    decimal Latitude,
    decimal Longitude,
    string[] Types
);

// Custom exception
public class ExternalServiceException(string message) : Exception(message);

// Google API response models
internal record GoogleAutocompleteResponse(List<Prediction> Predictions);
internal record Prediction(
    string PlaceId,
    string Description,
    StructuredFormatting StructuredFormatting
);
internal record StructuredFormatting(string MainText, string SecondaryText);

internal record GooglePlaceDetailsResponse(PlaceResult Result);
internal record PlaceResult(
    string PlaceId,
    string Name,
    string FormattedAddress,
    Geometry Geometry,
    string[] Types
);
internal record Geometry(Location Location);
internal record Location(double Lat, double Lng);
