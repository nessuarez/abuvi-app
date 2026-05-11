using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.MediaItems;

namespace Abuvi.API.Features.Camps;

public class AccommodationTypeMediaService(IAccommodationTypeMediaRepository repository)
{
    private const int MaxMediaItemsPerType = 10;

    public async Task<IReadOnlyList<AccommodationTypeMediaResponse>> GetAllAsync(CancellationToken ct)
    {
        var items = await repository.GetAllAsync(ct);
        return items.Select(ToResponse).ToList();
    }

    public async Task<IReadOnlyList<AccommodationTypeMediaResponse>> GetByTypeAsync(
        AccommodationType type,
        CancellationToken ct)
    {
        var items = await repository.GetByTypeAsync(type, ct);
        return items.Select(ToResponse).ToList();
    }

    public async Task<AccommodationTypeMediaResponse> AddAsync(
        Guid userId,
        AccommodationType type,
        AddAccommodationMediaRequest request,
        CancellationToken ct)
    {
        var count = await repository.CountByTypeAsync(type, ct);
        if (count >= MaxMediaItemsPerType)
            throw new BusinessRuleException(
                $"No se pueden añadir más de {MaxMediaItemsPerType} elementos multimedia por tipo de alojamiento");

        var isFirstItem = count == 0;
        var item = new AccommodationTypeMedia
        {
            Id = Guid.NewGuid(),
            AccommodationType = type,
            FileUrl = request.FileUrl,
            ThumbnailUrl = request.ThumbnailUrl,
            Description = request.Description,
            DisplayOrder = request.DisplayOrder,
            IsPrimary = isFirstItem,
            UploadedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(item, ct);
        return ToResponse(item);
    }

    public async Task<AccommodationTypeMediaResponse> SetPrimaryAsync(Guid mediaId, CancellationToken ct)
    {
        var item = await repository.GetByIdAsync(mediaId, ct)
            ?? throw new NotFoundException(nameof(AccommodationTypeMedia), mediaId);

        await repository.ClearPrimaryForTypeAsync(item.AccommodationType, ct);

        item.IsPrimary = true;
        item.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(item, ct);

        return ToResponse(item);
    }

    public async Task DeleteAsync(Guid mediaId, CancellationToken ct)
    {
        var item = await repository.GetByIdAsync(mediaId, ct)
            ?? throw new NotFoundException(nameof(AccommodationTypeMedia), mediaId);

        await repository.DeleteAsync(item, ct);
    }

    private static AccommodationTypeMediaResponse ToResponse(AccommodationTypeMedia m) =>
        new(m.Id, m.AccommodationType.ToString(), m.FileUrl, m.ThumbnailUrl,
            m.Description, m.DisplayOrder, m.IsPrimary, m.CreatedAt);
}
