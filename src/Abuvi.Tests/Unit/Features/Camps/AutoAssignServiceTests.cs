using Abuvi.API.Features.Camps;
using FluentAssertions;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

public class AutoAssignServiceTests
{
    private static AssignmentFamilyResponse MakeFamily(
        Guid? id = null,
        int memberCount = 2,
        IEnumerable<(Guid accommodationId, int order)>? preferences = null)
    {
        var regId = id ?? Guid.NewGuid();
        var prefs = (preferences ?? [])
            .Select(p => new AccommodationPreferenceItem(p.accommodationId, p.order))
            .ToList();
        return new AssignmentFamilyResponse(
            regId, Guid.NewGuid(), "Familia Test", "Rep Test",
            memberCount, memberCount, 0, false, null, null, prefs);
    }

    private static AssignmentAccommodationResponse MakeAccommodation(
        Guid? id = null,
        int? capacity = 4,
        AccommodationType type = AccommodationType.Lodge,
        bool countByFamily = false)
        => new(id ?? Guid.NewGuid(), "Alojamiento Test", type, capacity, countByFamily,
            null, null, 0);

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
}
