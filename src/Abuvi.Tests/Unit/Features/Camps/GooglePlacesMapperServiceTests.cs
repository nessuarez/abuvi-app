using Abuvi.API.Features.Camps;
using Abuvi.API.Features.GooglePlaces;
using FluentAssertions;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

/// <summary>
/// Unit tests for GooglePlacesMapperService
/// </summary>
public class GooglePlacesMapperServiceTests
{
    private readonly GooglePlacesMapperService _sut = new();

    private static PlaceDetails BuildDetails(
        string? formattedAddress = "Calle Mayor, 1, 28001 Madrid, Spain",
        string[] types = null!,
        string? phone = "+34 912 345 678",
        string? nationalPhone = "912 345 678",
        string? website = "https://camp.es",
        string? mapsUrl = "https://maps.google.com/?q=test",
        decimal? rating = 4.3m,
        int? ratingCount = 80,
        string? businessStatus = "OPERATIONAL",
        GoogleAddressComponent[]? components = null,
        List<PlacePhoto>? photos = null)
    {
        return new PlaceDetails(
            PlaceId: "ChIJtest",
            Name: "Camp Test",
            FormattedAddress: formattedAddress ?? string.Empty,
            Latitude: 40.4m,
            Longitude: -3.7m,
            Types: types ?? ["campground"],
            PhoneNumber: phone,
            NationalPhoneNumber: nationalPhone,
            Website: website,
            GoogleMapsUrl: mapsUrl,
            Rating: rating,
            RatingCount: ratingCount,
            BusinessStatus: businessStatus,
            AddressComponents: components,
            Photos: photos ?? []
        );
    }

    #region MapToCampData Tests

    [Fact]
    public void MapToCampData_WithFullDetails_MapsAllFields()
    {
        // Arrange
        var details = BuildDetails();

        // Act
        var result = _sut.MapToCampData(details);

        // Assert
        result.FormattedAddress.Should().Be("Calle Mayor, 1, 28001 Madrid, Spain");
        result.PhoneNumber.Should().Be("+34 912 345 678");
        result.NationalPhoneNumber.Should().Be("912 345 678");
        result.WebsiteUrl.Should().Be("https://camp.es");
        result.GoogleMapsUrl.Should().Be("https://maps.google.com/?q=test");
        result.GoogleRating.Should().Be(4.3m);
        result.GoogleRatingCount.Should().Be(80);
        result.BusinessStatus.Should().Be("OPERATIONAL");
    }

    [Fact]
    public void MapToCampData_WithTypes_SerializesAsJson()
    {
        // Arrange
        var details = BuildDetails(types: ["campground", "point_of_interest", "establishment"]);

        // Act
        var result = _sut.MapToCampData(details);

        // Assert
        result.PlaceTypes.Should().NotBeNull();
        result.PlaceTypes.Should().Contain("campground");
        result.PlaceTypes.Should().Contain("point_of_interest");
    }

    [Fact]
    public void MapToCampData_WithEmptyTypes_ReturnsNullPlaceTypes()
    {
        // Arrange
        var details = BuildDetails(types: []);

        // Act
        var result = _sut.MapToCampData(details);

        // Assert
        result.PlaceTypes.Should().BeNull();
    }

    [Fact]
    public void MapToCampData_WithAddressComponents_ExtractsLocality()
    {
        // Arrange
        var components = new[]
        {
            new GoogleAddressComponent("Madrid", "Madrid", ["locality", "political"]),
            new GoogleAddressComponent("28001", "28001", ["postal_code"]),
            new GoogleAddressComponent("Spain", "ES", ["country", "political"])
        };
        var details = BuildDetails(components: components);

        // Act
        var result = _sut.MapToCampData(details);

        // Assert
        result.Locality.Should().Be("Madrid");
        result.PostalCode.Should().Be("28001");
        result.Country.Should().Be("Spain");
    }

    [Fact]
    public void MapToCampData_WithStreetNumberAndRoute_BuildsStreetAddress()
    {
        // Arrange
        var components = new[]
        {
            new GoogleAddressComponent("1", "1", ["street_number"]),
            new GoogleAddressComponent("Calle Mayor", "Calle Mayor", ["route"])
        };
        var details = BuildDetails(components: components);

        // Act
        var result = _sut.MapToCampData(details);

        // Assert
        result.StreetAddress.Should().Be("Calle Mayor, 1");
    }

    [Fact]
    public void MapToCampData_WithRouteOnlyNoStreetNumber_ReturnsRouteOnly()
    {
        // Arrange
        var components = new[]
        {
            new GoogleAddressComponent("Carretera Nacional", "CN", ["route"])
        };
        var details = BuildDetails(components: components);

        // Act
        var result = _sut.MapToCampData(details);

        // Assert
        result.StreetAddress.Should().Be("Carretera Nacional");
    }

    [Fact]
    public void MapToCampData_WithNullAddressComponents_ReturnsNullAddressFields()
    {
        // Arrange
        var details = BuildDetails(components: null);

        // Act
        var result = _sut.MapToCampData(details);

        // Assert
        result.StreetAddress.Should().BeNull();
        result.Locality.Should().BeNull();
        result.PostalCode.Should().BeNull();
        result.Country.Should().BeNull();
    }

    [Fact]
    public void MapToCampData_WithNullOptionalFields_ReturnsNulls()
    {
        // Arrange
        var details = BuildDetails(phone: null, website: null, rating: null, ratingCount: null, businessStatus: null);

        // Act
        var result = _sut.MapToCampData(details);

        // Assert
        result.PhoneNumber.Should().BeNull();
        result.WebsiteUrl.Should().BeNull();
        result.GoogleRating.Should().BeNull();
        result.GoogleRatingCount.Should().BeNull();
        result.BusinessStatus.Should().BeNull();
    }

    #endregion

    #region MapToPhotos Tests

    [Fact]
    public void MapToPhotos_WithNoPhotos_ReturnsEmpty()
    {
        // Arrange
        var details = BuildDetails(photos: []);

        // Act
        var result = _sut.MapToPhotos(details, Guid.NewGuid());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void MapToPhotos_WithPhotos_MapsAllFields()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var photos = new List<PlacePhoto>
        {
            new("ref1", 800, 600, ["<a href=\"https://author.com\">Author Name</a>"]),
            new("ref2", 1200, 800, ["<a href=\"https://other.com\">Other Author</a>"])
        };
        var details = BuildDetails(photos: photos);

        // Act
        var result = _sut.MapToPhotos(details, campId);

        // Assert
        result.Should().HaveCount(2);
        result[0].CampId.Should().Be(campId);
        result[0].PhotoReference.Should().Be("ref1");
        result[0].Width.Should().Be(800);
        result[0].Height.Should().Be(600);
        result[0].IsPrimary.Should().BeTrue();
        result[0].DisplayOrder.Should().Be(1);
        result[0].IsOriginal.Should().BeTrue();

        result[1].IsPrimary.Should().BeFalse();
        result[1].DisplayOrder.Should().Be(2);
    }

    [Fact]
    public void MapToPhotos_StripsHtmlFromAttribution()
    {
        // Arrange
        var photos = new List<PlacePhoto>
        {
            new("ref1", 800, 600, ["<a href=\"https://google.com\">Photo Author</a>"])
        };
        var details = BuildDetails(photos: photos);

        // Act
        var result = _sut.MapToPhotos(details, Guid.NewGuid());

        // Assert
        result[0].AttributionName.Should().Be("Photo Author");
        result[0].AttributionUrl.Should().Be("https://google.com");
    }

    [Fact]
    public void MapToPhotos_WithEmptyAttribution_UsesGoogleDefault()
    {
        // Arrange
        var photos = new List<PlacePhoto>
        {
            new("ref1", 800, 600, [])
        };
        var details = BuildDetails(photos: photos);

        // Act
        var result = _sut.MapToPhotos(details, Guid.NewGuid());

        // Assert
        result[0].AttributionName.Should().Be("Google");
        result[0].AttributionUrl.Should().BeNull();
    }

    #endregion
}
