namespace Abuvi.API.Features.Camps;

public interface IAccommodationAssignmentProposalsRepository
{
    Task<AccommodationAssignmentProposal?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<AccommodationAssignmentProposal>> GetByCampEditionAsync(Guid campEditionId, CancellationToken ct = default);
    Task AddAsync(AccommodationAssignmentProposal proposal, CancellationToken ct = default);
    Task UpdateAsync(AccommodationAssignmentProposal proposal, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task ActivateAsync(Guid proposalId, Guid campEditionId, CancellationToken ct = default);
    Task<int> CountAssignmentsAsync(Guid proposalId, CancellationToken ct = default);
    Task<int> CountRegistrationsAsync(Guid campEditionId, CancellationToken ct = default);
    Task CopyAssignmentsAsync(Guid sourceProposalId, Guid targetProposalId, Guid assignedByUserId, CancellationToken ct = default);
    Task<Dictionary<Guid, string>> GetUserDisplayNamesAsync(IEnumerable<Guid> userIds, CancellationToken ct = default);
}
