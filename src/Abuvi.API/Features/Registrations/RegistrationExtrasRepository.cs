using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Registrations;

public interface IRegistrationExtrasRepository
{
    Task<IReadOnlyList<RegistrationExtra>> GetByRegistrationIdAsync(Guid registrationId, CancellationToken ct);
    Task DeleteByRegistrationIdAsync(Guid registrationId, CancellationToken ct);
    Task AddRangeAsync(IEnumerable<RegistrationExtra> extras, CancellationToken ct);
}

public class RegistrationExtrasRepository(AbuviDbContext db) : IRegistrationExtrasRepository
{
    public async Task<IReadOnlyList<RegistrationExtra>> GetByRegistrationIdAsync(Guid registrationId, CancellationToken ct)
        => await db.RegistrationExtras
            .AsNoTracking()
            .Where(e => e.RegistrationId == registrationId)
            .ToListAsync(ct);

    public async Task DeleteByRegistrationIdAsync(Guid registrationId, CancellationToken ct)
    {
        await db.RegistrationExtras
            .Where(e => e.RegistrationId == registrationId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<RegistrationExtra> extras, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        foreach (var extra in extras)
            extra.CreatedAt = now;
        db.RegistrationExtras.AddRange(extras);
        await db.SaveChangesAsync(ct);
    }
}
