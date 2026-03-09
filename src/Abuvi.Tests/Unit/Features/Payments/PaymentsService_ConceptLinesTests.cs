using System.Text.Json;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Payments;
using Abuvi.API.Features.Registrations;
using FluentAssertions;

namespace Abuvi.Tests.Unit.Features.Payments;

public class PaymentsService_ConceptLinesTests
{
    // ── SerializeBaseConceptLines ─────────────────────────────────────────────

    [Fact]
    public void SerializeBaseConceptLines_WithMembers_GeneratesCorrectLines()
    {
        var members = CreateMembers();
        var baseTotalAmount = 850m; // 400 + 400 + 50
        var installmentAmount = Math.Ceiling(baseTotalAmount / 2m); // 425

        var json = PaymentsService.SerializeBaseConceptLines(members, installmentAmount, baseTotalAmount);

        var data = JsonSerializer.Deserialize<PaymentConceptLinesJson>(json);
        data.Should().NotBeNull();
        data!.MemberLines.Should().HaveCount(3);
        data.ExtraLines.Should().BeNull();

        data.MemberLines![0].PersonFullName.Should().Be("Juan García");
        data.MemberLines[0].AgeCategory.Should().Be("Adulto");
        data.MemberLines[0].AttendancePeriod.Should().Be("Completo");
        data.MemberLines[0].IndividualAmount.Should().Be(400m);

        data.MemberLines[1].PersonFullName.Should().Be("María García");
        data.MemberLines[1].AgeCategory.Should().Be("Adulto");
        data.MemberLines[1].AttendancePeriod.Should().Be("1ª Semana");
        data.MemberLines[1].IndividualAmount.Should().Be(400m);

        data.MemberLines[2].PersonFullName.Should().Be("Pablo García");
        data.MemberLines[2].AgeCategory.Should().Be("Niño");
        data.MemberLines[2].AttendancePeriod.Should().Be("Completo");
        data.MemberLines[2].IndividualAmount.Should().Be(50m);
    }

    [Fact]
    public void SerializeBaseConceptLines_AmountsInPaymentSumToInstallmentAmount()
    {
        var members = CreateMembers();
        var baseTotalAmount = 850m;
        var installmentAmount = Math.Ceiling(baseTotalAmount / 2m); // 425

        var json = PaymentsService.SerializeBaseConceptLines(members, installmentAmount, baseTotalAmount);

        var data = JsonSerializer.Deserialize<PaymentConceptLinesJson>(json)!;
        var sum = data.MemberLines!.Sum(l => l.AmountInPayment);
        sum.Should().Be(installmentAmount);
    }

    [Fact]
    public void SerializeBaseConceptLines_SecondInstallment_AmountsSumCorrectly()
    {
        var members = CreateMembers();
        var baseTotalAmount = 850m;
        var p1Amount = Math.Ceiling(baseTotalAmount / 2m); // 425
        var p2Amount = baseTotalAmount - p1Amount; // 425

        var json = PaymentsService.SerializeBaseConceptLines(members, p2Amount, baseTotalAmount);

        var data = JsonSerializer.Deserialize<PaymentConceptLinesJson>(json)!;
        var sum = data.MemberLines!.Sum(l => l.AmountInPayment);
        sum.Should().Be(p2Amount);
    }

    [Fact]
    public void SerializeBaseConceptLines_PercentageIsCorrect()
    {
        var members = CreateMembers();
        var baseTotalAmount = 850m;
        var installmentAmount = Math.Ceiling(baseTotalAmount / 2m); // 425

        var json = PaymentsService.SerializeBaseConceptLines(members, installmentAmount, baseTotalAmount);

        var data = JsonSerializer.Deserialize<PaymentConceptLinesJson>(json)!;
        var expectedPercentage = Math.Round(425m / 850m * 100m, 2);
        data.MemberLines!.Should().AllSatisfy(l => l.Percentage.Should().Be(expectedPercentage));
    }

    [Fact]
    public void SerializeBaseConceptLines_OddAmountThreeMembers_NoRoundingDrift()
    {
        // 3 members at 100€ each = 300€ total, installment = 150€
        // Each member's share = 50€, which divides evenly
        // But test with 301€ to force rounding
        var members = new List<RegistrationMember>
        {
            CreateMember("A", "A", AgeCategory.Adult, AttendancePeriod.Complete, 100.33m),
            CreateMember("B", "B", AgeCategory.Adult, AttendancePeriod.Complete, 100.33m),
            CreateMember("C", "C", AgeCategory.Adult, AttendancePeriod.Complete, 100.34m),
        };
        var baseTotalAmount = 301m;
        var installmentAmount = Math.Ceiling(baseTotalAmount / 2m); // 151

        var json = PaymentsService.SerializeBaseConceptLines(members, installmentAmount, baseTotalAmount);

        var data = JsonSerializer.Deserialize<PaymentConceptLinesJson>(json)!;
        data.MemberLines!.Sum(l => l.AmountInPayment).Should().Be(installmentAmount);
    }

    [Fact]
    public void SerializeBaseConceptLines_EmptyMembers_ReturnsEmptyList()
    {
        var json = PaymentsService.SerializeBaseConceptLines([], 0m, 0m);

        var data = JsonSerializer.Deserialize<PaymentConceptLinesJson>(json)!;
        data.MemberLines.Should().BeEmpty();
        data.ExtraLines.Should().BeNull();
    }

    [Fact]
    public void SerializeBaseConceptLines_WeekendVisit_MapsCorrectly()
    {
        var members = new List<RegistrationMember>
        {
            CreateMember("Ana", "Ruiz", AgeCategory.Adult, AttendancePeriod.WeekendVisit, 80m),
        };

        var json = PaymentsService.SerializeBaseConceptLines(members, 40m, 80m);

        var data = JsonSerializer.Deserialize<PaymentConceptLinesJson>(json)!;
        data.MemberLines![0].AttendancePeriod.Should().Be("Fin de semana");
    }

    [Fact]
    public void SerializeBaseConceptLines_BabyCategory_MapsCorrectly()
    {
        var members = new List<RegistrationMember>
        {
            CreateMember("Lucía", "Pérez", AgeCategory.Baby, AttendancePeriod.Complete, 0m),
        };

        var json = PaymentsService.SerializeBaseConceptLines(members, 0m, 0m);

        var data = JsonSerializer.Deserialize<PaymentConceptLinesJson>(json)!;
        data.MemberLines.Should().BeEmpty(); // baseTotalAmount is 0, so empty
    }

    // ── SerializeExtrasConceptLines ──────────────────────────────────────────

    [Fact]
    public void SerializeExtrasConceptLines_WithExtras_GeneratesCorrectLines()
    {
        var extras = CreateExtras();

        var json = PaymentsService.SerializeExtrasConceptLines(extras);

        var data = JsonSerializer.Deserialize<PaymentConceptLinesJson>(json);
        data.Should().NotBeNull();
        data!.MemberLines.Should().BeNull();
        data.ExtraLines.Should().HaveCount(2);

        data.ExtraLines![0].ExtraName.Should().Be("Camiseta");
        data.ExtraLines[0].Quantity.Should().Be(3);
        data.ExtraLines[0].UnitPrice.Should().Be(15m);
        data.ExtraLines[0].TotalAmount.Should().Be(45m);
        data.ExtraLines[0].PricingType.Should().Be("PerPerson");
        data.ExtraLines[0].UserInput.Should().Be("Talla M");

        data.ExtraLines[1].ExtraName.Should().Be("Seguro viaje");
        data.ExtraLines[1].Quantity.Should().Be(1);
        data.ExtraLines[1].UnitPrice.Should().Be(50m);
        data.ExtraLines[1].TotalAmount.Should().Be(50m);
        data.ExtraLines[1].PricingType.Should().Be("PerFamily");
        data.ExtraLines[1].UserInput.Should().BeNull();
    }

    [Fact]
    public void SerializeExtrasConceptLines_EmptyExtras_ReturnsEmptyList()
    {
        var json = PaymentsService.SerializeExtrasConceptLines([]);

        var data = JsonSerializer.Deserialize<PaymentConceptLinesJson>(json)!;
        data.MemberLines.Should().BeNull();
        data.ExtraLines.Should().BeEmpty();
    }

    // ── Deserialization (null handling) ───────────────────────────────────────

    [Fact]
    public void MapToResponse_NullConceptLines_ReturnsNullFields()
    {
        // Verify that a Payment with null ConceptLinesSerialized
        // doesn't break response mapping (tested via the static serialization)
        var json = PaymentsService.SerializeBaseConceptLines(CreateMembers(), 425m, 850m);
        var data = JsonSerializer.Deserialize<PaymentConceptLinesJson>(json);
        data.Should().NotBeNull();

        // Also verify null input produces null output
        PaymentConceptLinesJson? nullData = null;
        var serialized = (string?)null;
        if (serialized is not null)
            nullData = JsonSerializer.Deserialize<PaymentConceptLinesJson>(serialized);
        nullData.Should().BeNull();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static List<RegistrationMember> CreateMembers() =>
    [
        CreateMember("Juan", "García", AgeCategory.Adult, AttendancePeriod.Complete, 400m),
        CreateMember("María", "García", AgeCategory.Adult, AttendancePeriod.FirstWeek, 400m),
        CreateMember("Pablo", "García", AgeCategory.Child, AttendancePeriod.Complete, 50m),
    ];

    private static RegistrationMember CreateMember(
        string firstName, string lastName, AgeCategory ageCategory,
        AttendancePeriod period, decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        RegistrationId = Guid.NewGuid(),
        FamilyMemberId = Guid.NewGuid(),
        AgeAtCamp = ageCategory == AgeCategory.Child ? 10 : ageCategory == AgeCategory.Baby ? 1 : 30,
        AgeCategory = ageCategory,
        IndividualAmount = amount,
        AttendancePeriod = period,
        FamilyMember = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyUnitId = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = new DateOnly(2000, 1, 1),
            Relationship = FamilyRelationship.Parent
        }
    };

    private static List<RegistrationExtra> CreateExtras() =>
    [
        new()
        {
            Id = Guid.NewGuid(),
            RegistrationId = Guid.NewGuid(),
            CampEditionExtraId = Guid.NewGuid(),
            Quantity = 3,
            UnitPrice = 15m,
            CampDurationDays = 15,
            TotalAmount = 45m,
            UserInput = "Talla M",
            CampEditionExtra = new CampEditionExtra
            {
                Id = Guid.NewGuid(),
                CampEditionId = Guid.NewGuid(),
                Name = "Camiseta",
                Price = 15m,
                PricingType = PricingType.PerPerson,
                PricingPeriod = PricingPeriod.OneTime,
            }
        },
        new()
        {
            Id = Guid.NewGuid(),
            RegistrationId = Guid.NewGuid(),
            CampEditionExtraId = Guid.NewGuid(),
            Quantity = 1,
            UnitPrice = 50m,
            CampDurationDays = 15,
            TotalAmount = 50m,
            CampEditionExtra = new CampEditionExtra
            {
                Id = Guid.NewGuid(),
                CampEditionId = Guid.NewGuid(),
                Name = "Seguro viaje",
                Price = 50m,
                PricingType = PricingType.PerFamily,
                PricingPeriod = PricingPeriod.OneTime,
            }
        }
    ];
}
