using Abuvi.API.Common.Exceptions;

namespace Abuvi.API.Features.Camps;

public class AccommodationAssignmentProposalsService(
    IAccommodationAssignmentProposalsRepository proposalsRepository,
    ICampEditionsRepository editionsRepository)
{
    public async Task<List<AccommodationAssignmentProposalSummaryResponse>> GetByEditionAsync(
        Guid campEditionId,
        CancellationToken ct = default)
    {
        var proposals = await proposalsRepository.GetByCampEditionAsync(campEditionId, ct);
        var totalRegistrations = await proposalsRepository.CountRegistrationsAsync(campEditionId, ct);

        var modifierIds = proposals
            .Where(p => p.LastModifiedByUserId.HasValue)
            .Select(p => p.LastModifiedByUserId!.Value)
            .Distinct();
        var userNames = await proposalsRepository.GetUserDisplayNamesAsync(modifierIds, ct);

        var result = new List<AccommodationAssignmentProposalSummaryResponse>(proposals.Count);
        foreach (var proposal in proposals)
        {
            var assignmentCount = await proposalsRepository.CountAssignmentsAsync(proposal.Id, ct);
            var lastModifiedName = proposal.LastModifiedByUserId.HasValue
                ? userNames.GetValueOrDefault(proposal.LastModifiedByUserId.Value)
                : null;
            result.Add(ToSummaryResponse(proposal, assignmentCount, totalRegistrations - assignmentCount, lastModifiedName));
        }

        return result;
    }

    public async Task<AccommodationAssignmentProposalSummaryResponse> CreateAsync(
        Guid campEditionId,
        CreateAccommodationAssignmentProposalRequest request,
        Guid createdByUserId,
        CancellationToken ct = default)
    {
        _ = await editionsRepository.GetByIdAsync(campEditionId, ct)
            ?? throw new NotFoundException("CampEdition", campEditionId);

        var proposal = new AccommodationAssignmentProposal
        {
            Id = Guid.NewGuid(),
            CampEditionId = campEditionId,
            Name = request.Name,
            Notes = request.Notes,
            IsActive = false,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await proposalsRepository.AddAsync(proposal, ct);

        if (request.CopyFromProposalId.HasValue)
        {
            var source = await proposalsRepository.GetByIdAsync(request.CopyFromProposalId.Value, ct);
            if (source is not null && source.CampEditionId == campEditionId)
                await proposalsRepository.CopyAssignmentsAsync(
                    request.CopyFromProposalId.Value, proposal.Id, createdByUserId, ct);
        }

        var assignmentCount = await proposalsRepository.CountAssignmentsAsync(proposal.Id, ct);
        var totalRegistrations = await proposalsRepository.CountRegistrationsAsync(campEditionId, ct);
        return ToSummaryResponse(proposal, assignmentCount, totalRegistrations - assignmentCount);
    }

    public async Task<AccommodationAssignmentProposalSummaryResponse> UpdateAsync(
        Guid proposalId,
        UpdateAccommodationAssignmentProposalRequest request,
        CancellationToken ct = default)
    {
        var proposal = await proposalsRepository.GetByIdAsync(proposalId, ct)
            ?? throw new NotFoundException("AccommodationAssignmentProposal", proposalId);

        proposal.Name = request.Name;
        proposal.Notes = request.Notes;
        proposal.UpdatedAt = DateTime.UtcNow;

        await proposalsRepository.UpdateAsync(proposal, ct);

        var assignmentCount = await proposalsRepository.CountAssignmentsAsync(proposalId, ct);
        var totalRegistrations = await proposalsRepository.CountRegistrationsAsync(proposal.CampEditionId, ct);
        return ToSummaryResponse(proposal, assignmentCount, totalRegistrations - assignmentCount);
    }

    public async Task DeleteAsync(Guid proposalId, CancellationToken ct = default)
    {
        var proposal = await proposalsRepository.GetByIdAsync(proposalId, ct)
            ?? throw new NotFoundException("AccommodationAssignmentProposal", proposalId);

        if (proposal.IsActive)
        {
            var allProposals = await proposalsRepository.GetByCampEditionAsync(proposal.CampEditionId, ct);
            if (allProposals.Count <= 1)
                throw new BusinessRuleException(
                    "No se puede eliminar la única propuesta activa. Crea otra propuesta primero.");
        }

        await proposalsRepository.DeleteAsync(proposalId, ct);
    }

    public async Task<AccommodationAssignmentProposalSummaryResponse> ActivateAsync(
        Guid proposalId,
        CancellationToken ct = default)
    {
        var proposal = await proposalsRepository.GetByIdAsync(proposalId, ct)
            ?? throw new NotFoundException("AccommodationAssignmentProposal", proposalId);

        await proposalsRepository.ActivateAsync(proposalId, proposal.CampEditionId, ct);

        proposal.IsActive = true;
        var assignmentCount = await proposalsRepository.CountAssignmentsAsync(proposalId, ct);
        var totalRegistrations = await proposalsRepository.CountRegistrationsAsync(proposal.CampEditionId, ct);
        return ToSummaryResponse(proposal, assignmentCount, totalRegistrations - assignmentCount);
    }

    private static AccommodationAssignmentProposalSummaryResponse ToSummaryResponse(
        AccommodationAssignmentProposal p,
        int assignmentCount,
        int unassignedCount,
        string? lastModifiedByUserName = null)
        => new(
            p.Id,
            p.CampEditionId,
            p.Name,
            p.Notes,
            p.IsActive,
            assignmentCount,
            unassignedCount,
            p.CreatedByUserId,
            p.CreatedAt,
            p.UpdatedAt,
            lastModifiedByUserName
        );
}
