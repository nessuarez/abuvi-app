using Abuvi.API.Common.Exceptions;

namespace Abuvi.API.Features.Camps;

public class AccommodationAssignmentsService(
    IAccommodationAssignmentsRepository assignmentsRepository)
{
    public async Task<ProposalAssignmentStateResponse> GetAssignmentStateAsync(
        Guid campEditionId,
        Guid proposalId,
        CancellationToken ct = default)
    {
        await EnsureProposalBelongsToEditionAsync(proposalId, campEditionId, ct);
        return await assignmentsRepository.GetAssignmentStateAsync(campEditionId, proposalId, ct);
    }

    public async Task<ProposalAssignmentStateResponse> AssignAsync(
        Guid campEditionId,
        Guid proposalId,
        Guid registrationId,
        SingleAssignRequest request,
        Guid assignedByUserId,
        CancellationToken ct = default)
    {
        await EnsureProposalBelongsToEditionAsync(proposalId, campEditionId, ct);
        await assignmentsRepository.AssignAsync(proposalId, registrationId, request.AccommodationId, assignedByUserId, ct);
        return await assignmentsRepository.GetAssignmentStateAsync(campEditionId, proposalId, ct);
    }

    public async Task UnassignAsync(
        Guid campEditionId,
        Guid proposalId,
        Guid registrationId,
        CancellationToken ct = default)
    {
        await EnsureProposalBelongsToEditionAsync(proposalId, campEditionId, ct);
        await assignmentsRepository.UnassignAsync(proposalId, registrationId, ct);
    }

    public async Task<ProposalAssignmentStateResponse> BulkReplaceAsync(
        Guid campEditionId,
        Guid proposalId,
        BulkAssignRequest request,
        Guid assignedByUserId,
        CancellationToken ct = default)
    {
        await EnsureProposalBelongsToEditionAsync(proposalId, campEditionId, ct);
        await assignmentsRepository.BulkReplaceAsync(proposalId, campEditionId, request.Assignments, assignedByUserId, ct);
        return await assignmentsRepository.GetAssignmentStateAsync(campEditionId, proposalId, ct);
    }

    public async Task<ProposalAssignmentStateResponse> AutoAssignAsync(
        Guid campEditionId,
        Guid proposalId,
        AutoAssignRequest request,
        Guid assignedByUserId,
        CancellationToken ct = default)
    {
        await EnsureProposalBelongsToEditionAsync(proposalId, campEditionId, ct);
        var state = await assignmentsRepository.GetAssignmentStateAsync(campEditionId, proposalId, ct);
        var computed = AutoAssignService.Compute(state, request.OverwriteExisting);
        await assignmentsRepository.BulkReplaceAsync(proposalId, campEditionId, computed, assignedByUserId, ct);
        return await assignmentsRepository.GetAssignmentStateAsync(campEditionId, proposalId, ct);
    }

    private async Task EnsureProposalBelongsToEditionAsync(
        Guid proposalId,
        Guid campEditionId,
        CancellationToken ct)
    {
        var belongs = await assignmentsRepository.ProposalBelongsToEditionAsync(proposalId, campEditionId, ct);
        if (!belongs)
            throw new NotFoundException("AccommodationAssignmentProposal", proposalId);
    }
}
