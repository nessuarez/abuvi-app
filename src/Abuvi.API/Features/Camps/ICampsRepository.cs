namespace Abuvi.API.Features.Camps;

/// <summary>
/// Repository interface for Camp data access operations
/// </summary>
public interface ICampsRepository
{
    /// <summary>
    /// Gets a camp by its unique identifier
    /// </summary>
    Task<Camp?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all camps with optional filtering and pagination
    /// </summary>
    Task<List<Camp>> GetAllAsync(
        bool? isActive = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new camp
    /// </summary>
    Task<Camp> CreateAsync(Camp camp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing camp
    /// </summary>
    Task<Camp> UpdateAsync(Camp camp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a camp by ID including its photos (ordered by DisplayOrder)
    /// </summary>
    Task<Camp?> GetByIdWithPhotosAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a collection of camp photos
    /// </summary>
    Task<IReadOnlyList<CampPhoto>> AddPhotosAsync(IEnumerable<CampPhoto> photos, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a camp by its unique identifier
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a camp photo by its unique identifier
    /// </summary>
    Task<CampPhoto?> GetPhotoByIdAsync(Guid photoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a single manually-uploaded camp photo
    /// </summary>
    Task<CampPhoto> AddPhotoAsync(CampPhoto photo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing camp photo
    /// </summary>
    Task<CampPhoto> UpdatePhotoAsync(CampPhoto photo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a camp photo by its unique identifier
    /// </summary>
    Task<bool> DeletePhotoAsync(Guid photoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all photos for a camp ordered by DisplayOrder
    /// </summary>
    Task<List<CampPhoto>> GetPhotosForCampAsync(Guid campId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates display orders for a batch of photos
    /// </summary>
    Task UpdatePhotoOrdersAsync(IEnumerable<(Guid PhotoId, int DisplayOrder)> updates, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures only one photo is marked as primary for a camp (clears all others)
    /// </summary>
    Task ClearPrimaryPhotoAsync(Guid campId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all Google Places-sourced photos (IsOriginal == true) for a camp
    /// </summary>
    Task<int> DeleteGooglePhotosAsync(Guid campId, CancellationToken cancellationToken = default);

    Task AddAuditLogsAsync(IEnumerable<CampAuditLog> entries, CancellationToken cancellationToken = default);

    Task<List<CampAuditLog>> GetAuditLogAsync(Guid campId, CancellationToken cancellationToken = default);
}
