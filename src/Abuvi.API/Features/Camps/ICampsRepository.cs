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
    /// Deletes a camp by its unique identifier
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
