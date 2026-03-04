using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.GooglePlaces;
using Abuvi.API.Features.Users;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

/// <summary>
/// Unit tests for CampsService.RefreshGooglePlacesAsync
/// </summary>
public class CampsServiceTests_RefreshGooglePlaces
{
    private readonly ICampsRepository _repository;
    private readonly IGooglePlacesService _googlePlacesService;
    private readonly IGooglePlacesMapperService _mapper;
    private readonly IUsersRepository _usersRepository;
    private readonly CampsService _sut;

    public CampsServiceTests_RefreshGooglePlaces()
    {
        _repository = Substitute.For<ICampsRepository>();
        _googlePlacesService = Substitute.For<IGooglePlacesService>();
        _mapper = Substitute.For<IGooglePlacesMapperService>();
        _usersRepository = Substitute.For<IUsersRepository>();
        _sut = new CampsService(_repository, _googlePlacesService, _mapper, _usersRepository);
    }

    private static Camp CreateCampWithGooglePlaceId(Guid? id = null, string googlePlaceId = "ChIJtest123")
    {
        var campId = id ?? Guid.NewGuid();
        return new Camp
        {
            Id = campId,
            Name = "Test Camp",
            Description = "A test camp",
            Location = "Test Location",
            Latitude = 40.7128m,
            Longitude = -74.0060m,
            GooglePlaceId = googlePlaceId,
            IsActive = true,
            Photos = new List<CampPhoto>(),
            Editions = new List<CampEdition>()
        };
    }

    private static CampGoogleData CreateGoogleData() => new(
        FormattedAddress: "Calle Test 123, Madrid, Spain",
        StreetAddress: "Calle Test 123",
        Locality: "Madrid",
        AdministrativeArea: "Comunidad de Madrid",
        PostalCode: "28001",
        Country: "Spain",
        PhoneNumber: "+34600000000",
        NationalPhoneNumber: "600 000 000",
        WebsiteUrl: "https://example.com",
        GoogleMapsUrl: "https://maps.google.com/test",
        GoogleRating: 4.5m,
        GoogleRatingCount: 120,
        BusinessStatus: "OPERATIONAL",
        PlaceTypes: "campground"
    );

    #region Successful Cases

    [Fact]
    public async Task RefreshGooglePlacesAsync_ValidCampWithGooglePlaceId_ReturnsUpdatedResponse()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var camp = CreateCampWithGooglePlaceId(campId);
        var googleData = CreateGoogleData();
        var placeDetails = new PlaceDetails(
            PlaceId: camp.GooglePlaceId!,
            Name: "Test Camp",
            FormattedAddress: googleData.FormattedAddress!,
            Latitude: 40.7128m,
            Longitude: -74.0060m,
            Types: ["campground"],
            PhoneNumber: googleData.PhoneNumber,
            NationalPhoneNumber: googleData.NationalPhoneNumber,
            Website: googleData.WebsiteUrl,
            GoogleMapsUrl: googleData.GoogleMapsUrl,
            Rating: 4.5m,
            RatingCount: 120,
            BusinessStatus: "OPERATIONAL",
            AddressComponents: [],
            Photos: []
        );

        _repository.GetByIdWithPhotosAsync(campId, Arg.Any<CancellationToken>())
            .Returns(camp);
        _repository.DeleteGooglePhotosAsync(campId, Arg.Any<CancellationToken>())
            .Returns(0);
        _googlePlacesService.GetPlaceDetailsAsync(camp.GooglePlaceId!, Arg.Any<CancellationToken>())
            .Returns(placeDetails);
        _mapper.MapToCampData(placeDetails).Returns(googleData);
        _mapper.MapToPhotos(placeDetails, campId).Returns(new List<CampPhoto>());
        _repository.UpdateAsync(Arg.Any<Camp>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Camp>());

        // Act
        var result = await _sut.RefreshGooglePlacesAsync(campId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(campId);
        await _repository.Received(1).DeleteGooglePhotosAsync(campId, Arg.Any<CancellationToken>());
        await _googlePlacesService.Received(1).GetPlaceDetailsAsync(camp.GooglePlaceId!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshGooglePlacesAsync_ValidCamp_DeletesExistingGooglePhotosBeforeReEnriching()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var camp = CreateCampWithGooglePlaceId(campId);
        camp.Photos = new List<CampPhoto>
        {
            new() { Id = Guid.NewGuid(), CampId = campId, IsOriginal = true, PhotoReference = "old-ref" },
            new() { Id = Guid.NewGuid(), CampId = campId, IsOriginal = false, PhotoUrl = "user-photo.jpg" }
        };

        _repository.GetByIdWithPhotosAsync(campId, Arg.Any<CancellationToken>())
            .Returns(camp);
        _repository.DeleteGooglePhotosAsync(campId, Arg.Any<CancellationToken>())
            .Returns(1);
        _googlePlacesService.GetPlaceDetailsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((PlaceDetails?)null);
        _repository.UpdateAsync(Arg.Any<Camp>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Camp>());

        // Act
        await _sut.RefreshGooglePlacesAsync(campId);

        // Assert
        await _repository.Received(1).DeleteGooglePhotosAsync(campId, Arg.Any<CancellationToken>());
    }

    #endregion

    #region Not Found

    [Fact]
    public async Task RefreshGooglePlacesAsync_NonExistentCamp_ReturnsNull()
    {
        // Arrange
        var campId = Guid.NewGuid();
        _repository.GetByIdWithPhotosAsync(campId, Arg.Any<CancellationToken>())
            .Returns((Camp?)null);

        // Act
        var result = await _sut.RefreshGooglePlacesAsync(campId);

        // Assert
        result.Should().BeNull();
        await _repository.DidNotReceive().DeleteGooglePhotosAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Business Rule Violations

    [Fact]
    public async Task RefreshGooglePlacesAsync_CampWithoutGooglePlaceId_ThrowsBusinessRuleException()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var camp = CreateCampWithGooglePlaceId(campId, googlePlaceId: null!);
        camp.GooglePlaceId = null;

        _repository.GetByIdWithPhotosAsync(campId, Arg.Any<CancellationToken>())
            .Returns(camp);

        // Act
        var act = () => _sut.RefreshGooglePlacesAsync(campId);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*Google Place ID*");
        await _repository.DidNotReceive().DeleteGooglePhotosAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshGooglePlacesAsync_CampWithEmptyGooglePlaceId_ThrowsBusinessRuleException()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var camp = CreateCampWithGooglePlaceId(campId);
        camp.GooglePlaceId = "   ";

        _repository.GetByIdWithPhotosAsync(campId, Arg.Any<CancellationToken>())
            .Returns(camp);

        // Act
        var act = () => _sut.RefreshGooglePlacesAsync(campId);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*Google Place ID*");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task RefreshGooglePlacesAsync_GooglePlacesServiceReturnsNull_ReturnsResponseWithoutGoogleDataChanges()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var camp = CreateCampWithGooglePlaceId(campId);

        _repository.GetByIdWithPhotosAsync(campId, Arg.Any<CancellationToken>())
            .Returns(camp);
        _repository.DeleteGooglePhotosAsync(campId, Arg.Any<CancellationToken>())
            .Returns(0);
        _googlePlacesService.GetPlaceDetailsAsync(camp.GooglePlaceId!, Arg.Any<CancellationToken>())
            .Returns((PlaceDetails?)null);
        _repository.UpdateAsync(Arg.Any<Camp>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Camp>());

        // Act
        var result = await _sut.RefreshGooglePlacesAsync(campId);

        // Assert — should still return a response (enrichment is a no-op when details are null)
        result.Should().NotBeNull();
        _mapper.DidNotReceive().MapToCampData(Arg.Any<PlaceDetails>());
    }

    [Fact]
    public async Task RefreshGooglePlacesAsync_CampWithNoExistingPhotos_AddsNewPhotosSuccessfully()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var camp = CreateCampWithGooglePlaceId(campId);
        var googleData = CreateGoogleData();
        var placeDetails = new PlaceDetails(
            PlaceId: camp.GooglePlaceId!,
            Name: "Test Camp",
            FormattedAddress: "Test Address",
            Latitude: 40.7128m,
            Longitude: -74.0060m,
            Types: ["campground"],
            PhoneNumber: null,
            NationalPhoneNumber: null,
            Website: null,
            GoogleMapsUrl: null,
            Rating: null,
            RatingCount: null,
            BusinessStatus: null,
            AddressComponents: [],
            Photos: []
        );
        var newPhotos = new List<CampPhoto>
        {
            new() { Id = Guid.NewGuid(), CampId = campId, IsOriginal = true, PhotoReference = "new-ref-1" },
            new() { Id = Guid.NewGuid(), CampId = campId, IsOriginal = true, PhotoReference = "new-ref-2" }
        };

        _repository.GetByIdWithPhotosAsync(campId, Arg.Any<CancellationToken>())
            .Returns(camp);
        _repository.DeleteGooglePhotosAsync(campId, Arg.Any<CancellationToken>())
            .Returns(0);
        _googlePlacesService.GetPlaceDetailsAsync(camp.GooglePlaceId!, Arg.Any<CancellationToken>())
            .Returns(placeDetails);
        _mapper.MapToCampData(placeDetails).Returns(googleData);
        _mapper.MapToPhotos(placeDetails, campId).Returns(newPhotos);
        _repository.UpdateAsync(Arg.Any<Camp>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Camp>());
        _repository.AddPhotosAsync(Arg.Any<IEnumerable<CampPhoto>>(), Arg.Any<CancellationToken>())
            .Returns(newPhotos);

        // Act
        var result = await _sut.RefreshGooglePlacesAsync(campId);

        // Assert
        result.Should().NotBeNull();
        await _repository.Received(1).AddPhotosAsync(
            Arg.Is<IReadOnlyList<CampPhoto>>(p => p.Count == 2),
            Arg.Any<CancellationToken>());
    }

    #endregion
}
