using System.Text.Json;
using System.Text.RegularExpressions;
using Abuvi.API.Features.GooglePlaces;

namespace Abuvi.API.Features.Camps;

public interface IGooglePlacesMapperService
{
    CampGoogleData MapToCampData(PlaceDetails details);
    IReadOnlyList<CampPhoto> MapToPhotos(PlaceDetails details, Guid campId);
}

/// <summary>
/// Value object carrying the Google Places-sourced data fields to apply to a Camp entity
/// </summary>
public record CampGoogleData(
    string? FormattedAddress,
    string? StreetAddress,
    string? Locality,
    string? AdministrativeArea,
    string? PostalCode,
    string? Country,
    string? PhoneNumber,
    string? NationalPhoneNumber,
    string? WebsiteUrl,
    string? GoogleMapsUrl,
    decimal? GoogleRating,
    int? GoogleRatingCount,
    string? BusinessStatus,
    string? PlaceTypes
);

/// <summary>
/// Maps Google Places API response data to Camp domain types
/// </summary>
public class GooglePlacesMapperService : IGooglePlacesMapperService
{
    public CampGoogleData MapToCampData(PlaceDetails details)
    {
        var components = details.AddressComponents ?? [];

        string? GetComponent(params string[] types)
            => components.FirstOrDefault(c => c.Types.Intersect(types).Any())?.LongName;

        var streetNumber = components.FirstOrDefault(c => c.Types.Contains("street_number"))?.LongName;
        var route = components.FirstOrDefault(c => c.Types.Contains("route"))?.LongName;
        var streetAddress = (streetNumber, route) switch
        {
            (not null, not null) => $"{route}, {streetNumber}",
            (null, not null) => route,
            _ => null
        };

        var placeTypes = details.Types.Length > 0
            ? JsonSerializer.Serialize(details.Types)
            : null;

        return new CampGoogleData(
            FormattedAddress: details.FormattedAddress,
            StreetAddress: streetAddress,
            Locality: GetComponent("locality", "sublocality"),
            AdministrativeArea: GetComponent("administrative_area_level_2", "administrative_area_level_1"),
            PostalCode: GetComponent("postal_code"),
            Country: GetComponent("country"),
            PhoneNumber: details.PhoneNumber,
            NationalPhoneNumber: details.NationalPhoneNumber,
            WebsiteUrl: details.Website,
            GoogleMapsUrl: details.GoogleMapsUrl,
            GoogleRating: details.Rating,
            GoogleRatingCount: details.RatingCount,
            BusinessStatus: details.BusinessStatus,
            PlaceTypes: placeTypes
        );
    }

    public IReadOnlyList<CampPhoto> MapToPhotos(PlaceDetails details, Guid campId)
    {
        if (details.Photos.Count == 0) return [];

        var now = DateTime.UtcNow;
        return details.Photos
            .Select((photo, index) => new CampPhoto
            {
                Id = Guid.NewGuid(),
                CampId = campId,
                PhotoReference = photo.PhotoReference,
                PhotoUrl = null,  // Phase 1: references only
                Width = photo.Width,
                Height = photo.Height,
                AttributionName = StripHtmlAttribution(photo.HtmlAttributions.FirstOrDefault() ?? "Google"),
                AttributionUrl = ExtractAttributionUrl(photo.HtmlAttributions.FirstOrDefault()),
                IsOriginal = true,
                IsPrimary = index == 0,
                DisplayOrder = index + 1,
                CreatedAt = now,
                UpdatedAt = now
            })
            .ToList();
    }

    private static string StripHtmlAttribution(string htmlAttribution)
        => Regex.Replace(htmlAttribution, "<[^>]+>", "").Trim();

    private static string? ExtractAttributionUrl(string? htmlAttribution)
    {
        if (string.IsNullOrEmpty(htmlAttribution)) return null;
        var match = Regex.Match(htmlAttribution, @"href=""([^""]+)""");
        return match.Success ? match.Groups[1].Value : null;
    }
}
