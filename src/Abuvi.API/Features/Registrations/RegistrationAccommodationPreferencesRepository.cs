using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Registrations;

public interface IRegistrationAccommodationPreferencesRepository
{
    Task<List<RegistrationAccommodationPreference>> GetByRegistrationIdAsync(Guid registrationId, CancellationToken ct);
    Task DeleteByRegistrationIdAsync(Guid registrationId, CancellationToken ct);
    Task AddRangeAsync(IEnumerable<RegistrationAccommodationPreference> preferences, CancellationToken ct);
}

public class RegistrationAccommodationPreferencesRepository(AbuviDbContext db)
    : IRegistrationAccommodationPreferencesRepository
{
    public async Task<List<RegistrationAccommodationPreference>> GetByRegistrationIdAsync(
        Guid registrationId, CancellationToken ct)
        => await db.RegistrationAccommodationPreferences
            .AsNoTracking()
            .Include(p => p.CampEditionAccommodation)
            .Where(p => p.RegistrationId == registrationId)
            .OrderBy(p => p.PreferenceOrder)
            .ToListAsync(ct);

    public async Task DeleteByRegistrationIdAsync(Guid registrationId, CancellationToken ct)
    {
        await db.RegistrationAccommodationPreferences
            .Where(p => p.RegistrationId == registrationId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<RegistrationAccommodationPreference> preferences, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        foreach (var pref in preferences)
            pref.CreatedAt = now;
        db.RegistrationAccommodationPreferences.AddRange(preferences);
        await db.SaveChangesAsync(ct);
    }
}
