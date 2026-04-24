using Abuvi.API.Common.Exceptions;
using Abuvi.API.Data;
using Abuvi.API.Features.Registrations;
using Abuvi.API.Features.Users;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Camps;

public class AccommodationAssignmentsRepository(AbuviDbContext db) : IAccommodationAssignmentsRepository
{
    private static readonly HashSet<AccommodationType> ByFamilyTypes =
        [AccommodationType.Caravan, AccommodationType.Tent];

    public async Task<ProposalAssignmentStateResponse> GetAssignmentStateAsync(
        Guid campEditionId,
        Guid proposalId,
        CancellationToken ct = default)
    {
        var registrations = await db.Registrations
            .AsNoTracking()
            .Where(r => r.CampEditionId == campEditionId && r.Status != RegistrationStatus.Cancelled)
            .Include(r => r.FamilyUnit)
            .Include(r => r.Members)
            .Include(r => r.AccommodationPreferences)
            .ToListAsync(ct);

        var repUserIds = registrations.Select(r => r.FamilyUnit.RepresentativeUserId).Distinct().ToList();
        var repUsers = await db.Users
            .AsNoTracking()
            .Where(u => repUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        var accommodations = await db.CampEditionAccommodations
            .AsNoTracking()
            .Where(a => a.CampEditionId == campEditionId && a.IsActive)
            .Include(a => a.Zone)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Name)
            .ToListAsync(ct);

        var assignments = await db.AccommodationAssignments
            .AsNoTracking()
            .Where(a => a.ProposalId == proposalId)
            .ToListAsync(ct);

        var families = registrations.Select(r =>
        {
            var adultCount = r.Members.Count(m => m.AgeCategory == AgeCategory.Adult);
            var childCount = r.Members.Count(m => m.AgeCategory == AgeCategory.Child);
            var babyCount = r.Members.Count(m => m.AgeCategory == AgeCategory.Baby);
            repUsers.TryGetValue(r.FamilyUnit.RepresentativeUserId, out var rep);
            var repName = rep is not null
                ? $"{rep.FirstName} {rep.LastName}"
                : string.Empty;

            return new AssignmentFamilyResponse(
                r.Id,
                r.FamilyUnitId,
                r.FamilyUnit.Name,
                repName,
                r.Members.Count,
                adultCount,
                childCount + babyCount,
                r.HasPet,
                r.SpecialNeeds,
                r.CampatesPreference,
                r.AccommodationPreferences
                    .OrderBy(p => p.PreferenceOrder)
                    .Select(p => new AccommodationPreferenceItem(p.CampEditionAccommodationId, p.PreferenceOrder))
                    .ToList()
            );
        }).ToList();

        var accommodationResponses = accommodations.Select(a => new AssignmentAccommodationResponse(
            a.Id,
            a.Name,
            a.AccommodationType,
            a.Capacity,
            ByFamilyTypes.Contains(a.AccommodationType),
            a.ZoneId,
            a.Zone?.Name,
            a.SortOrder
        )).ToList();

        var assignmentEntries = assignments
            .Select(a => new AssignmentEntry(a.RegistrationId, a.AccommodationId))
            .ToList();

        return new ProposalAssignmentStateResponse(proposalId, families, accommodationResponses, assignmentEntries);
    }

    public async Task AssignAsync(
        Guid proposalId,
        Guid registrationId,
        Guid accommodationId,
        Guid assignedByUserId,
        CancellationToken ct = default)
    {
        var existing = await db.AccommodationAssignments
            .FirstOrDefaultAsync(a => a.ProposalId == proposalId && a.RegistrationId == registrationId, ct);

        var now = DateTime.UtcNow;
        if (existing is not null)
        {
            existing.AccommodationId = accommodationId;
            existing.AssignedByUserId = assignedByUserId;
            existing.UpdatedAt = now;
            db.AccommodationAssignments.Update(existing);
        }
        else
        {
            db.AccommodationAssignments.Add(new AccommodationAssignment
            {
                Id = Guid.NewGuid(),
                ProposalId = proposalId,
                RegistrationId = registrationId,
                AccommodationId = accommodationId,
                AssignedByUserId = assignedByUserId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task UnassignAsync(Guid proposalId, Guid registrationId, CancellationToken ct = default)
    {
        await db.AccommodationAssignments
            .Where(a => a.ProposalId == proposalId && a.RegistrationId == registrationId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task BulkReplaceAsync(
        Guid proposalId,
        Guid campEditionId,
        IReadOnlyList<AssignmentEntry> assignments,
        Guid assignedByUserId,
        CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            if (assignments.Count > 0)
            {
                var requestedRegIds = assignments.Select(a => a.RegistrationId).ToHashSet();
                var validRegistrationIds = await db.Registrations
                    .Where(r => r.CampEditionId == campEditionId && requestedRegIds.Contains(r.Id))
                    .Select(r => r.Id)
                    .ToHashSetAsync(ct);

                if (requestedRegIds.Any(id => !validRegistrationIds.Contains(id)))
                    throw new BusinessRuleException(
                        "Algunas inscripciones no pertenecen a esta edición del campamento.");

                var accommodations = await db.CampEditionAccommodations
                    .Where(a => a.CampEditionId == campEditionId)
                    .ToListAsync(ct);

                var validAccIds = accommodations.Select(a => a.Id).ToHashSet();
                var requestedAccIds = assignments.Select(a => a.AccommodationId).ToHashSet();
                if (requestedAccIds.Any(id => !validAccIds.Contains(id)))
                    throw new BusinessRuleException(
                        "Alguno de los alojamientos no pertenece a esta edición del campamento.");

                var regSizes = await db.RegistrationMembers
                    .Where(m => requestedRegIds.Contains(m.RegistrationId))
                    .GroupBy(m => m.RegistrationId)
                    .Select(g => new { RegistrationId = g.Key, Size = g.Count() })
                    .ToDictionaryAsync(x => x.RegistrationId, x => x.Size, ct);

                foreach (var accGroup in assignments.GroupBy(a => a.AccommodationId))
                {
                    var acc = accommodations.First(a => a.Id == accGroup.Key);
                    if (acc.Capacity is null) continue;

                    if (ByFamilyTypes.Contains(acc.AccommodationType))
                    {
                        if (accGroup.Count() > acc.Capacity)
                            throw new BusinessRuleException(
                                $"El alojamiento '{acc.Name}' no tiene capacidad para {accGroup.Count()} familias " +
                                $"(máximo: {acc.Capacity}).");
                    }
                    else
                    {
                        var totalPersons = accGroup.Sum(a => regSizes.GetValueOrDefault(a.RegistrationId, 0));
                        if (totalPersons > acc.Capacity)
                            throw new BusinessRuleException(
                                $"El alojamiento '{acc.Name}' no tiene capacidad para {totalPersons} personas " +
                                $"(máximo: {acc.Capacity}).");
                    }
                }
            }

            await db.AccommodationAssignments
                .Where(a => a.ProposalId == proposalId)
                .ExecuteDeleteAsync(ct);

            if (assignments.Count > 0)
            {
                var now = DateTime.UtcNow;
                var newAssignments = assignments.Select(a => new AccommodationAssignment
                {
                    Id = Guid.NewGuid(),
                    ProposalId = proposalId,
                    RegistrationId = a.RegistrationId,
                    AccommodationId = a.AccommodationId,
                    AssignedByUserId = assignedByUserId,
                    CreatedAt = now,
                    UpdatedAt = now
                }).ToList();

                await db.AccommodationAssignments.AddRangeAsync(newAssignments, ct);
                await db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> ProposalBelongsToEditionAsync(
        Guid proposalId,
        Guid campEditionId,
        CancellationToken ct = default)
        => await db.AccommodationAssignmentProposals
            .AnyAsync(p => p.Id == proposalId && p.CampEditionId == campEditionId, ct);
}
