using System.Text.Json;
using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.Camps;

namespace Abuvi.API.Features.Registrations;

public class RegistrationPricingService(IAssociationSettingsRepository settingsRepo)
{
    public record AgeRanges(int BabyMaxAge, int ChildMinAge, int ChildMaxAge, int AdultMinAge);

    /// <summary>
    /// Calculates age as of campStartDate.
    /// </summary>
    public int CalculateAge(DateOnly dateOfBirth, DateTime campStartDate)
    {
        var campDate = DateOnly.FromDateTime(campStartDate);
        var age = campDate.Year - dateOfBirth.Year;
        if (campDate < dateOfBirth.AddYears(age)) age--;
        return age;
    }

    /// <summary>
    /// Determines AgeCategory from age and configured ranges.
    /// Throws BusinessRuleException if age fits no category.
    /// </summary>
    public async Task<AgeCategory> GetAgeCategoryAsync(int age, CampEdition edition, CancellationToken ct)
    {
        AgeRanges ranges;

        if (edition.UseCustomAgeRanges &&
            edition.CustomBabyMaxAge.HasValue &&
            edition.CustomChildMinAge.HasValue &&
            edition.CustomChildMaxAge.HasValue &&
            edition.CustomAdultMinAge.HasValue)
        {
            ranges = new AgeRanges(
                edition.CustomBabyMaxAge.Value,
                edition.CustomChildMinAge.Value,
                edition.CustomChildMaxAge.Value,
                edition.CustomAdultMinAge.Value);
        }
        else
        {
            var setting = await settingsRepo.GetByKeyAsync("age_ranges", ct)
                ?? throw new BusinessRuleException("La configuración de rangos de edad no está disponible");

            ranges = JsonSerializer.Deserialize<AgeRanges>(
                setting.SettingValue,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new BusinessRuleException("La configuración de rangos de edad tiene un formato inválido");
        }

        if (age >= 0 && age <= ranges.BabyMaxAge) return AgeCategory.Baby;
        if (age >= ranges.ChildMinAge && age <= ranges.ChildMaxAge) return AgeCategory.Child;
        if (age >= ranges.AdultMinAge) return AgeCategory.Adult;

        throw new BusinessRuleException($"La edad {age} no corresponde a ninguna categoría de precio configurada");
    }

    /// <summary>
    /// Returns the price in euros for a given AgeCategory and AttendancePeriod from the edition pricing.
    /// </summary>
    public decimal GetPriceForCategory(AgeCategory category, AttendancePeriod period, CampEdition edition)
    {
        if (period is AttendancePeriod.FirstWeek or AttendancePeriod.SecondWeek)
        {
            if (edition.PricePerAdultWeek is null)
                throw new BusinessRuleException(
                    "Esta edición no permite inscripción parcial por semanas");

            return category switch
            {
                AgeCategory.Adult => edition.PricePerAdultWeek.Value,
                AgeCategory.Child => edition.PricePerChildWeek!.Value,
                AgeCategory.Baby  => edition.PricePerBabyWeek!.Value,
                _ => throw new ArgumentOutOfRangeException(nameof(category))
            };
        }

        if (period == AttendancePeriod.WeekendVisit)
        {
            if (edition.PricePerAdultWeekend is null)
                throw new BusinessRuleException(
                    "Esta edición no permite visitas de fin de semana");

            return category switch
            {
                AgeCategory.Adult => edition.PricePerAdultWeekend.Value,
                AgeCategory.Child => edition.PricePerChildWeekend!.Value,
                AgeCategory.Baby  => edition.PricePerBabyWeekend!.Value,
                _ => throw new ArgumentOutOfRangeException(nameof(category))
            };
        }

        // Complete: existing logic unchanged
        return category switch
        {
            AgeCategory.Baby  => edition.PricePerBaby,
            AgeCategory.Child => edition.PricePerChild,
            AgeCategory.Adult => edition.PricePerAdult,
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };
    }

    /// <summary>
    /// Overload for edition-level display (AvailableCampEditionResponse).
    /// Uses the edition's WeekendStartDate/WeekendEndDate as the reference window.
    /// </summary>
    public static int GetPeriodDays(AttendancePeriod period, CampEdition edition)
        => GetPeriodDays(period, edition, visitStart: null, visitEnd: null);

    /// <summary>
    /// Computes attendance days for a given period.
    /// CampEdition.StartDate/EndDate are DateTime; converts via DateOnly.FromDateTime.
    /// FirstWeek    = start → halfDate (exclusive of halfDate)
    /// SecondWeek   = halfDate → end (exclusive of halfDate)
    /// Complete     = start → end (full camp duration)
    /// WeekendVisit = visitStart → visitEnd if provided, else edition.WeekendStartDate → WeekendEndDate; max 3 days
    /// </summary>
    public static int GetPeriodDays(
        AttendancePeriod period, CampEdition edition,
        DateOnly? visitStart, DateOnly? visitEnd)
    {
        var startDate = DateOnly.FromDateTime(edition.StartDate);
        var endDate   = DateOnly.FromDateTime(edition.EndDate);
        var totalDays = endDate.DayNumber - startDate.DayNumber;
        var halfDate  = edition.HalfDate ?? startDate.AddDays(totalDays / 2);

        return period switch
        {
            AttendancePeriod.FirstWeek    => halfDate.DayNumber - startDate.DayNumber,
            AttendancePeriod.SecondWeek   => endDate.DayNumber  - halfDate.DayNumber,
            AttendancePeriod.Complete     => totalDays,
            AttendancePeriod.WeekendVisit => ComputeWeekendDays(edition, visitStart, visitEnd),
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };
    }

    private static int ComputeWeekendDays(
        CampEdition edition, DateOnly? visitStart, DateOnly? visitEnd)
    {
        // Use member-specific dates if provided; fall back to edition defaults
        var start = visitStart ?? edition.WeekendStartDate;
        if (start is null) return 0;
        var end = visitEnd ?? edition.WeekendEndDate ?? start.Value.AddDays(2);
        var days = end.DayNumber - start.Value.DayNumber;
        return Math.Min(3, Math.Max(0, days));  // enforce max 3
    }

    /// <summary>
    /// Tries to load global age ranges. Returns null if not configured.
    /// Used by GetAvailableEditionsAsync to populate AgeRangesInfo.
    /// </summary>
    public async Task<AgeRanges?> TryGetGlobalAgeRangesAsync(CancellationToken ct)
    {
        var setting = await settingsRepo.GetByKeyAsync("age_ranges", ct);
        if (setting is null) return null;

        return JsonSerializer.Deserialize<AgeRanges>(
            setting.SettingValue,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    /// <summary>
    /// Calculates extra total: handles PerPerson/PerFamily × OneTime/PerDay.
    /// </summary>
    public decimal CalculateExtraAmount(CampEditionExtra extra, int quantity, int campDurationDays)
    {
        var baseAmount = extra.PricingType == PricingType.PerPerson
            ? extra.Price * quantity
            : extra.Price;

        return extra.PricingPeriod == PricingPeriod.PerDay
            ? baseAmount * campDurationDays
            : baseAmount;
    }
}
