using System.Text.Json;

namespace Abuvi.API.Features.Camps;

/// <summary>
/// Service for managing association-wide settings
/// </summary>
public class AssociationSettingsService
{
    private readonly IAssociationSettingsRepository _repository;
    private const string AgeRangesKey = "age_ranges";

    public AssociationSettingsService(IAssociationSettingsRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Gets the current age ranges configuration
    /// </summary>
    public async Task<AgeRangesResponse?> GetAgeRangesAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _repository.GetByKeyAsync(AgeRangesKey, cancellationToken);

        if (settings == null)
        {
            return null;
        }

        var ageRanges = JsonSerializer.Deserialize<AgeRangesData>(settings.SettingValue, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (ageRanges == null)
        {
            return null;
        }

        return new AgeRangesResponse(
            BabyMaxAge: ageRanges.BabyMaxAge,
            ChildMinAge: ageRanges.ChildMinAge,
            ChildMaxAge: ageRanges.ChildMaxAge,
            AdultMinAge: ageRanges.AdultMinAge,
            UpdatedBy: settings.UpdatedBy,
            UpdatedAt: settings.UpdatedAt
        );
    }

    /// <summary>
    /// Updates the age ranges configuration
    /// </summary>
    public async Task<AgeRangesResponse> UpdateAgeRangesAsync(
        UpdateAgeRangesRequest request,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        // Validate age values are non-negative
        if (request.BabyMaxAge < 0 || request.ChildMinAge < 0 ||
            request.ChildMaxAge < 0 || request.AdultMinAge < 0)
        {
            throw new ArgumentException("Age values must be non-negative");
        }

        // Validate age ranges don't overlap
        if (request.BabyMaxAge >= request.ChildMinAge)
        {
            throw new ArgumentException("Age ranges must not overlap: baby max age must be less than child min age");
        }

        if (request.ChildMaxAge >= request.AdultMinAge)
        {
            throw new ArgumentException("Age ranges must not overlap: child max age must be less than adult min age");
        }

        // Create JSON value
        var ageRanges = new AgeRangesData
        {
            BabyMaxAge = request.BabyMaxAge,
            ChildMinAge = request.ChildMinAge,
            ChildMaxAge = request.ChildMaxAge,
            AdultMinAge = request.AdultMinAge
        };

        var jsonValue = JsonSerializer.Serialize(ageRanges, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Check if setting exists
        var existingSettings = await _repository.GetByKeyAsync(AgeRangesKey, cancellationToken);

        AssociationSettings settings;

        if (existingSettings == null)
        {
            // Create new setting
            settings = new AssociationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = AgeRangesKey,
                SettingValue = jsonValue,
                UpdatedBy = updatedByUserId
            };

            settings = await _repository.CreateAsync(settings, cancellationToken);
        }
        else
        {
            // Update existing setting
            existingSettings.SettingValue = jsonValue;
            existingSettings.UpdatedBy = updatedByUserId;

            settings = await _repository.UpdateAsync(existingSettings, cancellationToken);
        }

        return new AgeRangesResponse(
            BabyMaxAge: request.BabyMaxAge,
            ChildMinAge: request.ChildMinAge,
            ChildMaxAge: request.ChildMaxAge,
            AdultMinAge: request.AdultMinAge,
            UpdatedBy: updatedByUserId,
            UpdatedAt: settings.UpdatedAt
        );
    }

    /// <summary>
    /// Internal class for JSON serialization of age ranges
    /// </summary>
    private class AgeRangesData
    {
        public int BabyMaxAge { get; set; }
        public int ChildMinAge { get; set; }
        public int ChildMaxAge { get; set; }
        public int AdultMinAge { get; set; }
    }
}
