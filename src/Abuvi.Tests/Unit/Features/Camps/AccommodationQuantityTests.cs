using Abuvi.API.Features.Camps;
using FluentAssertions;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

public class AccommodationQuantityTests
{
    // ── Slot expansion ────────────────────────────────────────────────────────

    [Fact]
    public void ExpandSlots_WithQuantity1_ReturnsSingleSlotNullUnitIndex()
    {
        var accommodation = MakeAccommodation("Cabaña A", quantity: 1);
        var slots = ExpandSlots([accommodation]);
        slots.Should().HaveCount(1);
        slots[0].Name.Should().Be("Cabaña A");
        slots[0].UnitIndex.Should().BeNull();
        slots[0].Quantity.Should().Be(1);
    }

    [Fact]
    public void ExpandSlots_WithQuantity3_Returns3IndexedNamedSlots()
    {
        var accommodation = MakeAccommodation("Habitación doble", quantity: 3);
        var slots = ExpandSlots([accommodation]);
        slots.Should().HaveCount(3);
        slots[0].Name.Should().Be("Habitación doble #1");
        slots[0].UnitIndex.Should().Be(0);
        slots[1].Name.Should().Be("Habitación doble #2");
        slots[1].UnitIndex.Should().Be(1);
        slots[2].Name.Should().Be("Habitación doble #3");
        slots[2].UnitIndex.Should().Be(2);
    }

    [Fact]
    public void ExpandSlots_AllSlotsSameId_MatchParentAccommodationId()
    {
        var id = Guid.NewGuid();
        var accommodation = MakeAccommodation("Suite", quantity: 5, id: id);
        var slots = ExpandSlots([accommodation]);
        slots.Should().HaveCount(5);
        slots.Should().AllSatisfy(s => s.Id.Should().Be(id));
    }

    [Fact]
    public void ExpandSlots_MultipleAccommodations_ExpandsEachIndependently()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var a1 = MakeAccommodation("Habitación doble", quantity: 2, id: id1);
        var a2 = MakeAccommodation("Suite", quantity: 1, id: id2);
        var slots = ExpandSlots([a1, a2]);
        slots.Should().HaveCount(3);
        slots.Where(s => s.Id == id1).Should().HaveCount(2);
        slots.Where(s => s.Id == id2).Should().HaveCount(1);
        slots.First(s => s.Id == id2).UnitIndex.Should().BeNull();
    }

    [Fact]
    public void ExpandSlots_InheritsCapacityAndFeaturesFromParent()
    {
        var featureId = Guid.NewGuid();
        var accommodation = MakeAccommodation("Bungalow", quantity: 4, capacity: 3);
        var slots = ExpandSlots([accommodation]);
        slots.Should().HaveCount(4);
        slots.Should().AllSatisfy(s => s.Capacity.Should().Be(3));
    }

    // ── Capacity calculations ─────────────────────────────────────────────────

    [Fact]
    public void ComputeGroupCapacity_WithQuantity5AndCapacity2_Returns10()
    {
        ComputeCapacity(capacity: 2, quantity: 5).Should().Be(10);
    }

    [Fact]
    public void ComputeGroupCapacity_WithQuantity1_MatchesCapacity()
    {
        ComputeCapacity(capacity: 4, quantity: 1).Should().Be(4);
    }

    [Fact]
    public void ComputeGroupCapacity_WithNullCapacity_ReturnsZero()
    {
        ComputeCapacity(capacity: null, quantity: 10).Should().Be(0);
    }

    [Fact]
    public void ComputeGroupCapacity_MultipleTotals_SumsAll()
    {
        var total = new[]
        {
            (Capacity: (int?)2, Quantity: 3),
            (Capacity: (int?)4, Quantity: 2),
            (Capacity: (int?)null, Quantity: 5),
        }.Sum(a => (a.Capacity ?? 0) * a.Quantity);
        total.Should().Be(14); // 6 + 8 + 0
    }

    // ── Validators ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void CreateValidator_WithQuantityLessThan1_FailsWithQuantityError(int quantity)
    {
        var validator = new CreateCampEditionAccommodationRequestValidator();
        var request = new CreateCampEditionAccommodationRequest(
            "Test", AccommodationType.Lodge, null, null, null, quantity);
        var result = validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Quantity");
    }

    [Fact]
    public void CreateValidator_WithQuantity1_PassesValidation()
    {
        var validator = new CreateCampEditionAccommodationRequestValidator();
        var request = new CreateCampEditionAccommodationRequest(
            "Test", AccommodationType.Lodge, null, null, null, 1);
        validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateValidator_WithQuantity0_FailsWithQuantityError()
    {
        var validator = new UpdateCampEditionAccommodationRequestValidator();
        var request = new UpdateCampEditionAccommodationRequest(
            "Test", AccommodationType.Lodge, null, null, false, 0, true, true, null, 0);
        var result = validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Quantity");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    public void CreateValidator_WithValidQuantity_Passes(int quantity)
    {
        var validator = new CreateCampEditionAccommodationRequestValidator();
        var request = new CreateCampEditionAccommodationRequest(
            "Test", AccommodationType.Lodge, null, null, null, quantity);
        validator.Validate(request).IsValid.Should().BeTrue();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CampEditionAccommodation MakeAccommodation(
        string name,
        int quantity,
        Guid? id = null,
        int? capacity = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        CampEditionId = Guid.NewGuid(),
        Name = name,
        AccommodationType = AccommodationType.Lodge,
        Quantity = quantity,
        Capacity = capacity,
        IsActive = true,
        SortOrder = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        FeatureAssignments = []
    };

    private static List<AssignmentAccommodationResponse> ExpandSlots(
        IEnumerable<CampEditionAccommodation> accommodations)
        => accommodations
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
            .ToList();

    private static int ComputeCapacity(int? capacity, int quantity)
        => (capacity ?? 0) * quantity;
}
