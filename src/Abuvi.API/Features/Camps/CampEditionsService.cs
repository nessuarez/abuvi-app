namespace Abuvi.API.Features.Camps;

/// <summary>
/// Service for managing camp editions with proposal workflow
/// </summary>
public class CampEditionsService
{
    private readonly ICampEditionsRepository _repository;
    private readonly ICampsRepository _campsRepository;

    public CampEditionsService(
        ICampEditionsRepository repository,
        ICampsRepository campsRepository)
    {
        _repository = repository;
        _campsRepository = campsRepository;
    }

    /// <summary>
    /// Proposes a new camp edition
    /// </summary>
    public async Task<CampEditionResponse> ProposeAsync(
        ProposeCampEditionRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate dates
        if (request.EndDate <= request.StartDate)
        {
            throw new ArgumentException("End date must be after start date");
        }

        // Get camp to validate it exists and is active
        var camp = await _campsRepository.GetByIdAsync(request.CampId, cancellationToken);
        if (camp == null)
        {
            throw new InvalidOperationException("Camp not found");
        }

        if (!camp.IsActive)
        {
            throw new InvalidOperationException("Cannot propose edition for inactive camp");
        }

        // Use provided prices or inherit from camp
        var pricePerAdult = request.PricePerAdult ?? camp.PricePerAdult;
        var pricePerChild = request.PricePerChild ?? camp.PricePerChild;
        var pricePerBaby = request.PricePerBaby ?? camp.PricePerBaby;

        // Create edition with Proposed status
        var edition = new CampEdition
        {
            Id = Guid.NewGuid(),
            CampId = request.CampId,
            Year = request.Year,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            PricePerAdult = pricePerAdult,
            PricePerChild = pricePerChild,
            PricePerBaby = pricePerBaby,
            UseCustomAgeRanges = request.UseCustomAgeRanges,
            CustomBabyMaxAge = request.CustomBabyMaxAge,
            CustomChildMinAge = request.CustomChildMinAge,
            CustomChildMaxAge = request.CustomChildMaxAge,
            CustomAdultMinAge = request.CustomAdultMinAge,
            Status = CampEditionStatus.Proposed,
            MaxCapacity = request.MaxCapacity,
            Notes = request.Notes,
            IsArchived = false
        };

        var created = await _repository.CreateAsync(edition, cancellationToken);

        return MapToCampEditionResponse(created, camp.Name);
    }

    /// <summary>
    /// Gets all proposed editions for a given year
    /// </summary>
    public async Task<List<CampEditionResponse>> GetProposedAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        var editions = await _repository.GetByStatusAndYearAsync(
            CampEditionStatus.Proposed,
            year,
            cancellationToken);

        return editions.Select(e => MapToCampEditionResponse(e, e.Camp.Name)).ToList();
    }

    /// <summary>
    /// Promotes a proposed edition to draft status
    /// </summary>
    public async Task<CampEditionResponse> PromoteToDraftAsync(
        Guid editionId,
        CancellationToken cancellationToken = default)
    {
        var edition = await _repository.GetByIdAsync(editionId, cancellationToken);
        if (edition == null)
        {
            throw new InvalidOperationException("Camp edition not found");
        }

        if (edition.Status != CampEditionStatus.Proposed)
        {
            throw new InvalidOperationException("Only Proposed editions can be promoted to Draft");
        }

        edition.Status = CampEditionStatus.Draft;

        var updated = await _repository.UpdateAsync(edition, cancellationToken);

        return MapToCampEditionResponse(updated, edition.Camp.Name);
    }

    /// <summary>
    /// Rejects a proposed edition (soft delete)
    /// </summary>
    public async Task<bool> RejectProposalAsync(
        Guid editionId,
        CancellationToken cancellationToken = default)
    {
        var edition = await _repository.GetByIdAsync(editionId, cancellationToken);
        if (edition == null)
        {
            return false;
        }

        if (edition.Status != CampEditionStatus.Proposed)
        {
            throw new InvalidOperationException("Only Proposed editions can be rejected");
        }

        edition.IsArchived = true;

        await _repository.UpdateAsync(edition, cancellationToken);

        return true;
    }

    private static CampEditionResponse MapToCampEditionResponse(CampEdition edition, string campName)
    {
        return new CampEditionResponse(
            Id: edition.Id,
            CampId: edition.CampId,
            CampName: campName,
            Year: edition.Year,
            StartDate: edition.StartDate,
            EndDate: edition.EndDate,
            PricePerAdult: edition.PricePerAdult,
            PricePerChild: edition.PricePerChild,
            PricePerBaby: edition.PricePerBaby,
            UseCustomAgeRanges: edition.UseCustomAgeRanges,
            CustomBabyMaxAge: edition.CustomBabyMaxAge,
            CustomChildMinAge: edition.CustomChildMinAge,
            CustomChildMaxAge: edition.CustomChildMaxAge,
            CustomAdultMinAge: edition.CustomAdultMinAge,
            Status: edition.Status,
            MaxCapacity: edition.MaxCapacity,
            Notes: edition.Notes,
            IsArchived: edition.IsArchived,
            CreatedAt: edition.CreatedAt,
            UpdatedAt: edition.UpdatedAt
        );
    }
}
