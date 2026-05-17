using Abuvi.API.Common.Exceptions;
using Abuvi.API.Data;
using Abuvi.API.Features.Registrations;
using Abuvi.API.Features.Users;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Camps;

public class AccommodationAssignmentReportsService(AbuviDbContext db)
{
    public async Task<List<AssignmentReportGroupResponse>> GetByTypeAsync(
        Guid campEditionId,
        Guid proposalId,
        CancellationToken ct = default)
    {
        await EnsureProposalBelongsToEditionAsync(proposalId, campEditionId, ct);

        var assignments = await LoadAssignmentsWithFamiliesAsync(campEditionId, proposalId, ct);
        var accommodations = await LoadAccommodationsAsync(campEditionId, ct);

        return accommodations
            .GroupBy(a => a.AccommodationType)
            .OrderBy(g => g.Key.ToString())
            .Select(typeGroup =>
            {
                var groupAccommodations = typeGroup.ToList();
                var accIds = groupAccommodations.Select(a => a.AccommodationId).ToHashSet();
                var totalCapacity = ComputeGroupCapacity(groupAccommodations);
                var groupAssignments = assignments.Where(x => accIds.Contains(x.AccommodationId)).ToList();
                var usedCapacity = ComputeUsedCapacity(groupAccommodations, groupAssignments);
                var families = BuildFamilyRows(groupAssignments);

                return new AssignmentReportGroupResponse(
                    typeGroup.Key.ToString(),
                    typeGroup.Key.ToString(),
                    totalCapacity,
                    usedCapacity,
                    families
                );
            })
            .ToList();
    }

    public async Task<List<AssignmentReportGroupResponse>> GetByZoneAsync(
        Guid campEditionId,
        Guid proposalId,
        CancellationToken ct = default)
    {
        await EnsureProposalBelongsToEditionAsync(proposalId, campEditionId, ct);

        var assignments = await LoadAssignmentsWithFamiliesAsync(campEditionId, proposalId, ct);
        var accommodations = await LoadAccommodationsAsync(campEditionId, ct);

        return accommodations
            .GroupBy(a => a.ZoneName ?? "Sin zona")
            .OrderBy(g => g.Key)
            .Select(zoneGroup =>
            {
                var groupAccommodations = zoneGroup.ToList();
                var accIds = groupAccommodations.Select(a => a.AccommodationId).ToHashSet();
                var totalCapacity = ComputeGroupCapacity(groupAccommodations);
                var groupAssignments = assignments.Where(x => accIds.Contains(x.AccommodationId)).ToList();
                var usedCapacity = ComputeUsedCapacity(groupAccommodations, groupAssignments);
                var families = BuildFamilyRows(groupAssignments);

                return new AssignmentReportGroupResponse(
                    zoneGroup.Key,
                    zoneGroup.Key,
                    totalCapacity,
                    usedCapacity,
                    families
                );
            })
            .ToList();
    }

    public async Task<List<AssignmentFamilyResponse>> GetUnassignedAsync(
        Guid campEditionId,
        Guid proposalId,
        CancellationToken ct = default)
    {
        await EnsureProposalBelongsToEditionAsync(proposalId, campEditionId, ct);

        var assignedRegIds = await db.AccommodationAssignments
            .AsNoTracking()
            .Where(a => a.ProposalId == proposalId)
            .Select(a => a.RegistrationId)
            .ToHashSetAsync(ct);

        var registrations = await db.Registrations
            .AsNoTracking()
            .Where(r => r.CampEditionId == campEditionId
                && r.Status != RegistrationStatus.Cancelled
                && !assignedRegIds.Contains(r.Id))
            .Include(r => r.FamilyUnit)
            .Include(r => r.Members)
            .Include(r => r.AccommodationPreferences)
            .ToListAsync(ct);

        var repUserIds = registrations.Select(r => r.FamilyUnit.RepresentativeUserId).Distinct().ToList();
        var repUsers = await db.Users
            .AsNoTracking()
            .Where(u => repUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        return registrations.Select(r =>
        {
            repUsers.TryGetValue(r.FamilyUnit.RepresentativeUserId, out var rep);
            return new AssignmentFamilyResponse(
                r.Id,
                r.FamilyUnitId,
                r.FamilyUnit.Name,
                rep is not null ? $"{rep.FirstName} {rep.LastName}" : string.Empty,
                r.Members.Count,
                r.Members.Count(m => m.AgeCategory == AgeCategory.Adult),
                r.Members.Count(m => m.AgeCategory == AgeCategory.Child),
                r.Members.Count(m => m.AgeCategory == AgeCategory.Baby),
                r.HasPet,
                r.SpecialNeeds,
                r.CampatesPreference,
                r.AccommodationPreferences
                    .OrderBy(p => p.PreferenceOrder)
                    .Select(p => new AccommodationPreferenceItem(p.CampEditionAccommodationId, p.PreferenceOrder))
                    .ToList(),
                r.SpecialNeeds is { Length: > 0 },
                [],
                []
            );
        }).ToList();
    }

    private async Task EnsureProposalBelongsToEditionAsync(
        Guid proposalId, Guid campEditionId, CancellationToken ct)
    {
        var belongs = await db.AccommodationAssignmentProposals
            .AnyAsync(p => p.Id == proposalId && p.CampEditionId == campEditionId, ct);
        if (!belongs)
            throw new NotFoundException("AccommodationAssignmentProposal", proposalId);
    }

    private async Task<List<ReportAssignmentRow>> LoadAssignmentsWithFamiliesAsync(
        Guid campEditionId, Guid proposalId, CancellationToken ct)
    {
        var assignments = await db.AccommodationAssignments
            .AsNoTracking()
            .Where(a => a.ProposalId == proposalId)
            .Include(a => a.Registration)
                .ThenInclude(r => r.FamilyUnit)
            .Include(a => a.Registration)
                .ThenInclude(r => r.Members)
            .Include(a => a.Accommodation)
                .ThenInclude(acc => acc.Zone)
            .ToListAsync(ct);

        var repUserIds = assignments
            .Select(a => a.Registration.FamilyUnit.RepresentativeUserId)
            .Distinct()
            .ToList();

        var repUsers = await db.Users
            .AsNoTracking()
            .Where(u => repUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        return assignments.Select(a =>
        {
            repUsers.TryGetValue(a.Registration.FamilyUnit.RepresentativeUserId, out var rep);
            return new ReportAssignmentRow(
                a.RegistrationId,
                a.AccommodationId,
                a.Registration.FamilyUnit.Name,
                rep is not null ? $"{rep.FirstName} {rep.LastName}" : string.Empty,
                a.Registration.Members.Count,
                a.Accommodation.Name,
                a.Accommodation.Zone?.Name,
                a.Accommodation.AccommodationType,
                a.Accommodation.Capacity
            );
        }).ToList();
    }

    private async Task<List<AccommodationReportItem>> LoadAccommodationsAsync(
        Guid campEditionId, CancellationToken ct)
        => await db.CampEditionAccommodations
            .AsNoTracking()
            .Where(a => a.CampEditionId == campEditionId && a.IsActive)
            .Include(a => a.Zone)
            .Select(a => new AccommodationReportItem(
                a.Id,
                a.Name,
                a.AccommodationType,
                a.Capacity,
                a.Zone != null ? a.Zone.Name : null,
                a.ZoneId,
                a.CountByFamily,
                a.Quantity))
            .ToListAsync(ct);

    private int ComputeGroupCapacity(List<AccommodationReportItem> accommodations)
        => accommodations.Sum(a => (a.Capacity ?? 0) * a.Quantity);

    private int ComputeUsedCapacity(
        List<AccommodationReportItem> accommodations,
        List<ReportAssignmentRow> assignments)
    {
        var total = 0;
        foreach (var acc in accommodations)
        {
            var accAssignments = assignments.Where(a => a.AccommodationId == acc.AccommodationId).ToList();
            total += acc.CountByFamily
                ? accAssignments.Count
                : accAssignments.Sum(a => a.MemberCount);
        }
        return total;
    }

    private static List<AssignmentReportFamilyRow> BuildFamilyRows(List<ReportAssignmentRow> rows)
        => rows.Select(r => new AssignmentReportFamilyRow(
            r.RegistrationId,
            r.FamilyName,
            r.RepresentativeName,
            r.MemberCount,
            r.AccommodationName,
            r.ZoneName
        )).ToList();

    private record ReportAssignmentRow(
        Guid RegistrationId,
        Guid AccommodationId,
        string FamilyName,
        string RepresentativeName,
        int MemberCount,
        string AccommodationName,
        string? ZoneName,
        AccommodationType AccommodationType,
        int? Capacity);

    private record AccommodationReportItem(
        Guid AccommodationId,
        string Name,
        AccommodationType AccommodationType,
        int? Capacity,
        string? ZoneName,
        Guid? ZoneId,
        bool CountByFamily,
        int Quantity);
}
