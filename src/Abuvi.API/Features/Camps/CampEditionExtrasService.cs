namespace Abuvi.API.Features.Camps;

public class CampEditionExtrasService(
    ICampEditionExtrasRepository repository,
    ICampEditionsRepository editionsRepository)
{
    public async Task<List<CampEditionExtraResponse>> GetByEditionAsync(
        Guid campEditionId,
        bool? activeOnly,
        CancellationToken ct = default)
    {
        var extras = await repository.GetByCampEditionAsync(campEditionId, activeOnly, ct);
        var result = new List<CampEditionExtraResponse>(extras.Count);

        foreach (var extra in extras)
        {
            var sold = await repository.GetQuantitySoldAsync(extra.Id, ct);
            result.Add(extra.ToResponse(sold));
        }

        return result;
    }

    public async Task<CampEditionExtraResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var extra = await repository.GetByIdAsync(id, ct);
        if (extra is null) return null;

        var sold = await repository.GetQuantitySoldAsync(extra.Id, ct);
        return extra.ToResponse(sold);
    }

    public async Task<CampEditionExtraResponse> CreateAsync(
        Guid campEditionId,
        CreateCampEditionExtraRequest request,
        CancellationToken ct = default)
    {
        var edition = await editionsRepository.GetByIdAsync(campEditionId, ct);
        if (edition is null)
            throw new InvalidOperationException("La edición de campamento no fue encontrada");

        if (edition.Status is CampEditionStatus.Completed or CampEditionStatus.Closed)
            throw new InvalidOperationException(
                "No se pueden añadir extras a una edición cerrada o completada");

        var extra = new CampEditionExtra
        {
            Id = Guid.NewGuid(),
            CampEditionId = campEditionId,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            PricingType = request.PricingType,
            PricingPeriod = request.PricingPeriod,
            IsRequired = request.IsRequired,
            IsActive = true,
            MaxQuantity = request.MaxQuantity,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(extra, ct);
        return extra.ToResponse(currentQuantitySold: 0);
    }

    public async Task<CampEditionExtraResponse> UpdateAsync(
        Guid id,
        UpdateCampEditionExtraRequest request,
        CancellationToken ct = default)
    {
        var extra = await repository.GetByIdAsync(id, ct);
        if (extra is null)
            throw new InvalidOperationException("El extra de campamento no fue encontrado");

        var sold = await repository.GetQuantitySoldAsync(id, ct);

        if (request.MaxQuantity.HasValue && sold > request.MaxQuantity.Value)
            throw new InvalidOperationException(
                $"No se puede reducir la cantidad máxima a {request.MaxQuantity} " +
                $"porque ya se han vendido {sold} unidades");

        if (sold > 0 && request.Price != extra.Price)
            throw new InvalidOperationException(
                "No se puede cambiar el precio de un extra que ya ha sido adquirido. " +
                "Considera crear un nuevo extra en su lugar");

        extra.Name = request.Name;
        extra.Description = request.Description;
        extra.Price = request.Price;
        extra.IsRequired = request.IsRequired;
        extra.IsActive = request.IsActive;
        extra.MaxQuantity = request.MaxQuantity;
        extra.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(extra, ct);
        return extra.ToResponse(sold);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var extra = await repository.GetByIdAsync(id, ct);
        if (extra is null) return false;

        var sold = await repository.GetQuantitySoldAsync(id, ct);
        if (sold > 0)
            throw new InvalidOperationException(
                $"No se puede eliminar el extra '{extra.Name}' porque ha sido " +
                $"seleccionado en {sold} inscripción/inscripciones. " +
                "Considera desactivarlo en su lugar");

        await repository.DeleteAsync(id, ct);
        return true;
    }

    public async Task<CampEditionExtraResponse> ActivateAsync(Guid id, CancellationToken ct = default)
    {
        var extra = await repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("El extra de campamento no fue encontrado");

        extra.IsActive = true;
        extra.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(extra, ct);

        var sold = await repository.GetQuantitySoldAsync(id, ct);
        return extra.ToResponse(sold);
    }

    public async Task<CampEditionExtraResponse> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var extra = await repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("El extra de campamento no fue encontrado");

        extra.IsActive = false;
        extra.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(extra, ct);

        var sold = await repository.GetQuantitySoldAsync(id, ct);
        return extra.ToResponse(sold);
    }

    public async Task<bool> IsAvailableAsync(
        Guid extraId,
        int requestedQuantity,
        CancellationToken ct = default)
    {
        var extra = await repository.GetByIdAsync(extraId, ct);
        if (extra is null || !extra.IsActive) return false;
        if (!extra.MaxQuantity.HasValue) return true; // unlimited

        var sold = await repository.GetQuantitySoldAsync(extraId, ct);
        return (extra.MaxQuantity.Value - sold) >= requestedQuantity;
    }
}

/// <summary>Extension method to map entity to response DTO.</summary>
internal static class CampEditionExtraExtensions
{
    public static CampEditionExtraResponse ToResponse(
        this CampEditionExtra extra,
        int currentQuantitySold)
        => new(
            extra.Id,
            extra.CampEditionId,
            extra.Name,
            extra.Description,
            extra.Price,
            extra.PricingType,
            extra.PricingPeriod,
            extra.IsRequired,
            extra.IsActive,
            extra.MaxQuantity,
            currentQuantitySold,
            extra.CreatedAt,
            extra.UpdatedAt
        );
}
