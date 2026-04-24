namespace Abuvi.API.Features.Camps;

public static class AutoAssignService
{
    private static readonly HashSet<AccommodationType> ByFamilyTypes =
        [AccommodationType.Caravan, AccommodationType.Tent];

    public static IReadOnlyList<AssignmentEntry> Compute(
        ProposalAssignmentStateResponse state,
        bool overwriteExisting)
    {
        var assignments = overwriteExisting
            ? new Dictionary<Guid, Guid>()
            : state.Assignments.ToDictionary(a => a.RegistrationId, a => a.AccommodationId);

        var occupancy = new Dictionary<Guid, List<Guid>>();
        foreach (var acc in state.Accommodations)
            occupancy[acc.Id] = [];

        foreach (var (regId, accId) in assignments)
        {
            if (occupancy.TryGetValue(accId, out var list))
                list.Add(regId);
        }

        var sizeMap = state.Families.ToDictionary(f => f.RegistrationId, f => f.MemberCount);

        var unassigned = state.Families
            .Where(f => !assignments.ContainsKey(f.RegistrationId))
            .OrderByDescending(f => f.MemberCount)
            .ToList();

        foreach (var family in unassigned)
        {
            var assigned = false;

            foreach (var pref in family.AccommodationPreferences.OrderBy(p => p.PreferenceOrder))
            {
                var target = state.Accommodations.FirstOrDefault(acc => acc.Id == pref.AccommodationId);
                if (target is null) continue;
                if (!HasCapacity(target, occupancy[target.Id], family.MemberCount, sizeMap)) continue;

                assignments[family.RegistrationId] = target.Id;
                occupancy[target.Id].Add(family.RegistrationId);
                assigned = true;
                break;
            }

            if (assigned) continue;

            var fallback = state.Accommodations
                .Where(acc => HasCapacity(acc, occupancy[acc.Id], family.MemberCount, sizeMap))
                .OrderBy(acc => GetRemainingCapacity(acc, occupancy[acc.Id], sizeMap))
                .FirstOrDefault();

            if (fallback is not null)
            {
                assignments[family.RegistrationId] = fallback.Id;
                occupancy[fallback.Id].Add(family.RegistrationId);
            }
        }

        return assignments
            .Select(kvp => new AssignmentEntry(kvp.Key, kvp.Value))
            .ToList();
    }

    private static bool HasCapacity(
        AssignmentAccommodationResponse acc,
        List<Guid> assignedRegIds,
        int familySize,
        Dictionary<Guid, int> sizeMap)
    {
        if (acc.Capacity is null) return true;
        var remaining = GetRemainingCapacity(acc, assignedRegIds, sizeMap);
        return acc.CountByFamily ? remaining >= 1 : remaining >= familySize;
    }

    private static int GetRemainingCapacity(
        AssignmentAccommodationResponse acc,
        List<Guid> assignedRegIds,
        Dictionary<Guid, int> sizeMap)
    {
        if (acc.Capacity is null) return int.MaxValue;
        var used = acc.CountByFamily
            ? assignedRegIds.Count
            : assignedRegIds.Sum(id => sizeMap.GetValueOrDefault(id, 0));
        return acc.Capacity.Value - used;
    }
}
