namespace Abuvi.API.Features.Camps;

public class CampEditionAccommodationsService(
    ICampEditionAccommodationsRepository repository,
    ICampEditionsRepository editionsRepository)
{
    public async Task<List<CampEditionAccommodationResponse>> GetByEditionAsync(
        Guid campEditionId,
        bool? activeOnly,
        CancellationToken ct = default)
    {
        var accommodations = await repository.GetByCampEditionAsync(campEditionId, activeOnly, ct);
        var result = new List<CampEditionAccommodationResponse>(accommodations.Count);

        foreach (var accommodation in accommodations)
        {
            var prefCount = await repository.GetPreferenceCountAsync(accommodation.Id, ct);
            var firstChoiceCount = await repository.GetFirstChoiceCountAsync(accommodation.Id, ct);
            result.Add(accommodation.ToResponse(prefCount, firstChoiceCount));
        }

        return result;
    }

    public async Task<CampEditionAccommodationResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var accommodation = await repository.GetByIdAsync(id, ct);
        if (accommodation is null) return null;

        var prefCount = await repository.GetPreferenceCountAsync(accommodation.Id, ct);
        var firstChoiceCount = await repository.GetFirstChoiceCountAsync(accommodation.Id, ct);
        return accommodation.ToResponse(prefCount, firstChoiceCount);
    }

    public async Task<CampEditionAccommodationResponse> CreateAsync(
        Guid campEditionId,
        CreateCampEditionAccommodationRequest request,
        CancellationToken ct = default)
    {
        var edition = await editionsRepository.GetByIdAsync(campEditionId, ct);
        if (edition is null)
            throw new InvalidOperationException("La edición de campamento no fue encontrada");

        if (edition.Status is CampEditionStatus.Completed or CampEditionStatus.Closed)
            throw new InvalidOperationException(
                "No se pueden añadir alojamientos a una edición cerrada o completada");

        var accommodation = new CampEditionAccommodation
        {
            Id = Guid.NewGuid(),
            CampEditionId = campEditionId,
            Name = request.Name,
            AccommodationType = request.AccommodationType,
            Description = request.Description,
            Capacity = request.Capacity,
            IsActive = true,
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(accommodation, ct);
        return accommodation.ToResponse(currentPreferenceCount: 0, firstChoiceCount: 0);
    }

    public async Task<CampEditionAccommodationResponse> UpdateAsync(
        Guid id,
        UpdateCampEditionAccommodationRequest request,
        CancellationToken ct = default)
    {
        var accommodation = await repository.GetByIdAsync(id, ct);
        if (accommodation is null)
            throw new InvalidOperationException("El alojamiento de campamento no fue encontrado");

        accommodation.Name = request.Name;
        accommodation.AccommodationType = request.AccommodationType;
        accommodation.Description = request.Description;
        accommodation.Capacity = request.Capacity;
        accommodation.IsActive = request.IsActive;
        accommodation.SortOrder = request.SortOrder;
        accommodation.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(accommodation, ct);

        var prefCount = await repository.GetPreferenceCountAsync(id, ct);
        var firstChoiceCount = await repository.GetFirstChoiceCountAsync(id, ct);
        return accommodation.ToResponse(prefCount, firstChoiceCount);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var accommodation = await repository.GetByIdAsync(id, ct);
        if (accommodation is null) return false;

        var hasPreferences = await repository.HasPreferencesAsync(id, ct);
        if (hasPreferences)
            throw new InvalidOperationException(
                $"No se puede eliminar el alojamiento '{accommodation.Name}' porque ha sido " +
                "seleccionado en preferencias de inscripción. Considera desactivarlo en su lugar");

        await repository.DeleteAsync(id, ct);
        return true;
    }

    public async Task<CampEditionAccommodationResponse> ActivateAsync(Guid id, CancellationToken ct = default)
    {
        var accommodation = await repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("El alojamiento de campamento no fue encontrado");

        accommodation.IsActive = true;
        accommodation.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(accommodation, ct);

        var prefCount = await repository.GetPreferenceCountAsync(id, ct);
        var firstChoiceCount = await repository.GetFirstChoiceCountAsync(id, ct);
        return accommodation.ToResponse(prefCount, firstChoiceCount);
    }

    public async Task<CampEditionAccommodationResponse> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var accommodation = await repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("El alojamiento de campamento no fue encontrado");

        accommodation.IsActive = false;
        accommodation.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(accommodation, ct);

        var prefCount = await repository.GetPreferenceCountAsync(id, ct);
        var firstChoiceCount = await repository.GetFirstChoiceCountAsync(id, ct);
        return accommodation.ToResponse(prefCount, firstChoiceCount);
    }
}

internal static class CampEditionAccommodationExtensions
{
    public static CampEditionAccommodationResponse ToResponse(
        this CampEditionAccommodation a,
        int currentPreferenceCount,
        int firstChoiceCount)
        => new(
            a.Id,
            a.CampEditionId,
            a.Name,
            a.AccommodationType,
            a.Description,
            a.Capacity,
            a.IsActive,
            a.SortOrder,
            currentPreferenceCount,
            firstChoiceCount,
            a.CreatedAt,
            a.UpdatedAt
        );
}
