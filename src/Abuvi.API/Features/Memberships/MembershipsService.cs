using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Common.Exceptions;

namespace Abuvi.API.Features.Memberships;

public class MembershipsService(
    IMembershipsRepository repository,
    IFamilyUnitsRepository familyUnitsRepository)
{
    public async Task<MembershipResponse> CreateAsync(
        Guid familyMemberId,
        CreateMembershipRequest request,
        CancellationToken ct)
    {
        // Validate FamilyMember exists
        var familyMember = await familyUnitsRepository.GetFamilyMemberByIdAsync(familyMemberId, ct);
        if (familyMember is null)
            throw new NotFoundException(nameof(FamilyMember), familyMemberId);

        // Validate no active membership exists
        var existing = await repository.GetByFamilyMemberIdAsync(familyMemberId, ct);
        if (existing is not null)
            throw new BusinessRuleException("El miembro ya tiene una membresía activa");

        // Create membership
        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            FamilyMemberId = familyMemberId,
            StartDate = request.StartDate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(membership, ct);

        return membership.ToResponse();
    }

    public async Task<MembershipResponse> GetByFamilyMemberIdAsync(
        Guid familyMemberId,
        CancellationToken ct)
    {
        var membership = await repository.GetByFamilyMemberIdAsync(familyMemberId, ct);
        if (membership is null)
            throw new NotFoundException("Membership", familyMemberId);

        return membership.ToResponse();
    }

    public async Task DeactivateAsync(Guid familyMemberId, CancellationToken ct)
    {
        var membership = await repository.GetByFamilyMemberIdAsync(familyMemberId, ct);
        if (membership is null)
            throw new NotFoundException("Membership", familyMemberId);

        membership.IsActive = false;
        membership.EndDate = DateTime.UtcNow;
        membership.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(membership, ct);
    }
}

// Extension methods for mapping
public static class MembershipExtensions
{
    public static MembershipResponse ToResponse(this Membership membership)
        => new(
            membership.Id,
            membership.FamilyMemberId,
            membership.StartDate,
            membership.EndDate,
            membership.IsActive,
            membership.Fees.Select(f => f.ToResponse()).ToList(),
            membership.CreatedAt,
            membership.UpdatedAt
        );

    public static MembershipFeeResponse ToResponse(this MembershipFee fee)
        => new(
            fee.Id,
            fee.MembershipId,
            fee.Year,
            fee.Amount,
            fee.Status,
            fee.PaidDate,
            fee.PaymentReference,
            fee.CreatedAt
        );
}
