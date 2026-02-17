namespace Abuvi.API.Features.Camps;

/// <summary>
/// Service for managing camp locations
/// </summary>
public class CampsService
{
    private readonly ICampsRepository _repository;

    public CampsService(ICampsRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Creates a new camp location
    /// </summary>
    public async Task<CampResponse> CreateAsync(
        CreateCampRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate pricing
        if (request.PricePerAdult < 0 || request.PricePerChild < 0 || request.PricePerBaby < 0)
        {
            throw new ArgumentException("All prices cannot be negative");
        }

        // Validate coordinates if provided
        if (request.Latitude.HasValue && (request.Latitude < -90 || request.Latitude > 90))
        {
            throw new ArgumentException("Latitude must be between -90 and 90");
        }

        if (request.Longitude.HasValue && (request.Longitude < -180 || request.Longitude > 180))
        {
            throw new ArgumentException("Longitude must be between -180 and 180");
        }

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

        var created = await _repository.CreateAsync(camp, cancellationToken);

        return MapToCampResponse(created);
    }

    /// <summary>
    /// Gets a camp by ID
    /// </summary>
    public async Task<CampResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var camp = await _repository.GetByIdAsync(id, cancellationToken);
        return camp == null ? null : MapToCampResponse(camp);
    }

    /// <summary>
    /// Gets all camps with optional filtering
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
    public async Task<CampResponse?> UpdateAsync(
        Guid id,
        UpdateCampRequest request,
        CancellationToken cancellationToken = default)
    {
        var camp = await _repository.GetByIdAsync(id, cancellationToken);
        if (camp == null)
        {
            return null;
        }

        // Validate pricing
        if (request.PricePerAdult < 0 || request.PricePerChild < 0 || request.PricePerBaby < 0)
        {
            throw new ArgumentException("All prices cannot be negative");
        }

        // Validate coordinates if provided
        if (request.Latitude.HasValue && (request.Latitude < -90 || request.Latitude > 90))
        {
            throw new ArgumentException("Latitude must be between -90 and 90");
        }

        if (request.Longitude.HasValue && (request.Longitude < -180 || request.Longitude > 180))
        {
            throw new ArgumentException("Longitude must be between -180 and 180");
        }

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

        var updated = await _repository.UpdateAsync(camp, cancellationToken);

        return MapToCampResponse(updated);
    }

    /// <summary>
    /// Deletes a camp
    /// </summary>
    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var camp = await _repository.GetByIdAsync(id, cancellationToken);
        if (camp == null)
        {
            return false;
        }

        // Check if camp has editions
        if (camp.Editions.Any())
        {
            throw new InvalidOperationException("Cannot delete camp with existing editions");
        }

        return await _repository.DeleteAsync(id, cancellationToken);
    }

    private static CampResponse MapToCampResponse(Camp camp)
    {
        return new CampResponse(
            Id: camp.Id,
            Name: camp.Name,
            Description: camp.Description,
            Location: camp.Location,
            Latitude: camp.Latitude,
            Longitude: camp.Longitude,
            GooglePlaceId: camp.GooglePlaceId,
            PricePerAdult: camp.PricePerAdult,
            PricePerChild: camp.PricePerChild,
            PricePerBaby: camp.PricePerBaby,
            IsActive: camp.IsActive,
            CreatedAt: camp.CreatedAt,
            UpdatedAt: camp.UpdatedAt
        );
    }
}
