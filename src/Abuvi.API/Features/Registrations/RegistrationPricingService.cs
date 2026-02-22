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
    /// Returns the price in euros for a given AgeCategory from the edition pricing.
    /// </summary>
    public decimal GetPriceForCategory(AgeCategory category, CampEdition edition) =>
        category switch
        {
            AgeCategory.Baby => edition.PricePerBaby,
            AgeCategory.Child => edition.PricePerChild,
            AgeCategory.Adult => edition.PricePerAdult,
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };

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
