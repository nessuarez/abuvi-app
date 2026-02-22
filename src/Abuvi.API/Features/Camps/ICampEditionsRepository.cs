namespace Abuvi.API.Features.Camps;

/// <summary>
/// Repository interface for CampEdition data access operations
/// </summary>
public interface ICampEditionsRepository
{
    /// <summary>
    /// Gets a camp edition by its unique identifier
    /// </summary>
    Task<CampEdition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets camp editions by status and year
    /// </summary>
    Task<List<CampEdition>> GetByStatusAndYearAsync(
        CampEditionStatus status,
        int? year = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new camp edition
    /// </summary>
    Task<CampEdition> CreateAsync(CampEdition edition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing camp edition
    /// </summary>
    Task<CampEdition> UpdateAsync(CampEdition edition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a camp edition by its unique identifier
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a non-archived edition already exists for the given camp and year
    /// </summary>
    Task<bool> ExistsAsync(Guid campId, int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all non-archived editions with optional filtering by year, status, and campId
    /// </summary>
    Task<List<CampEdition>> GetAllAsync(
        int? year = null,
        CampEditionStatus? status = null,
        Guid? campId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the best available camp edition for the given year (Open preferred, then Closed),
    /// falling back to the most recent Completed or Closed edition from the previous year.
    /// Returns null if no qualifying edition exists within the lookback window.
    /// </summary>
    Task<CampEdition?> GetCurrentAsync(int currentYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all non-archived Open camp editions, ordered by start date.
    /// Used by the registration flow to list editions available for registration.
    /// </summary>
    Task<IReadOnlyList<CampEdition>> GetOpenEditionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a CampEditionExtra by its unique identifier.
    /// </summary>
    Task<CampEditionExtra?> GetExtraByIdAsync(Guid extraId, CancellationToken cancellationToken = default);
}
