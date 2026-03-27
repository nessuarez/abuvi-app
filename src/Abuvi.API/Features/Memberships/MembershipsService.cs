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
            StartDate = new DateTime(request.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true,
            MemberNumber = await repository.GetNextMemberNumberAsync(ct),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assign family number if this is the first membership for the family
        var familyUnit = await familyUnitsRepository.GetFamilyUnitByIdAsync(familyMember.FamilyUnitId, ct);
        if (familyUnit is not null && familyUnit.FamilyNumber is null)
        {
            familyUnit.FamilyNumber = await familyUnitsRepository.GetNextFamilyNumberAsync(ct);
            await familyUnitsRepository.UpdateFamilyUnitAsync(familyUnit, ct);
        }

        await repository.AddAsync(membership, ct);

        // Auto-create fee for the start year (admin will mark as Paid separately)
        var fee = new MembershipFee
        {
            Id = Guid.NewGuid(),
            MembershipId = membership.Id,
            Year = request.Year,
            Amount = 0m,
            Status = FeeStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await repository.AddFeeAsync(fee, ct);

        // Return membership with the newly created fee included
        var created = new Membership
        {
            Id = membership.Id,
            FamilyMemberId = membership.FamilyMemberId,
            MemberNumber = membership.MemberNumber,
            StartDate = membership.StartDate,
            EndDate = membership.EndDate,
            IsActive = membership.IsActive,
            Fees = new List<MembershipFee> { fee },
            CreatedAt = membership.CreatedAt,
            UpdatedAt = membership.UpdatedAt
        };
        return created.ToResponse();
    }

    public async Task<BulkActivateMembershipResponse> BulkActivateAsync(
        Guid familyUnitId,
        BulkActivateMembershipRequest request,
        CancellationToken ct)
    {
        var familyUnit = await familyUnitsRepository.GetFamilyUnitByIdAsync(familyUnitId, ct);
        if (familyUnit is null)
            throw new NotFoundException(nameof(FamilyUnit), familyUnitId);

        var members = await familyUnitsRepository.GetFamilyMembersByFamilyUnitIdAsync(familyUnitId, ct);

        bool familyNumberAssigned = familyUnit.FamilyNumber is not null;

        var results = new List<BulkMembershipMemberResult>();
        int activated = 0, skipped = 0;

        var startDate = new DateTime(request.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        foreach (var member in members)
        {
            var memberName = $"{member.FirstName} {member.LastName}";

            var existing = await repository.GetByFamilyMemberIdAsync(member.Id, ct);
            if (existing is not null)
            {
                results.Add(new(member.Id, memberName, BulkMembershipResultStatus.Skipped, "Ya tiene membresía activa"));
                skipped++;
                continue;
            }

            try
            {
                var membership = new Membership
                {
                    Id = Guid.NewGuid(),
                    FamilyMemberId = member.Id,
                    StartDate = startDate,
                    IsActive = true,
                    MemberNumber = await repository.GetNextMemberNumberAsync(ct),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Assign family number on first successful activation
                if (!familyNumberAssigned)
                {
                    familyUnit.FamilyNumber = await familyUnitsRepository.GetNextFamilyNumberAsync(ct);
                    await familyUnitsRepository.UpdateFamilyUnitAsync(familyUnit, ct);
                    familyNumberAssigned = true;
                }

                await repository.AddAsync(membership, ct);

                // Auto-create fee for the activation year
                var memberFee = new MembershipFee
                {
                    Id = Guid.NewGuid(),
                    MembershipId = membership.Id,
                    Year = request.Year,
                    Amount = 0m,
                    Status = FeeStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await repository.AddFeeAsync(memberFee, ct);

                results.Add(new(member.Id, memberName, BulkMembershipResultStatus.Activated));
                activated++;
            }
            catch (Exception ex)
            {
                results.Add(new(member.Id, memberName, BulkMembershipResultStatus.Failed, ex.Message));
            }
        }

        return new BulkActivateMembershipResponse(activated, skipped, results);
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

    public async Task<IReadOnlyList<MembershipFeeResponse>> GetFeesAsync(
        Guid membershipId,
        CancellationToken ct)
    {
        var fees = await repository.GetFeesByMembershipAsync(membershipId, ct);
        return fees.Select(f => f.ToResponse()).ToList();
    }

    public async Task<MembershipFeeResponse> GetCurrentYearFeeAsync(
        Guid membershipId,
        CancellationToken ct)
    {
        var fee = await repository.GetCurrentYearFeeAsync(membershipId, ct);
        if (fee is null)
            throw new NotFoundException("MembershipFee", membershipId);

        return fee.ToResponse();
    }

    public async Task<MembershipResponse> UpdateMemberNumberAsync(
        Guid membershipId,
        UpdateMemberNumberRequest request,
        CancellationToken ct)
    {
        var membership = await repository.GetByIdAsync(membershipId, ct);
        if (membership is null)
            throw new NotFoundException(nameof(Membership), membershipId);

        // Check uniqueness
        var isTaken = await repository.IsMemberNumberTakenAsync(request.MemberNumber, membershipId, ct);
        if (isTaken)
            throw new BusinessRuleException($"El número de socio/a {request.MemberNumber} ya está en uso");

        membership.MemberNumber = request.MemberNumber;
        membership.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(membership, ct);

        return membership.ToResponse();
    }

    public async Task<MembershipFeeResponse> CreateFeeAsync(
        Guid membershipId,
        CreateMembershipFeeRequest request,
        CancellationToken ct)
    {
        var membership = await repository.GetByIdAsync(membershipId, ct);
        if (membership is null)
            throw new NotFoundException(nameof(Membership), membershipId);

        var existing = await repository.GetFeeByYearAsync(membershipId, request.Year, ct);
        if (existing is not null)
            throw new BusinessRuleException($"Ya existe una cuota para el año {request.Year} en esta membresía.");

        var fee = new MembershipFee
        {
            Id = Guid.NewGuid(),
            MembershipId = membershipId,
            Year = request.Year,
            Amount = request.Amount,
            Status = FeeStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddFeeAsync(fee, ct);

        return fee.ToResponse();
    }

    public async Task<MembershipResponse> ReactivateAsync(
        Guid familyMemberId,
        ReactivateMembershipRequest request,
        CancellationToken ct)
    {
        var membership = await repository.GetByFamilyMemberIdIgnoringActiveAsync(familyMemberId, ct);

        if (membership is null)
            throw new NotFoundException(nameof(Membership), familyMemberId);

        if (membership.IsActive)
            throw new BusinessRuleException("El miembro ya tiene una membresía activa.");

        membership.IsActive = true;
        membership.EndDate = null;
        membership.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(membership, ct);

        // Create fee for the requested year if not already present
        var existingFee = await repository.GetFeeByYearAsync(membership.Id, request.Year, ct);
        if (existingFee is null)
        {
            var fee = new MembershipFee
            {
                Id = Guid.NewGuid(),
                MembershipId = membership.Id,
                Year = request.Year,
                Amount = 0m,
                Status = FeeStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await repository.AddFeeAsync(fee, ct);
        }

        var updated = await repository.GetByFamilyMemberIdAsync(familyMemberId, ct);
        return updated!.ToResponse();
    }

    public async Task<MembershipFeeResponse> PayFeeAsync(
        Guid feeId,
        PayFeeRequest request,
        CancellationToken ct)
    {
        var fee = await repository.GetFeeByIdAsync(feeId, ct);
        if (fee is null)
            throw new NotFoundException(nameof(MembershipFee), feeId);

        if (fee.Status == FeeStatus.Paid)
            throw new BusinessRuleException("La cuota ya está pagada");

        fee.Status = FeeStatus.Paid;
        fee.PaidDate = request.PaidDate;
        fee.PaymentReference = request.PaymentReference;
        fee.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateFeeAsync(fee, ct);

        return fee.ToResponse();
    }
}

// Extension methods for mapping
public static class MembershipExtensions
{
    public static MembershipResponse ToResponse(this Membership membership)
        => new(
            membership.Id,
            membership.FamilyMemberId,
            membership.MemberNumber,
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
