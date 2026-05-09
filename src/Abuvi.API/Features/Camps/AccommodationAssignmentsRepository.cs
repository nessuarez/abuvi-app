using Abuvi.API.Common.Exceptions;
using Abuvi.API.Data;
using Abuvi.API.Features.Registrations;
using Abuvi.API.Features.Users;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Camps;

public class AccommodationAssignmentsRepository(AbuviDbContext db) : IAccommodationAssignmentsRepository
{
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
            .Include(r => r.AccommodationNeeds)
            .ToListAsync(ct);

        var repUserIds = registrations.Select(r => r.FamilyUnit.RepresentativeUserId).Distinct().ToList();
        var repUsers = await db.Users
            .AsNoTracking()
            .Where(u => repUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        // Build friend-link map: registrationId → list of friendly FamilyUnitIds
        var regIds = registrations.Select(r => r.Id).ToHashSet();
        var registrationFamilyMap = registrations.ToDictionary(r => r.Id, r => r.FamilyUnitId);
        var friendLinks = await db.RegistrationFriendLinks
            .AsNoTracking()
            .Where(fl => regIds.Contains(fl.RegistrationId))
            .ToListAsync(ct);
        var friendlyFamilyMap = friendLinks
            .GroupBy(fl => fl.RegistrationId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Guid>)g
                    .Select(fl => registrationFamilyMap.GetValueOrDefault(fl.LinkedRegistrationId))
                    .Where(id => id != Guid.Empty)
                    .Distinct()
                    .ToList()
            );

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
                    .ToList(),
                r.SpecialNeeds is { Length: > 0 },
                r.AccommodationNeeds.Select(n => n.AccommodationFeatureId).ToList(),
                friendlyFamilyMap.GetValueOrDefault(r.Id, [])
            );
        }).ToList();

        var accommodationResponses = accommodations
            .SelectMany(a => Enumerable.Range(0, a.Quantity).Select(unitIndex =>
                new AssignmentAccommodationResponse(
                    a.Id,
                    a.Quantity > 1 ? $"{a.Name} #{unitIndex + 1}" : a.Name,
                    a.AccommodationType,
                    a.Capacity,
                    a.CountByFamily,
                    a.ZoneId,
                    a.Zone?.Name,
                    a.SortOrder,
                    [],
                    a.Quantity,
                    a.Quantity > 1 ? unitIndex : (int?)null
                )
            ))
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToList();

        var assignmentEntries = assignments
            .Select(a => new AssignmentEntry(a.RegistrationId, a.AccommodationId, a.UnitIndex))
            .ToList();

        return new ProposalAssignmentStateResponse(proposalId, families, accommodationResponses, assignmentEntries);
    }

    public async Task AssignAsync(
        Guid proposalId,
        Guid registrationId,
        Guid accommodationId,
        int? unitIndex,
        Guid assignedByUserId,
        CancellationToken ct = default)
    {
        var existing = await db.AccommodationAssignments
            .FirstOrDefaultAsync(a => a.ProposalId == proposalId && a.RegistrationId == registrationId, ct);

        var now = DateTime.UtcNow;
        if (existing is not null)
        {
            existing.AccommodationId = accommodationId;
            existing.UnitIndex = unitIndex;
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
                UnitIndex = unitIndex,
                AssignedByUserId = assignedByUserId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await StampProposalModifierAsync(proposalId, assignedByUserId, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UnassignAsync(
        Guid proposalId,
        Guid registrationId,
        Guid modifiedByUserId,
        CancellationToken ct = default)
    {
        var assignment = await db.AccommodationAssignments
            .FirstOrDefaultAsync(a => a.ProposalId == proposalId && a.RegistrationId == registrationId, ct);
        if (assignment is not null) db.AccommodationAssignments.Remove(assignment);

        await StampProposalModifierAsync(proposalId, modifiedByUserId, ct);
        await db.SaveChangesAsync(ct);
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

                // Validate UnitIndex bounds
                foreach (var entry in assignments.Where(a => a.UnitIndex.HasValue))
                {
                    var acc = accommodations.FirstOrDefault(a => a.Id == entry.AccommodationId);
                    if (acc is not null && entry.UnitIndex >= acc.Quantity)
                        throw new BusinessRuleException(
                            $"El índice de unidad {entry.UnitIndex} no es válido para el alojamiento " +
                            $"'{acc.Name}' (máximo: {acc.Quantity - 1}).");
                }

                // Per-unit capacity validation
                foreach (var accGroup in assignments.GroupBy(a => a.AccommodationId))
                {
                    var acc = accommodations.First(a => a.Id == accGroup.Key);
                    if (acc.Capacity is null) continue;

                    foreach (var unitGroup in accGroup.GroupBy(a => a.UnitIndex))
                    {
                        if (acc.CountByFamily)
                        {
                            if (unitGroup.Count() > acc.Capacity)
                                throw new BusinessRuleException(
                                    $"La unidad '{acc.Name}'{UnitLabel(unitGroup.Key)} no tiene capacidad " +
                                    $"para {unitGroup.Count()} familias (máximo: {acc.Capacity}).");
                        }
                        else
                        {
                            var totalPersons = unitGroup.Sum(a => regSizes.GetValueOrDefault(a.RegistrationId, 0));
                            if (totalPersons > acc.Capacity)
                                throw new BusinessRuleException(
                                    $"La unidad '{acc.Name}'{UnitLabel(unitGroup.Key)} no tiene capacidad " +
                                    $"para {totalPersons} personas (máximo: {acc.Capacity}).");
                        }
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
                    UnitIndex = a.UnitIndex,
                    AssignedByUserId = assignedByUserId,
                    CreatedAt = now,
                    UpdatedAt = now
                }).ToList();

                await db.AccommodationAssignments.AddRangeAsync(newAssignments, ct);
            }

            await StampProposalModifierAsync(proposalId, assignedByUserId, ct);
            await db.SaveChangesAsync(ct);

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

    private async Task StampProposalModifierAsync(Guid proposalId, Guid userId, CancellationToken ct)
    {
        var proposal = await db.AccommodationAssignmentProposals
            .FirstOrDefaultAsync(p => p.Id == proposalId, ct);
        if (proposal is null) return;
        proposal.LastModifiedByUserId = userId;
        proposal.UpdatedAt = DateTime.UtcNow;
    }

    private static string UnitLabel(int? unitIndex)
        => unitIndex.HasValue ? $" #{unitIndex + 1}" : string.Empty;
}
