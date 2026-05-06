namespace Abuvi.API.Features.Camps;

public interface IAccommodationAssignmentsRepository
{
    Task<ProposalAssignmentStateResponse> GetAssignmentStateAsync(
        Guid campEditionId, Guid proposalId, CancellationToken ct = default);

    Task AssignAsync(
        Guid proposalId, Guid registrationId, Guid accommodationId,
        Guid assignedByUserId, CancellationToken ct = default);

    Task UnassignAsync(Guid proposalId, Guid registrationId, Guid modifiedByUserId, CancellationToken ct = default);

    Task BulkReplaceAsync(
        Guid proposalId, Guid campEditionId,
        IReadOnlyList<AssignmentEntry> assignments,
        Guid assignedByUserId, CancellationToken ct = default);

    Task<bool> ProposalBelongsToEditionAsync(
        Guid proposalId, Guid campEditionId, CancellationToken ct = default);
}
