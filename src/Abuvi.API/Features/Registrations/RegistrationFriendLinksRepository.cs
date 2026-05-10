using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Registrations;

public interface IRegistrationFriendLinksRepository
{
    Task<List<RegistrationFriendLink>> GetByRegistrationIdAsync(Guid registrationId, CancellationToken ct);
    Task ReplaceAsync(Guid registrationId, IEnumerable<Guid> linkedRegistrationIds, Guid? createdByUserId, CancellationToken ct);
}

public class RegistrationFriendLinksRepository(AbuviDbContext db)
    : IRegistrationFriendLinksRepository
{
    public async Task<List<RegistrationFriendLink>> GetByRegistrationIdAsync(
        Guid registrationId, CancellationToken ct)
    {
        // Query both outgoing (A→B) and incoming (B→A) to be robust against inconsistencies
        var outgoing = await db.RegistrationFriendLinks
            .AsNoTracking()
            .Include(l => l.LinkedRegistration).ThenInclude(r => r.FamilyUnit)
            .Where(l => l.RegistrationId == registrationId)
            .ToListAsync(ct);

        var incoming = await db.RegistrationFriendLinks
            .AsNoTracking()
            .Include(l => l.Registration).ThenInclude(r => r.FamilyUnit)
            .Where(l => l.LinkedRegistrationId == registrationId)
            .ToListAsync(ct);

        // Deduplicate: outgoing covers all bidirectional links when properly maintained;
        // include incoming to handle any asymmetry. Key = the "other" registration ID.
        var seen = new HashSet<Guid>();
        var result = new List<RegistrationFriendLink>();

        foreach (var link in outgoing)
        {
            if (seen.Add(link.LinkedRegistrationId))
                result.Add(link);
        }

        foreach (var link in incoming)
        {
            if (seen.Add(link.RegistrationId))
            {
                // Normalize so LinkedRegistrationId always represents the "other" side
                result.Add(new RegistrationFriendLink
                {
                    Id = link.Id,
                    RegistrationId = registrationId,
                    LinkedRegistrationId = link.RegistrationId,
                    CreatedByUserId = link.CreatedByUserId,
                    CreatedAt = link.CreatedAt,
                    LinkedRegistration = link.Registration
                });
            }
        }

        return result;
    }

    public async Task ReplaceAsync(
        Guid registrationId,
        IEnumerable<Guid> linkedRegistrationIds,
        Guid? createdByUserId,
        CancellationToken ct)
    {
        var desired = linkedRegistrationIds.ToHashSet();

        var current = await db.RegistrationFriendLinks
            .Where(l => l.RegistrationId == registrationId)
            .Select(l => l.LinkedRegistrationId)
            .ToListAsync(ct);
        var currentSet = current.ToHashSet();

        var toDelete = currentSet.Except(desired).ToList();
        var toInsert = desired.Except(currentSet).ToList();

        foreach (var otherId in toDelete)
        {
            await db.RegistrationFriendLinks
                .Where(l => (l.RegistrationId == registrationId && l.LinkedRegistrationId == otherId)
                         || (l.RegistrationId == otherId && l.LinkedRegistrationId == registrationId))
                .ExecuteDeleteAsync(ct);
        }

        if (toInsert.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var otherId in toInsert)
            {
                db.RegistrationFriendLinks.Add(new RegistrationFriendLink
                {
                    Id = Guid.NewGuid(),
                    RegistrationId = registrationId,
                    LinkedRegistrationId = otherId,
                    CreatedByUserId = createdByUserId,
                    CreatedAt = now
                });
                db.RegistrationFriendLinks.Add(new RegistrationFriendLink
                {
                    Id = Guid.NewGuid(),
                    RegistrationId = otherId,
                    LinkedRegistrationId = registrationId,
                    CreatedByUserId = createdByUserId,
                    CreatedAt = now
                });
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
