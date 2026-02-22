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

        var price = _sut.GetPriceForCategory(AgeCategory.Baby, edition);

        price.Should().Be(edition.PricePerBaby);
    }

    [Fact]
    public void GetPriceForCategory_Child_ReturnsPricePerChild()
    {
        var edition = CreateEditionWithGlobalRanges();

        var price = _sut.GetPriceForCategory(AgeCategory.Child, edition);

        price.Should().Be(edition.PricePerChild);
    }

    [Fact]
    public void GetPriceForCategory_Adult_ReturnsPricePerAdult()
    {
        var edition = CreateEditionWithGlobalRanges();

        var price = _sut.GetPriceForCategory(AgeCategory.Adult, edition);

        price.Should().Be(edition.PricePerAdult);
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
