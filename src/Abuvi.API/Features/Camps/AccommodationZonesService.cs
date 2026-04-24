using Abuvi.API.Common.Exceptions;

namespace Abuvi.API.Features.Camps;

public class AccommodationZonesService(
    IAccommodationZonesRepository zonesRepository,
    ICampEditionsRepository editionsRepository)
{
    public async Task<List<AccommodationZoneResponse>> GetByEditionAsync(
        Guid campEditionId,
        CancellationToken ct = default)
    {
        var zones = await zonesRepository.GetByCampEditionAsync(campEditionId, ct);
        return zones.Select(ToResponse).ToList();
    }

    public async Task<AccommodationZoneResponse> CreateAsync(
        Guid campEditionId,
        CreateAccommodationZoneRequest request,
        CancellationToken ct = default)
    {
        var edition = await editionsRepository.GetByIdAsync(campEditionId, ct)
            ?? throw new NotFoundException("CampEdition", campEditionId);

        var zone = new AccommodationZone
        {
            Id = Guid.NewGuid(),
            CampEditionId = campEditionId,
            AccommodationType = request.AccommodationType,
            Name = request.Name,
            MaxCapacity = request.MaxCapacity,
            DistributionNotes = request.DistributionNotes,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await zonesRepository.AddAsync(zone, ct);
        return ToResponse(zone);
    }

    public async Task<AccommodationZoneResponse> UpdateAsync(
        Guid zoneId,
        UpdateAccommodationZoneRequest request,
        CancellationToken ct = default)
    {
        var zone = await zonesRepository.GetByIdAsync(zoneId, ct)
            ?? throw new NotFoundException("AccommodationZone", zoneId);

        zone.Name = request.Name;
        zone.MaxCapacity = request.MaxCapacity;
        zone.DistributionNotes = request.DistributionNotes;
        zone.SortOrder = request.SortOrder;
        zone.UpdatedAt = DateTime.UtcNow;

        await zonesRepository.UpdateAsync(zone, ct);
        return ToResponse(zone);
    }

    public async Task DeleteAsync(Guid zoneId, CancellationToken ct = default)
    {
        var zone = await zonesRepository.GetByIdAsync(zoneId, ct)
            ?? throw new NotFoundException("AccommodationZone", zoneId);

        if (await zonesRepository.HasActiveAssignmentsAsync(zoneId, ct))
            throw new BusinessRuleException(
                "No se puede eliminar la zona porque tiene familias asignadas en alguna propuesta. " +
                "Elimina primero las asignaciones o desactiva la zona.");

        await zonesRepository.DeleteAsync(zoneId, ct);
    }

    public async Task<AccommodationZoneResponse> AttachAccommodationsAsync(
        Guid zoneId,
        AttachAccommodationsToZoneRequest request,
        CancellationToken ct = default)
    {
        var zone = await zonesRepository.GetByIdAsync(zoneId, ct)
            ?? throw new NotFoundException("AccommodationZone", zoneId);

        await zonesRepository.AttachAccommodationsAsync(zoneId, request.AccommodationIds, ct);

        var updated = await zonesRepository.GetByIdAsync(zoneId, ct);
        return ToResponse(updated!);
    }

    private static AccommodationZoneResponse ToResponse(AccommodationZone z) =>
        new(
            z.Id,
            z.CampEditionId,
            z.AccommodationType,
            z.Name,
            z.MaxCapacity,
            z.DistributionNotes,
            z.SortOrder,
            z.IsActive,
            z.Accommodations.Select(a => a.Id).ToList(),
            z.CreatedAt,
            z.UpdatedAt
        );
}
