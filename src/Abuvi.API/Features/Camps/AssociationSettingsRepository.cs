using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Camps;

/// <summary>
/// Repository implementation for AssociationSettings data access operations
/// </summary>
public class AssociationSettingsRepository : IAssociationSettingsRepository
{
    private readonly AbuviDbContext _context;

    public AssociationSettingsRepository(AbuviDbContext context)
    {
        _context = context;
    }

    public async Task<AssociationSettings?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _context.AssociationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SettingKey == key, cancellationToken);
    }

    public async Task<AssociationSettings> CreateAsync(AssociationSettings settings, CancellationToken cancellationToken = default)
    {
        settings.UpdatedAt = DateTime.UtcNow;

        _context.AssociationSettings.Add(settings);
        await _context.SaveChangesAsync(cancellationToken);

        return settings;
    }

    public async Task<AssociationSettings> UpdateAsync(AssociationSettings settings, CancellationToken cancellationToken = default)
    {
        settings.UpdatedAt = DateTime.UtcNow;

        _context.AssociationSettings.Update(settings);
        await _context.SaveChangesAsync(cancellationToken);

        return settings;
    }
}
