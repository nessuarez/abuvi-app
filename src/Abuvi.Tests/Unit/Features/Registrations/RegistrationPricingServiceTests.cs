using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.Registrations;
using FluentAssertions;
using NSubstitute;

namespace Abuvi.Tests.Unit.Features.Registrations;

public class RegistrationPricingServiceTests
{
    private readonly IAssociationSettingsRepository _settingsRepo;
    private readonly RegistrationPricingService _sut;

    public RegistrationPricingServiceTests()
    {
        _settingsRepo = Substitute.For<IAssociationSettingsRepository>();
        _sut = new RegistrationPricingService(_settingsRepo);
    }

    // ── Age Calculation ────────────────────────────────────────────────────────

    [Fact]
    public void CalculateAge_WhenBirthdayIsOnCampStartDate_ReturnsExactAge()
    {
        var dob = new DateOnly(2010, 7, 15);
        var campStart = new DateTime(2020, 7, 15, 0, 0, 0, DateTimeKind.Utc);

        var age = _sut.CalculateAge(dob, campStart);

        age.Should().Be(10);
    }

    [Fact]
    public void CalculateAge_WhenBirthdayIsAfterCampStartDate_ReturnsAgeMinusOne()
    {
        var dob = new DateOnly(2010, 8, 1);
        var campStart = new DateTime(2020, 7, 15, 0, 0, 0, DateTimeKind.Utc);

        var age = _sut.CalculateAge(dob, campStart);

        age.Should().Be(9);
    }

    [Fact]
    public void CalculateAge_WhenBornOnLeapDay_HandlesCorrectly()
    {
        // Born on Feb 29, 2000 — camp starts Feb 28, 2020
        var dob = new DateOnly(2000, 2, 29);
        var campStart = new DateTime(2020, 2, 28, 0, 0, 0, DateTimeKind.Utc);

        var age = _sut.CalculateAge(dob, campStart);

        // Feb 28 is before Feb 29, so birthday hasn't happened yet → 19
        age.Should().Be(19);
    }

    // ── Age Category ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAgeCategory_WhenAgeFitsGlobalBabyRange_ReturnsBaby()
    {
        var edition = CreateEditionWithGlobalRanges();
        SetupGlobalAgeRanges(babyMaxAge: 3, childMinAge: 4, childMaxAge: 17, adultMinAge: 18);

        var category = await _sut.GetAgeCategoryAsync(2, edition, CancellationToken.None);

        category.Should().Be(AgeCategory.Baby);
    }

    [Fact]
    public async Task GetAgeCategory_WhenAgeFitsGlobalChildRange_ReturnsChild()
    {
        var edition = CreateEditionWithGlobalRanges();
        SetupGlobalAgeRanges(babyMaxAge: 3, childMinAge: 4, childMaxAge: 17, adultMinAge: 18);

        var category = await _sut.GetAgeCategoryAsync(10, edition, CancellationToken.None);

        category.Should().Be(AgeCategory.Child);
    }

    [Fact]
    public async Task GetAgeCategory_WhenAgeFitsGlobalAdultRange_ReturnsAdult()
    {
        var edition = CreateEditionWithGlobalRanges();
        SetupGlobalAgeRanges(babyMaxAge: 3, childMinAge: 4, childMaxAge: 17, adultMinAge: 18);

        var category = await _sut.GetAgeCategoryAsync(25, edition, CancellationToken.None);

        category.Should().Be(AgeCategory.Adult);
    }

    [Fact]
    public async Task GetAgeCategory_WhenAgeOutsideAllRanges_ThrowsBusinessRuleException()
    {
        // Age -1 fits no category
        var edition = CreateEditionWithGlobalRanges();
        SetupGlobalAgeRanges(babyMaxAge: 3, childMinAge: 4, childMaxAge: 17, adultMinAge: 18);

        Func<Task> act = async () => await _sut.GetAgeCategoryAsync(-1, edition, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task GetAgeCategory_WhenEditionHasCustomRanges_OverridesGlobalRanges()
    {
        var edition = new CampEdition
        {
            Id = Guid.NewGuid(),
            CampId = Guid.NewGuid(),
            Year = 2025,
            StartDate = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2025, 7, 14, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult = 500m,
            PricePerChild = 300m,
            PricePerBaby = 100m,
            UseCustomAgeRanges = true,
            CustomBabyMaxAge = 5,
            CustomChildMinAge = 6,
            CustomChildMaxAge = 15,
            CustomAdultMinAge = 16,
            Status = CampEditionStatus.Open,
            Camp = new Camp { Id = Guid.NewGuid(), Name = "Test Camp", PricePerAdult = 500m, PricePerChild = 300m, PricePerBaby = 100m }
        };

        // Age 5 should be Baby with custom ranges (customBabyMaxAge=5)
        var category = await _sut.GetAgeCategoryAsync(5, edition, CancellationToken.None);

        category.Should().Be(AgeCategory.Baby);
        // No call to settingsRepo since custom ranges are used
        await _settingsRepo.DidNotReceive().GetByKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Extra Amount Calculation ───────────────────────────────────────────────

    [Fact]
    public void CalculateExtraAmount_PerPersonOneTime_MultipliesPriceByQuantity()
    {
        var extra = CreateExtra(PricingType.PerPerson, PricingPeriod.OneTime, price: 50m);

        var amount = _sut.CalculateExtraAmount(extra, quantity: 3, campDurationDays: 7);

        amount.Should().Be(150m); // 50 × 3
    }

    [Fact]
    public void CalculateExtraAmount_PerPersonPerDay_MultipliesPriceByQuantityAndDays()
    {
        var extra = CreateExtra(PricingType.PerPerson, PricingPeriod.PerDay, price: 10m);

        var amount = _sut.CalculateExtraAmount(extra, quantity: 2, campDurationDays: 7);

        amount.Should().Be(140m); // 10 × 2 × 7
    }

    [Fact]
    public void CalculateExtraAmount_PerFamilyOneTime_ReturnsFixedPrice()
    {
        var extra = CreateExtra(PricingType.PerFamily, PricingPeriod.OneTime, price: 100m);

        var amount = _sut.CalculateExtraAmount(extra, quantity: 5, campDurationDays: 7);

        amount.Should().Be(100m); // fixed per family
    }

    [Fact]
    public void CalculateExtraAmount_PerFamilyPerDay_MultipliesPriceByDaysOnly()
    {
        var extra = CreateExtra(PricingType.PerFamily, PricingPeriod.PerDay, price: 20m);

        var amount = _sut.CalculateExtraAmount(extra, quantity: 3, campDurationDays: 7);

        amount.Should().Be(140m); // 20 × 7 (quantity ignored for PerFamily)
    }

    // ── GetPriceForCategory ───────────────────────────────────────────────────

    [Fact]
    public void GetPriceForCategory_Baby_ReturnsPricePerBaby()
    {
        var edition = CreateEditionWithGlobalRanges();

        var price = _sut.GetPriceForCategory(AgeCategory.Baby, AttendancePeriod.Complete, edition);

        price.Should().Be(edition.PricePerBaby);
    }

    [Fact]
    public void GetPriceForCategory_Child_ReturnsPricePerChild()
    {
        var edition = CreateEditionWithGlobalRanges();

        var price = _sut.GetPriceForCategory(AgeCategory.Child, AttendancePeriod.Complete, edition);

        price.Should().Be(edition.PricePerChild);
    }

    [Fact]
    public void GetPriceForCategory_Adult_ReturnsPricePerAdult()
    {
        var edition = CreateEditionWithGlobalRanges();

        var price = _sut.GetPriceForCategory(AgeCategory.Adult, AttendancePeriod.Complete, edition);

        price.Should().Be(edition.PricePerAdult);
    }

    [Fact]
    public void GetPriceForCategory_FirstWeek_Adult_ReturnsWeekPrice()
    {
        var edition = CreateEditionWithGlobalRanges();
        edition.PricePerAdultWeek = 110m;
        edition.PricePerChildWeek = 55m;
        edition.PricePerBabyWeek = 0m;

        var price = _sut.GetPriceForCategory(AgeCategory.Adult, AttendancePeriod.FirstWeek, edition);

        price.Should().Be(110m);
    }

    [Fact]
    public void GetPriceForCategory_SecondWeek_Child_ReturnsWeekPrice()
    {
        var edition = CreateEditionWithGlobalRanges();
        edition.PricePerAdultWeek = 110m;
        edition.PricePerChildWeek = 55m;
        edition.PricePerBabyWeek = 0m;

        var price = _sut.GetPriceForCategory(AgeCategory.Child, AttendancePeriod.SecondWeek, edition);

        price.Should().Be(55m);
    }

    [Fact]
    public void GetPriceForCategory_FirstWeek_WhenNoWeekPriceSet_ThrowsBusinessRuleException()
    {
        var edition = CreateEditionWithGlobalRanges(); // PricePerAdultWeek = null

        var act = () => _sut.GetPriceForCategory(AgeCategory.Adult, AttendancePeriod.FirstWeek, edition);

        act.Should().Throw<BusinessRuleException>()
           .WithMessage("Esta edición no permite inscripción parcial por semanas");
    }

    [Fact]
    public void GetPriceForCategory_WeekendVisit_Adult_ReturnsWeekendPrice()
    {
        var edition = CreateEditionWithGlobalRanges();
        edition.PricePerAdultWeekend = 40m;
        edition.PricePerChildWeekend = 20m;
        edition.PricePerBabyWeekend = 0m;

        var price = _sut.GetPriceForCategory(AgeCategory.Adult, AttendancePeriod.WeekendVisit, edition);

        price.Should().Be(40m);
    }

    [Fact]
    public void GetPriceForCategory_WeekendVisit_WhenNoWeekendPriceSet_ThrowsBusinessRuleException()
    {
        var edition = CreateEditionWithGlobalRanges(); // PricePerAdultWeekend = null

        var act = () => _sut.GetPriceForCategory(AgeCategory.Adult, AttendancePeriod.WeekendVisit, edition);

        act.Should().Throw<BusinessRuleException>()
           .WithMessage("Esta edición no permite visitas de fin de semana");
    }

    // ── GetPeriodDays ─────────────────────────────────────────────────────────

    [Fact]
    public void GetPeriodDays_Complete_ReturnsFullCampDuration()
    {
        var edition = CreateEditionWithGlobalRanges(); // 2025-07-01 to 2025-07-14 = 13 days

        var result = RegistrationPricingService.GetPeriodDays(AttendancePeriod.Complete, edition);

        result.Should().Be(13);
    }

    [Fact]
    public void GetPeriodDays_FirstWeek_WithExplicitHalfDate_ReturnsCorrectDays()
    {
        var edition = CreateEditionWithGlobalRanges();
        edition.HalfDate = new DateOnly(2025, 7, 8);

        var result = RegistrationPricingService.GetPeriodDays(AttendancePeriod.FirstWeek, edition);

        result.Should().Be(7); // July 1 → July 8 = 7 days
    }

    [Fact]
    public void GetPeriodDays_SecondWeek_WithExplicitHalfDate_ReturnsCorrectDays()
    {
        var edition = CreateEditionWithGlobalRanges();
        edition.HalfDate = new DateOnly(2025, 7, 8);

        var result = RegistrationPricingService.GetPeriodDays(AttendancePeriod.SecondWeek, edition);

        result.Should().Be(6); // July 8 → July 14 = 6 days
    }

    [Fact]
    public void GetPeriodDays_FirstWeek_WithNullHalfDate_UsesComputedMidpoint()
    {
        // 13-day camp: totalDays=13, halfDate = startDate.AddDays(13/2 = 6) = July 7
        // firstWeek = halfDate - start = 7 - 1 = 6 days
        var edition = CreateEditionWithGlobalRanges();

        var result = RegistrationPricingService.GetPeriodDays(AttendancePeriod.FirstWeek, edition);

        result.Should().Be(6);
    }

    [Fact]
    public void GetPeriodDays_SecondWeek_WithNullHalfDate_UsesComputedMidpoint()
    {
        // 13-day camp: halfDate = July 7, secondWeek = end - halfDate = 14 - 7 = 7 days
        var edition = CreateEditionWithGlobalRanges();

        var result = RegistrationPricingService.GetPeriodDays(AttendancePeriod.SecondWeek, edition);

        result.Should().Be(7);
    }

    [Fact]
    public void GetPeriodDays_FirstWeek_WithEvenTotalDays_SplitsEvenly()
    {
        // 14-day camp (July 1 → July 15): totalDays=14, halfDate = startDate.AddDays(7) = July 8
        // firstWeek = 7, secondWeek = 7
        var edition = CreateEditionWithGlobalRanges();
        edition.StartDate = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        edition.EndDate = new DateTime(2025, 7, 15, 0, 0, 0, DateTimeKind.Utc);

        var first = RegistrationPricingService.GetPeriodDays(AttendancePeriod.FirstWeek, edition);
        var second = RegistrationPricingService.GetPeriodDays(AttendancePeriod.SecondWeek, edition);

        first.Should().Be(7);
        second.Should().Be(7);
    }

    [Fact]
    public void GetPeriodDays_WeekendVisit_WithMemberSpecificDates_ReturnsCorrectDays()
    {
        var edition = CreateEditionWithGlobalRanges();

        var result = RegistrationPricingService.GetPeriodDays(
            AttendancePeriod.WeekendVisit, edition,
            visitStart: new DateOnly(2025, 7, 4),
            visitEnd: new DateOnly(2025, 7, 6));

        result.Should().Be(2); // July 4 → July 6 = 2-day difference
    }

    [Fact]
    public void GetPeriodDays_WeekendVisit_WithNullMemberDates_FallsBackToEditionDefaults()
    {
        var edition = CreateEditionWithGlobalRanges();
        edition.WeekendStartDate = new DateOnly(2025, 7, 5);
        edition.WeekendEndDate = new DateOnly(2025, 7, 7);

        var result = RegistrationPricingService.GetPeriodDays(
            AttendancePeriod.WeekendVisit, edition,
            visitStart: null, visitEnd: null);

        result.Should().Be(2);
    }

    [Fact]
    public void GetPeriodDays_WeekendVisit_WhenDurationExceedsThreeDays_CapsAtThree()
    {
        var edition = CreateEditionWithGlobalRanges();

        var result = RegistrationPricingService.GetPeriodDays(
            AttendancePeriod.WeekendVisit, edition,
            visitStart: new DateOnly(2025, 7, 4),
            visitEnd: new DateOnly(2025, 7, 10)); // 6-day diff → capped at 3

        result.Should().Be(3);
    }

    [Fact]
    public void GetPeriodDays_WeekendVisit_WithNullWeekendEndDate_DefaultsTwoExtraDays()
    {
        // visitEnd = null and edition.WeekendEndDate = null → end = start + 2
        var edition = CreateEditionWithGlobalRanges();
        edition.WeekendStartDate = new DateOnly(2025, 7, 5);
        edition.WeekendEndDate = null;

        var result = RegistrationPricingService.GetPeriodDays(
            AttendancePeriod.WeekendVisit, edition,
            visitStart: null, visitEnd: null);

        result.Should().Be(2);
    }

    [Fact]
    public void GetPeriodDays_WeekendVisit_WithNoWeekendStartDate_ReturnsZero()
    {
        var edition = CreateEditionWithGlobalRanges(); // WeekendStartDate = null

        var result = RegistrationPricingService.GetPeriodDays(
            AttendancePeriod.WeekendVisit, edition,
            visitStart: null, visitEnd: null);

        result.Should().Be(0);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CampEdition CreateEditionWithGlobalRanges() => new()
    {
        Id = Guid.NewGuid(),
        CampId = Guid.NewGuid(),
        Year = 2025,
        StartDate = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate = new DateTime(2025, 7, 14, 0, 0, 0, DateTimeKind.Utc),
        PricePerAdult = 500m,
        PricePerChild = 300m,
        PricePerBaby = 100m,
        UseCustomAgeRanges = false,
        Status = CampEditionStatus.Open,
        Camp = new Camp { Id = Guid.NewGuid(), Name = "Test Camp", PricePerAdult = 500m, PricePerChild = 300m, PricePerBaby = 100m }
    };

    private void SetupGlobalAgeRanges(int babyMaxAge, int childMinAge, int childMaxAge, int adultMinAge)
    {
        var json = $"{{\"babyMaxAge\":{babyMaxAge},\"childMinAge\":{childMinAge},\"childMaxAge\":{childMaxAge},\"adultMinAge\":{adultMinAge}}}";
        _settingsRepo.GetByKeyAsync("age_ranges", Arg.Any<CancellationToken>())
            .Returns(new AssociationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = "age_ranges",
                SettingValue = json,
                UpdatedAt = DateTime.UtcNow
            });
    }

    private static CampEditionExtra CreateExtra(PricingType type, PricingPeriod period, decimal price) =>
        new()
        {
            Id = Guid.NewGuid(),
            CampEditionId = Guid.NewGuid(),
            Name = "Test Extra",
            Price = price,
            PricingType = type,
            PricingPeriod = period,
            IsActive = true
        };
}
