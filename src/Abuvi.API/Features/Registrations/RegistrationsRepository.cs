using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Registrations;

public interface IRegistrationsRepository
{
    Task<Registration?> GetByIdAsync(Guid id, CancellationToken ct);
    /// <summary>Includes Members.FamilyMember, Extras.CampEditionExtra, Payments, FamilyUnit, CampEdition.Camp</summary>
    Task<Registration?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Registration>> GetByFamilyUnitAsync(Guid familyUnitId, CancellationToken ct);
    Task<bool> ExistsAsync(Guid familyUnitId, Guid campEditionId, CancellationToken ct);
    /// <summary>Counts non-Cancelled registrations for capacity check.</summary>
    Task<int> CountActiveByEditionAsync(Guid campEditionId, CancellationToken ct);
    /// <summary>
    /// Counts members (not registrations) on-site for a given period.
    /// A Complete member counts toward both FirstWeek and SecondWeek.
    /// </summary>
    Task<int> CountConcurrentAttendeesByPeriodAsync(
        Guid campEditionId,
        AttendancePeriod period,
        CancellationToken ct
    );
    Task AddAsync(Registration registration, CancellationToken ct);
    Task UpdateAsync(Registration registration, CancellationToken ct);
    Task DeleteMembersByRegistrationIdAsync(Guid registrationId, CancellationToken ct);
}

public class RegistrationsRepository(AbuviDbContext db) : IRegistrationsRepository
{
    public async Task<Registration?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.Registrations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<Registration?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct)
        => await db.Registrations
            .AsNoTracking()
            .Include(r => r.FamilyUnit)
            .Include(r => r.CampEdition).ThenInclude(e => e.Camp)
            .Include(r => r.RegisteredByUser)
            .Include(r => r.Members).ThenInclude(m => m.FamilyMember)
            .Include(r => r.Extras).ThenInclude(e => e.CampEditionExtra)
            .Include(r => r.Payments)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Registration>> GetByFamilyUnitAsync(Guid familyUnitId, CancellationToken ct)
        => await db.Registrations
            .AsNoTracking()
            .Include(r => r.FamilyUnit)
            .Include(r => r.CampEdition).ThenInclude(e => e.Camp)
            .Include(r => r.Payments)
            .Where(r => r.FamilyUnitId == familyUnitId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public async Task<bool> ExistsAsync(Guid familyUnitId, Guid campEditionId, CancellationToken ct)
        => await db.Registrations
            .AnyAsync(r => r.FamilyUnitId == familyUnitId && r.CampEditionId == campEditionId, ct);

    public async Task<int> CountActiveByEditionAsync(Guid campEditionId, CancellationToken ct)
        => await db.Registrations
            .CountAsync(r => r.CampEditionId == campEditionId && r.Status != RegistrationStatus.Cancelled, ct);

    public async Task<int> CountConcurrentAttendeesByPeriodAsync(
        Guid campEditionId,
        AttendancePeriod period,
        CancellationToken ct)
        => await db.RegistrationMembers
            .Where(rm =>
                rm.Registration.CampEditionId == campEditionId &&
                rm.Registration.Status != RegistrationStatus.Cancelled &&
                (rm.AttendancePeriod == AttendancePeriod.Complete ||
                 rm.AttendancePeriod == period))
            .CountAsync(ct);

    public async Task AddAsync(Registration registration, CancellationToken ct)
    {
        registration.CreatedAt = DateTime.UtcNow;
        registration.UpdatedAt = DateTime.UtcNow;
        db.Registrations.Add(registration);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Registration registration, CancellationToken ct)
    {
        registration.UpdatedAt = DateTime.UtcNow;
        db.Registrations.Update(registration);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteMembersByRegistrationIdAsync(Guid registrationId, CancellationToken ct)
    {
        await db.RegistrationMembers
            .Where(m => m.RegistrationId == registrationId)
            .ExecuteDeleteAsync(ct);
    }
}
