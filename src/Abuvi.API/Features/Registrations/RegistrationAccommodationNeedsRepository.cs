using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Registrations;

public interface IRegistrationAccommodationNeedsRepository
{
    Task<List<RegistrationAccommodationNeed>> GetByRegistrationIdAsync(Guid registrationId, CancellationToken ct);
    Task ReplaceAsync(Guid registrationId, IEnumerable<RegistrationAccommodationNeed> needs, CancellationToken ct);
}

public class RegistrationAccommodationNeedsRepository(AbuviDbContext db)
    : IRegistrationAccommodationNeedsRepository
{
    public async Task<List<RegistrationAccommodationNeed>> GetByRegistrationIdAsync(
        Guid registrationId, CancellationToken ct)
        => await db.RegistrationAccommodationNeeds
            .AsNoTracking()
            .Include(n => n.AccommodationFeature)
            .Where(n => n.RegistrationId == registrationId)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(ct);

    public async Task ReplaceAsync(
        Guid registrationId, IEnumerable<RegistrationAccommodationNeed> needs, CancellationToken ct)
    {
        await db.RegistrationAccommodationNeeds
            .Where(n => n.RegistrationId == registrationId)
            .ExecuteDeleteAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var need in needs)
            need.CreatedAt = now;

        db.RegistrationAccommodationNeeds.AddRange(needs);
        await db.SaveChangesAsync(ct);
    }
}
