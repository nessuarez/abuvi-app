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
}
