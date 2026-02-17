using System.Text.Json.Serialization;

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
                p.StructuredFormatting?.MainText ?? p.Description,
                p.StructuredFormatting?.SecondaryText ?? string.Empty
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
        var fields = "place_id,name,formatted_address,geometry,types,address_component,formatted_phone_number,international_phone_number,website,url,rating,user_ratings_total,business_status,photos";
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
                place.Types,
                PhoneNumber: place.InternationalPhoneNumber,
                NationalPhoneNumber: place.FormattedPhoneNumber,
                Website: place.Website,
                GoogleMapsUrl: place.Url,
                Rating: place.Rating.HasValue ? (decimal)place.Rating.Value : null,
                RatingCount: place.UserRatingsTotal,
                BusinessStatus: place.BusinessStatus,
                AddressComponents: place.AddressComponents,
                Photos: place.Photos?
                    .Select(p => new PlacePhoto(p.PhotoReference, p.Width, p.Height, p.HtmlAttributions))
                    .ToList() ?? []
            );
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to call Google Places Details API for placeId {PlaceId}", placeId);
            throw new ExternalServiceException("Google Places Details API is unavailable");
        }
    }
}

// Public DTOs
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
    string[] Types,
    string? PhoneNumber,
    string? NationalPhoneNumber,
    string? Website,
    string? GoogleMapsUrl,
    decimal? Rating,
    int? RatingCount,
    string? BusinessStatus,
    GoogleAddressComponent[]? AddressComponents,
    IReadOnlyList<PlacePhoto> Photos
);

public record PlacePhoto(
    string PhotoReference,
    int Width,
    int Height,
    string[] HtmlAttributions
);

// Made public so GooglePlacesMapperService in Camps feature can use it
public record GoogleAddressComponent(
    [property: JsonPropertyName("long_name")] string LongName,
    [property: JsonPropertyName("short_name")] string ShortName,
    [property: JsonPropertyName("types")] string[] Types
);

// Custom exception
public class ExternalServiceException(string message) : Exception(message);

// Google API response models (internal)
internal record GoogleAutocompleteResponse(
    [property: JsonPropertyName("predictions")] List<Prediction> Predictions
);
internal record Prediction(
    [property: JsonPropertyName("place_id")] string PlaceId,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("structured_formatting")] StructuredFormatting? StructuredFormatting
);
internal record StructuredFormatting(
    [property: JsonPropertyName("main_text")] string MainText,
    [property: JsonPropertyName("secondary_text")] string? SecondaryText
);

internal record GooglePlaceDetailsResponse(PlaceResult Result);
internal record PlaceResult(
    [property: JsonPropertyName("place_id")] string PlaceId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("formatted_address")] string FormattedAddress,
    [property: JsonPropertyName("geometry")] Geometry Geometry,
    [property: JsonPropertyName("types")] string[] Types,
    [property: JsonPropertyName("international_phone_number")] string? InternationalPhoneNumber,
    [property: JsonPropertyName("formatted_phone_number")] string? FormattedPhoneNumber,
    [property: JsonPropertyName("website")] string? Website,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("rating")] double? Rating,
    [property: JsonPropertyName("user_ratings_total")] int? UserRatingsTotal,
    [property: JsonPropertyName("business_status")] string? BusinessStatus,
    [property: JsonPropertyName("address_components")] GoogleAddressComponent[]? AddressComponents,
    [property: JsonPropertyName("photos")] GooglePhotoResult[]? Photos
);
internal record Geometry([property: JsonPropertyName("location")] Location Location);
internal record Location([property: JsonPropertyName("lat")] double Lat, [property: JsonPropertyName("lng")] double Lng);
internal record GooglePhotoResult(
    [property: JsonPropertyName("photo_reference")] string PhotoReference,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("html_attributions")] string[] HtmlAttributions
);
