namespace Abuvi.API.Features.Camps;

/// <summary>
/// Service for managing manually-uploaded camp photos (CRUD, reorder, set-primary)
/// </summary>
public class CampPhotosService
{
    private readonly ICampsRepository _repository;

    public CampPhotosService(ICampsRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Adds a manually-uploaded photo to a camp.
    /// If IsPrimary is true, clears the previous primary first.
    /// </summary>
    public async Task<CampPhotoResponse> AddPhotoAsync(
        Guid campId,
        AddCampPhotoRequest request,
        CancellationToken cancellationToken = default)
    {
        var camp = await _repository.GetByIdAsync(campId, cancellationToken);
        if (camp == null)
            throw new InvalidOperationException("Camp not found");

        if (request.IsPrimary)
            await _repository.ClearPrimaryPhotoAsync(campId, cancellationToken);

        var photo = new CampPhoto
        {
            Id = Guid.NewGuid(),
            CampId = campId,
            PhotoUrl = request.Url,
            Description = request.Description,
            DisplayOrder = request.DisplayOrder,
            IsPrimary = request.IsPrimary,
            IsOriginal = false,
            Width = 0,
            Height = 0,
            AttributionName = string.Empty
        };

        var created = await _repository.AddPhotoAsync(photo, cancellationToken);
        return MapToResponse(created);
    }

    /// <summary>
    /// Updates an existing camp photo.
    /// If IsPrimary is changed to true, clears the previous primary first.
    /// </summary>
    public async Task<CampPhotoResponse?> UpdatePhotoAsync(
        Guid campId,
        Guid photoId,
        UpdateCampPhotoRequest request,
        CancellationToken cancellationToken = default)
    {
        var photo = await _repository.GetPhotoByIdAsync(photoId, cancellationToken);
        if (photo == null || photo.CampId != campId) return null;

        if (request.IsPrimary && !photo.IsPrimary)
            await _repository.ClearPrimaryPhotoAsync(campId, cancellationToken);

        photo.PhotoUrl = request.Url;
        photo.Description = request.Description;
        photo.DisplayOrder = request.DisplayOrder;
        photo.IsPrimary = request.IsPrimary;

        var updated = await _repository.UpdatePhotoAsync(photo, cancellationToken);
        return MapToResponse(updated);
    }

    /// <summary>
    /// Deletes a camp photo. Returns false if not found or does not belong to camp.
    /// </summary>
    public async Task<bool> DeletePhotoAsync(
        Guid campId,
        Guid photoId,
        CancellationToken cancellationToken = default)
    {
        var photo = await _repository.GetPhotoByIdAsync(photoId, cancellationToken);
        if (photo == null || photo.CampId != campId) return false;

        return await _repository.DeletePhotoAsync(photoId, cancellationToken);
    }

    /// <summary>
    /// Bulk reorders camp photos. Returns false if camp not found.
    /// </summary>
    public async Task<bool> ReorderPhotosAsync(
        Guid campId,
        ReorderCampPhotosRequest request,
        CancellationToken cancellationToken = default)
    {
        var camp = await _repository.GetByIdAsync(campId, cancellationToken);
        if (camp == null) return false;

        var updates = request.Photos.Select(p => (p.Id, p.DisplayOrder));
        await _repository.UpdatePhotoOrdersAsync(updates, cancellationToken);

        return true;
    }

    /// <summary>
    /// Sets the primary photo for a camp, clearing any existing primary.
    /// Returns null if photo not found or does not belong to camp.
    /// </summary>
    public async Task<CampPhotoResponse?> SetPrimaryPhotoAsync(
        Guid campId,
        Guid photoId,
        CancellationToken cancellationToken = default)
    {
        var photo = await _repository.GetPhotoByIdAsync(photoId, cancellationToken);
        if (photo == null || photo.CampId != campId) return null;

        await _repository.ClearPrimaryPhotoAsync(campId, cancellationToken);

        // Re-fetch after clearing (EF tracking cleared)
        photo = await _repository.GetPhotoByIdAsync(photoId, cancellationToken);
        photo!.IsPrimary = true;

        var updated = await _repository.UpdatePhotoAsync(photo, cancellationToken);
        return MapToResponse(updated);
    }

    /// <summary>
    /// Lists all photos for a camp ordered by DisplayOrder.
    /// </summary>
    public async Task<List<CampPhotoResponse>> GetPhotosAsync(
        Guid campId,
        CancellationToken cancellationToken = default)
    {
        var photos = await _repository.GetPhotosForCampAsync(campId, cancellationToken);
        return photos.Select(MapToResponse).ToList();
    }

    private static CampPhotoResponse MapToResponse(CampPhoto photo) => new(
        Id: photo.Id,
        PhotoReference: photo.PhotoReference,
        PhotoUrl: photo.PhotoUrl,
        Width: photo.Width,
        Height: photo.Height,
        AttributionName: photo.AttributionName,
        AttributionUrl: photo.AttributionUrl,
        Description: photo.Description,
        IsPrimary: photo.IsPrimary,
        DisplayOrder: photo.DisplayOrder
    );
}
