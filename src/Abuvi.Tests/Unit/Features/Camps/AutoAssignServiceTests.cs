using Abuvi.API.Features.Camps;
using FluentAssertions;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

public class AutoAssignServiceTests
{
    private static AssignmentFamilyResponse MakeFamily(
        Guid? id = null,
        Guid? familyUnitId = null,
        int memberCount = 2,
        IEnumerable<(Guid accommodationId, int order)>? preferences = null,
        IEnumerable<Guid>? requiredFeatures = null,
        IEnumerable<Guid>? friendlyFamilyUnitIds = null)
    {
        var regId = id ?? Guid.NewGuid();
        var prefs = (preferences ?? [])
            .Select(p => new AccommodationPreferenceItem(p.accommodationId, p.order))
            .ToList();
        return new AssignmentFamilyResponse(
            regId, familyUnitId ?? Guid.NewGuid(), "Familia Test", "Rep Test",
            memberCount, memberCount, 0, false, null, null, prefs,
            false,
            requiredFeatures?.ToList() ?? [],
            friendlyFamilyUnitIds?.ToList() ?? []);
    }

    private static AssignmentAccommodationResponse MakeAccommodation(
        Guid? id = null,
        int? capacity = 4,
        AccommodationType type = AccommodationType.Lodge,
        bool countByFamily = false,
        Guid? zoneId = null,
        IEnumerable<Guid>? availableFeatures = null)
        => new(id ?? Guid.NewGuid(), "Alojamiento Test", type, capacity, countByFamily,
            zoneId, null, 0, availableFeatures?.ToList() ?? [], 1, null);

    private static ProposalAssignmentStateResponse MakeState(
        IEnumerable<AssignmentFamilyResponse> families,
        IEnumerable<AssignmentAccommodationResponse> accommodations,
        IEnumerable<AssignmentEntry>? assignments = null)
        => new(Guid.NewGuid(), families.ToList(), accommodations.ToList(),
            assignments?.ToList() ?? []);

    [Fact]
    public void Compute_WithNoFamilies_ReturnsEmptyList()
    {
        var acc = MakeAccommodation();
        var state = MakeState([], [acc]);

        var result = AutoAssignService.Compute(state, overwriteExisting: false);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Compute_WithAllFamiliesHavingFirstPreference_AssignsToFirstPreference()
    {
        var accId = Guid.NewGuid();
        var acc = MakeAccommodation(accId, capacity: 10);
        var family = MakeFamily(preferences: [(accId, 1)]);
        var state = MakeState([family], [acc]);

        var result = AutoAssignService.Compute(state, overwriteExisting: false);

        result.Should().ContainSingle(e =>
            e.RegistrationId == family.RegistrationId && e.AccommodationId == accId);
    }

    [Fact]
    public void Compute_WhenFirstPreferenceOverCapacity_AssignsToSecondPreference()
    {
        var acc1Id = Guid.NewGuid();
        var acc2Id = Guid.NewGuid();
        var acc1 = MakeAccommodation(acc1Id, capacity: 1); // only 1 person fits
        var acc2 = MakeAccommodation(acc2Id, capacity: 10);

        var family1 = MakeFamily(memberCount: 1, preferences: [(acc1Id, 1), (acc2Id, 2)]);
        var family2 = MakeFamily(memberCount: 1, preferences: [(acc1Id, 1), (acc2Id, 2)]);

        var state = MakeState([family1, family2], [acc1, acc2]);

        var result = AutoAssignService.Compute(state, overwriteExisting: false);

        result.Should().HaveCount(2);
        // One family gets acc1, the other falls to acc2
        result.Should().Contain(e => e.AccommodationId == acc1Id);
        result.Should().Contain(e => e.AccommodationId == acc2Id);
    }

    [Fact]
    public void Compute_WhenAllPreferencesOverCapacity_AssignsToFallback()
    {
        var acc1Id = Guid.NewGuid();
        var acc2Id = Guid.NewGuid();
        var acc1 = MakeAccommodation(acc1Id, capacity: 1); // preferred but full
        var acc2 = MakeAccommodation(acc2Id, capacity: 10); // fallback

        var blocker = MakeFamily(memberCount: 1, preferences: [(acc1Id, 1)]);
        var family = MakeFamily(memberCount: 1, preferences: [(acc1Id, 1)]); // acc1 pref but acc1 full

        var state = MakeState([blocker, family], [acc1, acc2]);

        var result = AutoAssignService.Compute(state, overwriteExisting: false);

        result.Should().HaveCount(2);
        result.Should().Contain(e => e.RegistrationId == family.RegistrationId && e.AccommodationId == acc2Id);
    }

    [Fact]
    public void Compute_WhenNoCapacityAnywhere_LeavesUnassigned()
    {
        var accId = Guid.NewGuid();
        var acc = MakeAccommodation(accId, capacity: 1); // fits exactly 1 person
        var family1 = MakeFamily(memberCount: 1);
        var family2 = MakeFamily(memberCount: 1);

        var state = MakeState([family1, family2], [acc]);

        var result = AutoAssignService.Compute(state, overwriteExisting: false);

        result.Should().HaveCount(1); // second family cannot be placed
    }

    [Fact]
    public void Compute_WithByFamilyTypeAccommodation_CountsUnitsByFamily()
    {
        var accId = Guid.NewGuid();
        // Caravan counts by family unit (not persons), capacity=2 → can fit 2 families
        var acc = MakeAccommodation(accId, capacity: 2, type: AccommodationType.Caravan, countByFamily: true);
        var family1 = MakeFamily(memberCount: 5);
        var family2 = MakeFamily(memberCount: 5);
        var family3 = MakeFamily(memberCount: 5);

        var state = MakeState([family1, family2, family3], [acc]);

        var result = AutoAssignService.Compute(state, overwriteExisting: false);

        result.Should().HaveCount(2); // 3rd family cannot fit (capacity=2 families)
    }

    [Fact]
    public void Compute_WithByPersonTypeAccommodation_CountsByPersons()
    {
        var accId = Guid.NewGuid();
        // Lodge counts by persons, capacity=4
        var acc = MakeAccommodation(accId, capacity: 4, type: AccommodationType.Lodge, countByFamily: false);
        var family1 = MakeFamily(memberCount: 3);
        var family2 = MakeFamily(memberCount: 2); // 3+2=5 > 4, should not fit

        var state = MakeState([family1, family2], [acc]);

        var result = AutoAssignService.Compute(state, overwriteExisting: false);

        result.Should().HaveCount(1);
        result.Should().Contain(e => e.RegistrationId == family1.RegistrationId);
    }

    [Fact]
    public void Compute_WithOverwriteExistingFalse_KeepsAlreadyAssigned()
    {
        var accId = Guid.NewGuid();
        var acc = MakeAccommodation(accId, capacity: 1); // capacity: 1 person
        var family = MakeFamily(memberCount: 1);
        var existing = new AssignmentEntry(family.RegistrationId, accId);

        var state = MakeState([family], [acc], [existing]);

        var result = AutoAssignService.Compute(state, overwriteExisting: false);

        result.Should().ContainSingle(e =>
            e.RegistrationId == family.RegistrationId && e.AccommodationId == accId);
    }

    [Fact]
    public void Compute_WithOverwriteExistingTrue_IgnoresPreviousAssignments()
    {
        var acc1Id = Guid.NewGuid();
        var acc2Id = Guid.NewGuid();
        var acc1 = MakeAccommodation(acc1Id, capacity: 1);
        var acc2 = MakeAccommodation(acc2Id, capacity: 10);

        var family = MakeFamily(memberCount: 1, preferences: [(acc2Id, 1)]);
        // Existing assignment points to acc1, but overwrite=true should re-assign via preferences
        var existing = new AssignmentEntry(family.RegistrationId, acc1Id);

        var state = MakeState([family], [acc1, acc2], [existing]);

        var result = AutoAssignService.Compute(state, overwriteExisting: true);

        result.Should().ContainSingle(e =>
            e.RegistrationId == family.RegistrationId && e.AccommodationId == acc2Id);
    }

    [Fact]
    public void Compute_TightestFitHeuristic_PrefersTighterFit()
    {
        var smallAccId = Guid.NewGuid();
        var largeAccId = Guid.NewGuid();
        // small acc: capacity 2, large acc: capacity 10
        // A family of 2 should prefer the small one (tighter fit)
        var smallAcc = MakeAccommodation(smallAccId, capacity: 2);
        var largeAcc = MakeAccommodation(largeAccId, capacity: 10);

        var family = MakeFamily(memberCount: 2, preferences: [(smallAccId, 1), (largeAccId, 2)]);

        var state = MakeState([family], [smallAcc, largeAcc]);

        var result = AutoAssignService.Compute(state, overwriteExisting: false);

        // The algorithm should pick the preferred acc first (preference order wins over tightest fit)
        result.Should().ContainSingle(e =>
            e.RegistrationId == family.RegistrationId && e.AccommodationId == smallAccId);
    }

    [Fact]
    public void Compute_FallbackTightestFit_PicksSmallerAvailableAccommodation()
    {
        var smallAccId = Guid.NewGuid();
        var largeAccId = Guid.NewGuid();
        var smallAcc = MakeAccommodation(smallAccId, capacity: 3); // remaining: 3
        var largeAcc = MakeAccommodation(largeAccId, capacity: 20); // remaining: 20

        // Family has no preferences → fallback tightest fit
        var family = MakeFamily(memberCount: 2);

        var state = MakeState([family], [smallAcc, largeAcc]);

        var result = AutoAssignService.Compute(state, overwriteExisting: false);

        // Tightest fit = smallest remaining capacity → smallAcc
        result.Should().ContainSingle(e =>
            e.RegistrationId == family.RegistrationId && e.AccommodationId == smallAccId);
    }

    // ── Scoring: RequiredFeatures ─────────────────────────────────────────

    [Fact]
    public void Compute_PrefersAccommodationCoveringRequiredFeatures_InFallback()
    {
        var feat1 = Guid.NewGuid();
        var accWithFeatId = Guid.NewGuid();
        var accWithoutFeatId = Guid.NewGuid();
        var accWithFeat = MakeAccommodation(accWithFeatId, capacity: 10, availableFeatures: [feat1]);
        var accWithoutFeat = MakeAccommodation(accWithoutFeatId, capacity: 10);

        var family = MakeFamily(memberCount: 1, requiredFeatures: [feat1]);
        var state = MakeState([family], [accWithFeat, accWithoutFeat]);

        var result = AutoAssignService.Compute(state, overwriteExisting: false);

        result.Should().ContainSingle(e =>
            e.RegistrationId == family.RegistrationId && e.AccommodationId == accWithFeatId);
    }

    [Fact]
    public void Compute_ScoresMultipleRequiredFeatures_AccumulatesPoints()
    {
        var feat1 = Guid.NewGuid();
        var feat2 = Guid.NewGuid();
        var accBothId = Guid.NewGuid();
        var accOneId = Guid.NewGuid();
        var accBoth = MakeAccommodation(accBothId, capacity: 10, availableFeatures: [feat1, feat2]);
        var accOne = MakeAccommodation(accOneId, capacity: 3, availableFeatures: [feat1]); // tighter fit but fewer features

        var family = MakeFamily(memberCount: 1, requiredFeatures: [feat1, feat2]);
        var state = MakeState([family], [accBoth, accOne]);

        var result = AutoAssignService.Compute(state, overwriteExisting: false);

        // accBoth scores +10 (2 features), accOne scores +5 (1 feature) — accBoth wins despite larger remaining
        result.Should().ContainSingle(e =>
            e.RegistrationId == family.RegistrationId && e.AccommodationId == accBothId);
    }

    // ── Scoring: FriendlyFamilies ─────────────────────────────────────────

    [Fact]
    public void Compute_PrefersAccommodationWithFriendlyFamily_InFallback()
    {
        var friendFuId = Guid.NewGuid();
        var accWithFriendId = Guid.NewGuid();
        var accEmptyId = Guid.NewGuid();
        var accWithFriend = MakeAccommodation(accWithFriendId, capacity: 10);
        var accEmpty = MakeAccommodation(accEmptyId, capacity: 10);

        var friendReg = MakeFamily(familyUnitId: friendFuId, memberCount: 1);
        var family = MakeFamily(memberCount: 1, friendlyFamilyUnitIds: [friendFuId]);

        // Pre-assign friendReg to accWithFriend
        var existing = new AssignmentEntry(friendReg.RegistrationId, accWithFriendId);
        var state = MakeState([friendReg, family], [accWithFriend, accEmpty], [existing]);

        var result = AutoAssignService.Compute(state, overwriteExisting: false);

        result.Should().Contain(e =>
            e.RegistrationId == family.RegistrationId && e.AccommodationId == accWithFriendId);
    }

    [Fact]
    public void Compute_PrefersAccommodationInSameZoneAsFriendlyFamily_InFallback()
    {
        var zoneId = Guid.NewGuid();
        var friendFuId = Guid.NewGuid();
        var accSameZoneId = Guid.NewGuid();
        var accDiffZoneId = Guid.NewGuid();
        var accFriendZoneId = Guid.NewGuid();

        var accSameZone = MakeAccommodation(accSameZoneId, capacity: 10, zoneId: zoneId);
        var accDiffZone = MakeAccommodation(accDiffZoneId, capacity: 3); // tighter fit but no zone
        // accFriendZone is at capacity (1 person = the friend), so the family cannot go there
        var accFriendZone = MakeAccommodation(accFriendZoneId, capacity: 1, zoneId: zoneId);

        var friendReg = MakeFamily(familyUnitId: friendFuId, memberCount: 1);
        var family = MakeFamily(memberCount: 1, friendlyFamilyUnitIds: [friendFuId]);

        // Pre-assign friendReg to accFriendZone (same zone as accSameZone, now full)
        var existing = new AssignmentEntry(friendReg.RegistrationId, accFriendZoneId);
        var state = MakeState([friendReg, family], [accSameZone, accDiffZone, accFriendZone], [existing]);

        var result = AutoAssignService.Compute(state, overwriteExisting: false);

        // accFriendZone is full; accSameZone scores +10 (same-zone friendly), accDiffZone scores 0
        result.Should().Contain(e =>
            e.RegistrationId == family.RegistrationId && e.AccommodationId == accSameZoneId);
    }

    [Fact]
    public void Compute_SameAccommodationFriendBonus_OutweighsSameZoneBonus()
    {
        var zoneId = Guid.NewGuid();
        var friendFuId = Guid.NewGuid();
        var accDirectId = Guid.NewGuid();
        var accSameZoneId = Guid.NewGuid();

        // accDirect: friend already in it (+15), smaller remaining (tighter fit after score tie would pick this)
        var accDirect = MakeAccommodation(accDirectId, capacity: 10, zoneId: zoneId);
        var accSameZone = MakeAccommodation(accSameZoneId, capacity: 10, zoneId: zoneId);

        var friendReg = MakeFamily(familyUnitId: friendFuId, memberCount: 1);
        var family = MakeFamily(memberCount: 1, friendlyFamilyUnitIds: [friendFuId]);

        // Pre-assign friendReg to accDirect
        var existing = new AssignmentEntry(friendReg.RegistrationId, accDirectId);
        var state = MakeState([friendReg, family], [accDirect, accSameZone], [existing]);

        var result = AutoAssignService.Compute(state, overwriteExisting: false);

        // accDirect scores +15 (direct), accSameZone scores +10 (zone) — accDirect wins
        result.Should().Contain(e =>
            e.RegistrationId == family.RegistrationId && e.AccommodationId == accDirectId);
    }

    [Fact]
    public void Compute_MultipleFriendlyFamiliesInSameAccommodation_AccumulatesBonus()
    {
        var friend1FuId = Guid.NewGuid();
        var friend2FuId = Guid.NewGuid();
        var accId = Guid.NewGuid();
        var otherAccId = Guid.NewGuid();

        var acc = MakeAccommodation(accId, capacity: 20);
        var otherAcc = MakeAccommodation(otherAccId, capacity: 20);

        var friend1 = MakeFamily(familyUnitId: friend1FuId, memberCount: 1);
        var friend2 = MakeFamily(familyUnitId: friend2FuId, memberCount: 1);
        var family = MakeFamily(memberCount: 1, friendlyFamilyUnitIds: [friend1FuId, friend2FuId]);

        var existing = new[]
        {
            new AssignmentEntry(friend1.RegistrationId, accId),
            new AssignmentEntry(friend2.RegistrationId, accId),
        };
        var state = MakeState([friend1, friend2, family], [acc, otherAcc], existing);

        var result = AutoAssignService.Compute(state, overwriteExisting: false);

        // acc scores +30 (2 × +15), otherAcc scores 0
        result.Should().Contain(e =>
            e.RegistrationId == family.RegistrationId && e.AccommodationId == accId);
    }

    // ── Scoring: combined and regression ──────────────────────────────────

    [Fact]
    public void Compute_WithFeaturesAndFriendlyFamilies_SelectsHighestScoredFallback()
    {
        var feat = Guid.NewGuid();
        var friendFuId = Guid.NewGuid();
        var accFeatId = Guid.NewGuid();      // +5 for feature
        var accFriendId = Guid.NewGuid();    // +15 for friendly family
        var accBothId = Guid.NewGuid();      // +5 + 15 = +20

        var accFeat = MakeAccommodation(accFeatId, capacity: 10, availableFeatures: [feat]);
        var accFriend = MakeAccommodation(accFriendId, capacity: 10);
        var accBoth = MakeAccommodation(accBothId, capacity: 10, availableFeatures: [feat]);

        var friendReg = MakeFamily(familyUnitId: friendFuId, memberCount: 1);
        var family = MakeFamily(memberCount: 1, requiredFeatures: [feat], friendlyFamilyUnitIds: [friendFuId]);

        var existing = new[]
        {
            new AssignmentEntry(friendReg.RegistrationId, accFriendId),
            new AssignmentEntry(friendReg.RegistrationId, accBothId), // friend also in accBoth
        };

        // Assign friendReg to accBoth only (one reg can only be in one acc)
        var existing2 = new[] { new AssignmentEntry(friendReg.RegistrationId, accBothId) };
        var state = MakeState([friendReg, family], [accFeat, accFriend, accBoth], existing2);

        var result = AutoAssignService.Compute(state, overwriteExisting: false);

        result.Should().Contain(e =>
            e.RegistrationId == family.RegistrationId && e.AccommodationId == accBothId);
    }

    [Fact]
    public void Compute_EmptyRequiredFeaturesAndNoFriends_BehavesLikePreviousAlgorithm()
    {
        // Regression: families with no features/friends should still be assigned via tightest fit
        var smallAccId = Guid.NewGuid();
        var largeAccId = Guid.NewGuid();
        var smallAcc = MakeAccommodation(smallAccId, capacity: 3);
        var largeAcc = MakeAccommodation(largeAccId, capacity: 20);

        var family = MakeFamily(memberCount: 2);
        var state = MakeState([family], [smallAcc, largeAcc]);

        var result = AutoAssignService.Compute(state, overwriteExisting: false);

        result.Should().ContainSingle(e =>
            e.RegistrationId == family.RegistrationId && e.AccommodationId == smallAccId);
    }
}
