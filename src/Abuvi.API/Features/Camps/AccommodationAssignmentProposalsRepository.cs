using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Camps;

public class AccommodationAssignmentProposalsRepository(AbuviDbContext db)
    : IAccommodationAssignmentProposalsRepository
{
    public async Task<AccommodationAssignmentProposal?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.AccommodationAssignmentProposals
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<List<AccommodationAssignmentProposal>> GetByCampEditionAsync(
        Guid campEditionId,
        CancellationToken ct = default)
        => await db.AccommodationAssignmentProposals
            .AsNoTracking()
            .Where(p => p.CampEditionId == campEditionId)
            .OrderByDescending(p => p.IsActive)
            .ThenBy(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(AccommodationAssignmentProposal proposal, CancellationToken ct = default)
    {
        db.AccommodationAssignmentProposals.Add(proposal);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AccommodationAssignmentProposal proposal, CancellationToken ct = default)
    {
        db.AccommodationAssignmentProposals.Update(proposal);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var proposal = await db.AccommodationAssignmentProposals.FindAsync([id], ct);
        if (proposal is not null)
        {
            db.AccommodationAssignmentProposals.Remove(proposal);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task ActivateAsync(Guid proposalId, Guid campEditionId, CancellationToken ct = default)
    {
        await db.AccommodationAssignmentProposals
            .Where(p => p.CampEditionId == campEditionId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.IsActive, false), ct);

        await db.AccommodationAssignmentProposals
            .Where(p => p.Id == proposalId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.IsActive, true), ct);
    }

    public async Task<int> CountAssignmentsAsync(Guid proposalId, CancellationToken ct = default)
        => await db.AccommodationAssignments
            .CountAsync(a => a.ProposalId == proposalId, ct);

    public async Task<int> CountRegistrationsAsync(Guid campEditionId, CancellationToken ct = default)
        => await db.Registrations
            .CountAsync(r => r.CampEditionId == campEditionId
                && r.Status != Registrations.RegistrationStatus.Cancelled, ct);

    public async Task<Dictionary<Guid, string>> GetUserDisplayNamesAsync(
        IEnumerable<Guid> userIds,
        CancellationToken ct = default)
    {
        var ids = userIds.ToList();
        if (ids.Count == 0) return [];
        return await db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}", ct);
    }

    public async Task CopyAssignmentsAsync(
        Guid sourceProposalId,
        Guid targetProposalId,
        Guid assignedByUserId,
        CancellationToken ct = default)
    {
        var sourceAssignments = await db.AccommodationAssignments
            .Where(a => a.ProposalId == sourceProposalId)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var copies = sourceAssignments.Select(a => new AccommodationAssignment
        {
            Id = Guid.NewGuid(),
            ProposalId = targetProposalId,
            RegistrationId = a.RegistrationId,
            AccommodationId = a.AccommodationId,
            AssignedByUserId = assignedByUserId,
            CreatedAt = now,
            UpdatedAt = now
        }).ToList();

        await db.AccommodationAssignments.AddRangeAsync(copies, ct);
        await db.SaveChangesAsync(ct);
    }
}
