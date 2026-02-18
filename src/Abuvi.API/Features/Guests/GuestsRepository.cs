using Microsoft.EntityFrameworkCore;
using Abuvi.API.Data;
using Abuvi.API.Common.Services;

namespace Abuvi.API.Features.Guests;

public interface IGuestsRepository
{
    Task<Guest?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Guest>> GetByFamilyUnitAsync(Guid familyUnitId, CancellationToken ct);
    Task AddAsync(Guest guest, CancellationToken ct);
    Task UpdateAsync(Guest guest, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public class GuestsRepository(AbuviDbContext db, IEncryptionService encryption) : IGuestsRepository
{
    public async Task<Guest?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var guest = await db.Guests
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id, ct);

        if (guest is not null)
            DecryptSensitiveFields(guest);

        return guest;
    }

    public async Task<IReadOnlyList<Guest>> GetByFamilyUnitAsync(Guid familyUnitId, CancellationToken ct)
    {
        var guests = await db.Guests
            .AsNoTracking()
            .Where(g => g.FamilyUnitId == familyUnitId && g.IsActive)
            .OrderBy(g => g.LastName)
            .ThenBy(g => g.FirstName)
            .ToListAsync(ct);

        foreach (var guest in guests)
            DecryptSensitiveFields(guest);

        return guests;
    }

    public async Task AddAsync(Guest guest, CancellationToken ct)
    {
        EncryptSensitiveFields(guest);
        db.Guests.Add(guest);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Guest guest, CancellationToken ct)
    {
        EncryptSensitiveFields(guest);
        db.Guests.Update(guest);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var guest = await db.Guests.FindAsync([id], ct);
        if (guest is not null)
        {
            db.Guests.Remove(guest);
            await db.SaveChangesAsync(ct);
        }
    }

    private void EncryptSensitiveFields(Guest guest)
    {
        if (!string.IsNullOrEmpty(guest.MedicalNotes))
            guest.MedicalNotes = encryption.Encrypt(guest.MedicalNotes);

        if (!string.IsNullOrEmpty(guest.Allergies))
            guest.Allergies = encryption.Encrypt(guest.Allergies);
    }

    private void DecryptSensitiveFields(Guest guest)
    {
        if (!string.IsNullOrEmpty(guest.MedicalNotes))
            guest.MedicalNotes = encryption.Decrypt(guest.MedicalNotes);

        if (!string.IsNullOrEmpty(guest.Allergies))
            guest.Allergies = encryption.Decrypt(guest.Allergies);
    }
}
