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
        var registrationToFamilyUnit = state.Families.ToDictionary(f => f.RegistrationId, f => f.FamilyUnitId);

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
                .OrderByDescending(acc => ScoreAccommodation(acc, family, occupancy, sizeMap, registrationToFamilyUnit, state))
                .ThenBy(acc => GetRemainingCapacity(acc, occupancy[acc.Id], sizeMap))
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

    private static int ScoreAccommodation(
        AssignmentAccommodationResponse acc,
        AssignmentFamilyResponse family,
        Dictionary<Guid, List<Guid>> occupancy,
        Dictionary<Guid, int> sizeMap,
        IReadOnlyDictionary<Guid, Guid> registrationToFamilyUnit,
        ProposalAssignmentStateResponse state)
    {
        var score = 0;

        // +5 per required feature covered by this accommodation
        score += family.RequiredFeatures.Count(req => acc.AvailableFeatures.Contains(req)) * 5;

        // +15 if a friendly family is already assigned to this exact accommodation
        foreach (var assignedRegId in occupancy[acc.Id])
        {
            if (!registrationToFamilyUnit.TryGetValue(assignedRegId, out var fuId)) continue;
            if (family.FriendlyFamilyUnitIds.Contains(fuId)) score += 15;
        }

        // +10 if a friendly family is in another accommodation of the same zone
        if (acc.ZoneId.HasValue)
        {
            var sameZoneAccIds = state.Accommodations
                .Where(a => a.ZoneId == acc.ZoneId && a.Id != acc.Id)
                .Select(a => a.Id)
                .ToHashSet();

            var bonusGiven = false;
            foreach (var sameZoneAccId in sameZoneAccIds)
            {
                if (bonusGiven) break;
                if (!occupancy.TryGetValue(sameZoneAccId, out var sameZoneRegIds)) continue;
                foreach (var regId in sameZoneRegIds)
                {
                    if (!registrationToFamilyUnit.TryGetValue(regId, out var fuId)) continue;
                    if (!family.FriendlyFamilyUnitIds.Contains(fuId)) continue;
                    score += 10;
                    bonusGiven = true;
                    break;
                }
            }
        }

        return score;
    }
}
