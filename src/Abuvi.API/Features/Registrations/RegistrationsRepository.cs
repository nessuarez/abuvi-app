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
    Task<(List<AdminRegistrationProjection> Items, int TotalCount, AdminRegistrationTotals Totals)>
        GetAdminPagedAsync(Guid campEditionId, int page, int pageSize, string? search, string? status, CancellationToken ct);
    Task AddAsync(Registration registration, CancellationToken ct);
    Task UpdateAsync(Registration registration, CancellationToken ct);
    Task AddMembersAsync(IEnumerable<RegistrationMember> members, CancellationToken ct);
    Task DeleteMembersByRegistrationIdAsync(Guid registrationId, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
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

    public async Task<(List<AdminRegistrationProjection> Items, int TotalCount, AdminRegistrationTotals Totals)>
        GetAdminPagedAsync(Guid campEditionId, int page, int pageSize, string? search, string? status, CancellationToken ct)
    {
        var query = from r in db.Registrations.AsNoTracking()
                    join fu in db.FamilyUnits on r.FamilyUnitId equals fu.Id
                    join u in db.Users on r.RegisteredByUserId equals u.Id
                    where r.CampEditionId == campEditionId
                    select new
                    {
                        r.Id,
                        FamilyUnitId = fu.Id,
                        FamilyUnitName = fu.Name,
                        RepresentativeUserId = u.Id,
                        RepresentativeFirstName = u.FirstName,
                        RepresentativeLastName = u.LastName,
                        RepresentativeEmail = u.Email,
                        r.Status,
                        MemberCount = db.RegistrationMembers.Count(m => m.RegistrationId == r.Id),
                        r.TotalAmount,
                        AmountPaid = db.Payments
                            .Where(p => p.RegistrationId == r.Id && p.Status == PaymentStatus.Completed)
                            .Sum(p => (decimal?)p.Amount) ?? 0m,
                        r.CreatedAt
                    };

        // Status filter
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RegistrationStatus>(status, true, out var statusEnum))
        {
            query = query.Where(x => x.Status == statusEnum);
        }

        // Search filter (case-insensitive on family name or representative name)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.FamilyUnitName.ToLower().Contains(term) ||
                (x.RepresentativeFirstName + " " + x.RepresentativeLastName).ToLower().Contains(term));
        }

        // Totals (computed AFTER filters, BEFORE pagination)
        var totalCount = await query.CountAsync(ct);

        var aggregateTotals = totalCount == 0
            ? new AdminRegistrationTotals(0, 0, 0, 0, 0)
            : await query.GroupBy(_ => 1).Select(g => new AdminRegistrationTotals(
                g.Count(),
                g.Sum(x => x.MemberCount),
                g.Sum(x => x.TotalAmount),
                g.Sum(x => x.AmountPaid),
                g.Sum(x => x.TotalAmount - x.AmountPaid)
            )).FirstAsync(ct);

        // Pagination
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var projections = items.Select(x => new AdminRegistrationProjection(
            x.Id, x.FamilyUnitId, x.FamilyUnitName,
            x.RepresentativeUserId, x.RepresentativeFirstName,
            x.RepresentativeLastName, x.RepresentativeEmail,
            x.Status, x.MemberCount, x.TotalAmount, x.AmountPaid, x.CreatedAt
        )).ToList();

        return (projections, totalCount, aggregateTotals);
    }

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
        // Use Entry().State instead of Update() to avoid recursively walking the
        // entire navigation graph, which would mark detached child entities
        // (Members, Extras, Payments, etc.) as Modified and attempt spurious UPDATEs.
        db.Entry(registration).State = EntityState.Modified;
        await db.SaveChangesAsync(ct);
    }

    public async Task AddMembersAsync(IEnumerable<RegistrationMember> members, CancellationToken ct)
    {
        db.RegistrationMembers.AddRange(members);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteMembersByRegistrationIdAsync(Guid registrationId, CancellationToken ct)
    {
        await db.RegistrationMembers
            .Where(m => m.RegistrationId == registrationId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var entity = await db.Registrations.FindAsync([id], ct);
        if (entity is null) return;
        db.Registrations.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
