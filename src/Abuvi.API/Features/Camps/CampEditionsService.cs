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

        // Check for existing non-archived edition for same camp+year
        var exists = await _repository.ExistsAsync(request.CampId, request.Year, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException(
                $"Ya existe una edición para este campamento en el año {request.Year}");
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
            ProposalReason = request.ProposalReason,
            ProposalNotes  = request.ProposalNotes,
            IsArchived = false
        };

        edition.SetAccommodationCapacity(request.AccommodationCapacity);

        var created = await _repository.CreateAsync(edition, cancellationToken);

        // Auto-sync accommodation to camp template if provided
        if (request.AccommodationCapacity is not null)
        {
            camp.SetAccommodationCapacity(request.AccommodationCapacity);
            await _campsRepository.UpdateAsync(camp, cancellationToken);
        }

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

        // Sync accommodation to camp template when promoting
        if (!string.IsNullOrWhiteSpace(edition.AccommodationCapacityJson))
        {
            edition.Camp.SetAccommodationCapacity(edition.GetAccommodationCapacity());
            await _campsRepository.UpdateAsync(edition.Camp, cancellationToken);
        }

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

    /// <summary>
    /// Changes the status of a camp edition, enforcing valid transitions and date constraints.
    /// </summary>
    public async Task<CampEditionResponse> ChangeStatusAsync(
        Guid editionId,
        CampEditionStatus newStatus,
        bool force,
        CancellationToken cancellationToken = default)
    {
        var edition = await _repository.GetByIdAsync(editionId, cancellationToken);
        if (edition == null)
            throw new InvalidOperationException("La edición de campamento no fue encontrada");

        ValidateStatusTransition(edition.Status, newStatus);

        if (!force)
            ValidateDateConstraintsForTransition(edition, newStatus);

        edition.Status = newStatus;
        var updated = await _repository.UpdateAsync(edition, cancellationToken);
        return MapToCampEditionResponse(updated, updated.Camp.Name);
    }

    /// <summary>
    /// Updates a camp edition with status-based field restrictions:
    /// - Proposed/Draft: all fields can be updated
    /// - Open: only Notes and MaxCapacity
    /// - Closed/Completed: no updates allowed
    /// </summary>
    public async Task<CampEditionResponse> UpdateAsync(
        Guid editionId,
        UpdateCampEditionRequest request,
        CancellationToken cancellationToken = default)
    {
        var edition = await _repository.GetByIdAsync(editionId, cancellationToken);
        if (edition == null)
            throw new InvalidOperationException("La edición de campamento no fue encontrada");

        if (edition.Status is CampEditionStatus.Closed or CampEditionStatus.Completed)
            throw new InvalidOperationException(
                "No se puede modificar una edición cerrada o completada");

        if (edition.Status == CampEditionStatus.Open)
        {
            if (request.StartDate != edition.StartDate ||
                request.EndDate != edition.EndDate ||
                request.PricePerAdult != edition.PricePerAdult ||
                request.PricePerChild != edition.PricePerChild ||
                request.PricePerBaby != edition.PricePerBaby)
            {
                throw new InvalidOperationException(
                    "No se pueden modificar las fechas ni los precios de una edición abierta");
            }
        }

        edition.StartDate = request.StartDate;
        edition.EndDate = request.EndDate;
        edition.PricePerAdult = request.PricePerAdult;
        edition.PricePerChild = request.PricePerChild;
        edition.PricePerBaby = request.PricePerBaby;
        edition.UseCustomAgeRanges = request.UseCustomAgeRanges;
        edition.CustomBabyMaxAge = request.CustomBabyMaxAge;
        edition.CustomChildMinAge = request.CustomChildMinAge;
        edition.CustomChildMaxAge = request.CustomChildMaxAge;
        edition.CustomAdultMinAge = request.CustomAdultMinAge;
        edition.MaxCapacity = request.MaxCapacity;
        edition.Notes = request.Notes;

        var updated = await _repository.UpdateAsync(edition, cancellationToken);
        return MapToCampEditionResponse(updated, updated.Camp.Name);
    }

    /// <summary>
    /// Gets a camp edition by ID. Returns null if not found.
    /// </summary>
    public async Task<CampEditionResponse?> GetByIdAsync(
        Guid editionId,
        CancellationToken cancellationToken = default)
    {
        var edition = await _repository.GetByIdAsync(editionId, cancellationToken);
        if (edition == null)
            return null;

        return MapToCampEditionResponse(edition, edition.Camp.Name);
    }

    /// <summary>
    /// Gets all camp editions with optional filtering by year, status, and campId.
    /// </summary>
    public async Task<List<CampEditionResponse>> GetAllAsync(
        int? year,
        CampEditionStatus? status,
        Guid? campId,
        CancellationToken cancellationToken = default)
    {
        var editions = await _repository.GetAllAsync(year, status, campId, cancellationToken);
        return editions.Select(e => MapToCampEditionResponse(e, e.Camp.Name)).ToList();
    }

    /// <summary>
    /// Returns the best available camp edition using status-priority and year-fallback logic.
    /// Priority: current year Open > current year Closed > previous year Completed > previous year Closed.
    /// Returns null if no qualifying edition exists within the 1-year lookback window.
    /// </summary>
    public async Task<CurrentCampEditionResponse?> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        var currentYear = DateTime.UtcNow.Year;
        var edition = await _repository.GetCurrentAsync(currentYear, cancellationToken);

        if (edition == null)
            return null;

        var registrationCount = 0; // Placeholder until Registrations feature is built
        var availableSpots = edition.MaxCapacity.HasValue
            ? edition.MaxCapacity.Value - registrationCount
            : (int?)null;

        return new CurrentCampEditionResponse(
            Id: edition.Id,
            CampId: edition.CampId,
            CampName: edition.Camp.Name,
            CampLocation: edition.Camp.Location,
            CampFormattedAddress: edition.Camp.FormattedAddress,
            CampLatitude: edition.Camp.Latitude,
            CampLongitude: edition.Camp.Longitude,
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
            RegistrationCount: registrationCount,
            AvailableSpots: availableSpots,
            Notes: edition.Notes,
            CreatedAt: edition.CreatedAt,
            UpdatedAt: edition.UpdatedAt
        );
    }

    /// <summary>
    /// Gets the active (Open) edition for the given year. Defaults to the current year.
    /// Returns null if no Open edition exists.
    /// </summary>
    public async Task<ActiveCampEditionResponse?> GetActiveEditionAsync(
        int? year,
        CancellationToken cancellationToken = default)
    {
        var targetYear = year ?? DateTime.UtcNow.Year;

        var editions = await _repository.GetByStatusAndYearAsync(
            CampEditionStatus.Open,
            targetYear,
            cancellationToken);

        var edition = editions.FirstOrDefault();
        if (edition == null)
            return null;

        // RegistrationCount is always 0 until the Registrations feature is integrated.
        return new ActiveCampEditionResponse(
            Id: edition.Id,
            CampId: edition.CampId,
            CampName: edition.Camp.Name,
            CampLocation: edition.Camp.Location,
            CampFormattedAddress: edition.Camp.FormattedAddress,
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
            RegistrationCount: 0,
            Notes: edition.Notes,
            CreatedAt: edition.CreatedAt,
            UpdatedAt: edition.UpdatedAt
        );
    }

    private static void ValidateStatusTransition(CampEditionStatus current, CampEditionStatus next)
    {
        var validTransitions = new Dictionary<CampEditionStatus, CampEditionStatus[]>
        {
            [CampEditionStatus.Proposed]  = [CampEditionStatus.Draft],
            [CampEditionStatus.Draft]     = [CampEditionStatus.Open],
            [CampEditionStatus.Open]      = [CampEditionStatus.Closed, CampEditionStatus.Draft],
            [CampEditionStatus.Closed]    = [CampEditionStatus.Completed],
            [CampEditionStatus.Completed] = []
        };

        if (!validTransitions.TryGetValue(current, out var allowed) || !allowed.Contains(next))
            throw new InvalidOperationException(
                $"La transición de '{current}' a '{next}' no es válida");
    }

    private static void ValidateDateConstraintsForTransition(CampEdition edition, CampEditionStatus newStatus)
    {
        var today = DateTime.UtcNow.Date;

        if (newStatus == CampEditionStatus.Open && edition.StartDate.Date < today)
            throw new InvalidOperationException(
                "No se puede abrir el registro de una edición con fecha de inicio en el pasado");

        if (newStatus == CampEditionStatus.Completed && edition.EndDate.Date >= today)
            throw new InvalidOperationException(
                "No se puede marcar como completada una edición cuya fecha de fin no ha pasado");
    }

    private static CampEditionResponse MapToCampEditionResponse(CampEdition edition, string campName)
    {
        var accommodation = edition.GetAccommodationCapacity();
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
            AccommodationCapacity: accommodation,
            CalculatedTotalBedCapacity: accommodation?.CalculateTotalBedCapacity(),
            IsArchived: edition.IsArchived,
            CreatedAt: edition.CreatedAt,
            UpdatedAt: edition.UpdatedAt
        );
    }
}
