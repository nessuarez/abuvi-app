using Abuvi.API.Features.GooglePlaces;

namespace Abuvi.API.Features.Camps;

/// <summary>
/// Service for managing camp locations
/// </summary>
public class CampsService
{
    private readonly ICampsRepository _repository;
    private readonly IGooglePlacesService _googlePlacesService;
    private readonly IGooglePlacesMapperService _mapper;

    public CampsService(
        ICampsRepository repository,
        IGooglePlacesService googlePlacesService,
        IGooglePlacesMapperService mapper)
    {
        _repository = repository;
        _googlePlacesService = googlePlacesService;
        _mapper = mapper;
    }

    /// <summary>
    /// Creates a new camp location. If GooglePlaceId is provided, auto-enriches with Google Places data.
    /// </summary>
    public async Task<CampDetailResponse> CreateAsync(
        CreateCampRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate pricing
        if (request.PricePerAdult < 0 || request.PricePerChild < 0 || request.PricePerBaby < 0)
            throw new ArgumentException("All prices cannot be negative");

        // Validate coordinates if provided
        if (request.Latitude.HasValue && (request.Latitude < -90 || request.Latitude > 90))
            throw new ArgumentException("Latitude must be between -90 and 90");

        if (request.Longitude.HasValue && (request.Longitude < -180 || request.Longitude > 180))
            throw new ArgumentException("Longitude must be between -180 and 180");

        var camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Location = request.Location,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            GooglePlaceId = request.GooglePlaceId,
            PricePerAdult = request.PricePerAdult,
            PricePerChild = request.PricePerChild,
            PricePerBaby = request.PricePerBaby,
            IsActive = true
        };
        camp.SetAccommodationCapacity(request.AccommodationCapacity);

        var created = await _repository.CreateAsync(camp, cancellationToken);

        // Auto-enrich from Google Places if GooglePlaceId is provided
        if (!string.IsNullOrWhiteSpace(created.GooglePlaceId))
            created = await EnrichFromGooglePlacesAsync(created, cancellationToken);

        return MapToCampDetailResponse(created, created.Photos);
    }

    /// <summary>
    /// Gets a camp by ID including photos
    /// </summary>
    public async Task<CampDetailResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var camp = await _repository.GetByIdWithPhotosAsync(id, cancellationToken);
        return camp == null ? null : MapToCampDetailResponse(camp, camp.Photos);
    }

    /// <summary>
    /// Gets all camps with optional filtering (lightweight — no photos)
    /// </summary>
    public async Task<List<CampResponse>> GetAllAsync(
        bool? isActive = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var camps = await _repository.GetAllAsync(isActive, skip, take, cancellationToken);
        return camps.Select(MapToCampResponse).ToList();
    }

    /// <summary>
    /// Updates an existing camp
    /// </summary>
    public async Task<CampDetailResponse?> UpdateAsync(
        Guid id,
        UpdateCampRequest request,
        CancellationToken cancellationToken = default)
    {
        var camp = await _repository.GetByIdWithPhotosAsync(id, cancellationToken);
        if (camp == null) return null;

        // Validate pricing
        if (request.PricePerAdult < 0 || request.PricePerChild < 0 || request.PricePerBaby < 0)
            throw new ArgumentException("All prices cannot be negative");

        // Validate coordinates if provided
        if (request.Latitude.HasValue && (request.Latitude < -90 || request.Latitude > 90))
            throw new ArgumentException("Latitude must be between -90 and 90");

        if (request.Longitude.HasValue && (request.Longitude < -180 || request.Longitude > 180))
            throw new ArgumentException("Longitude must be between -180 and 180");

        camp.Name = request.Name;
        camp.Description = request.Description;
        camp.Location = request.Location;
        camp.Latitude = request.Latitude;
        camp.Longitude = request.Longitude;
        camp.GooglePlaceId = request.GooglePlaceId;
        camp.PricePerAdult = request.PricePerAdult;
        camp.PricePerChild = request.PricePerChild;
        camp.PricePerBaby = request.PricePerBaby;
        camp.IsActive = request.IsActive;
        camp.SetAccommodationCapacity(request.AccommodationCapacity);

        var updated = await _repository.UpdateAsync(camp, cancellationToken);

        return MapToCampDetailResponse(updated, updated.Photos);
    }

    /// <summary>
    /// Deletes a camp
    /// </summary>
    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var camp = await _repository.GetByIdAsync(id, cancellationToken);
        if (camp == null) return false;

        // Check if camp has editions
        if (camp.Editions.Any())
            throw new InvalidOperationException("Cannot delete camp with existing editions");

        return await _repository.DeleteAsync(id, cancellationToken);
    }

    // --- Private helpers ---

    private async Task<Camp> EnrichFromGooglePlacesAsync(Camp camp, CancellationToken ct)
    {
        var details = await _googlePlacesService.GetPlaceDetailsAsync(camp.GooglePlaceId!, ct);
        if (details is null) return camp;

        var googleData = _mapper.MapToCampData(details);
        var photos = _mapper.MapToPhotos(details, camp.Id);

        camp.FormattedAddress = googleData.FormattedAddress;
        camp.StreetAddress = googleData.StreetAddress;
        camp.Locality = googleData.Locality;
        camp.AdministrativeArea = googleData.AdministrativeArea;
        camp.PostalCode = googleData.PostalCode;
        camp.Country = googleData.Country;
        camp.PhoneNumber = googleData.PhoneNumber;
        camp.NationalPhoneNumber = googleData.NationalPhoneNumber;
        camp.WebsiteUrl = googleData.WebsiteUrl;
        camp.GoogleMapsUrl = googleData.GoogleMapsUrl;
        camp.GoogleRating = googleData.GoogleRating;
        camp.GoogleRatingCount = googleData.GoogleRatingCount;
        camp.BusinessStatus = googleData.BusinessStatus;
        camp.PlaceTypes = googleData.PlaceTypes;
        camp.LastGoogleSyncAt = DateTime.UtcNow;

        var updatedCamp = await _repository.UpdateAsync(camp, ct);

        if (photos.Count > 0)
        {
            var savedPhotos = await _repository.AddPhotosAsync(photos, ct);
            updatedCamp.Photos = savedPhotos.ToList();
        }

        return updatedCamp;
    }

    private static CampResponse MapToCampResponse(Camp camp) => new(
        Id: camp.Id,
        Name: camp.Name,
        Description: camp.Description,
        Location: camp.Location,
        Latitude: camp.Latitude,
        Longitude: camp.Longitude,
        GooglePlaceId: camp.GooglePlaceId,
        FormattedAddress: camp.FormattedAddress,
        PhoneNumber: camp.PhoneNumber,
        WebsiteUrl: camp.WebsiteUrl,
        GoogleMapsUrl: camp.GoogleMapsUrl,
        GoogleRating: camp.GoogleRating,
        GoogleRatingCount: camp.GoogleRatingCount,
        BusinessStatus: camp.BusinessStatus,
        PricePerAdult: camp.PricePerAdult,
        PricePerChild: camp.PricePerChild,
        PricePerBaby: camp.PricePerBaby,
        IsActive: camp.IsActive,
        CreatedAt: camp.CreatedAt,
        UpdatedAt: camp.UpdatedAt
    );

    private static CampDetailResponse MapToCampDetailResponse(Camp camp, IEnumerable<CampPhoto> photos)
    {
        var accommodation = camp.GetAccommodationCapacity();
        return new CampDetailResponse(
            Id: camp.Id,
            Name: camp.Name,
            Description: camp.Description,
            Location: camp.Location,
            Latitude: camp.Latitude,
            Longitude: camp.Longitude,
            GooglePlaceId: camp.GooglePlaceId,
            FormattedAddress: camp.FormattedAddress,
            StreetAddress: camp.StreetAddress,
            Locality: camp.Locality,
            AdministrativeArea: camp.AdministrativeArea,
            PostalCode: camp.PostalCode,
            Country: camp.Country,
            PhoneNumber: camp.PhoneNumber,
            NationalPhoneNumber: camp.NationalPhoneNumber,
            WebsiteUrl: camp.WebsiteUrl,
            GoogleMapsUrl: camp.GoogleMapsUrl,
            GoogleRating: camp.GoogleRating,
            GoogleRatingCount: camp.GoogleRatingCount,
            BusinessStatus: camp.BusinessStatus,
            PlaceTypes: camp.PlaceTypes,
            LastGoogleSyncAt: camp.LastGoogleSyncAt,
            PricePerAdult: camp.PricePerAdult,
            PricePerChild: camp.PricePerChild,
            PricePerBaby: camp.PricePerBaby,
            IsActive: camp.IsActive,
            AccommodationCapacity: accommodation,
            CalculatedTotalBedCapacity: accommodation?.CalculateTotalBedCapacity(),
            Photos: photos.Select(MapToPhotoResponse).ToList(),
            CreatedAt: camp.CreatedAt,
            UpdatedAt: camp.UpdatedAt
        );
    }

    private static CampPhotoResponse MapToPhotoResponse(CampPhoto photo) => new(
        Id: photo.Id,
        PhotoReference: photo.PhotoReference,
        PhotoUrl: photo.PhotoUrl,
        Width: photo.Width,
        Height: photo.Height,
        AttributionName: photo.AttributionName,
        AttributionUrl: photo.AttributionUrl,
        Description: photo.Description,
        IsPrimary: photo.IsPrimary,
        DisplayOrder: photo.DisplayOrder
    );
}
