namespace Abuvi.API.Features.Camps;

/// <summary>
/// Repository interface for AssociationSettings data access operations
/// </summary>
public interface IAssociationSettingsRepository
{
    /// <summary>
    /// Gets a setting by its key
    /// </summary>
    Task<AssociationSettings?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new setting
    /// </summary>
    Task<AssociationSettings> CreateAsync(AssociationSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing setting
    /// </summary>
    Task<AssociationSettings> UpdateAsync(AssociationSettings settings, CancellationToken cancellationToken = default);
}
