namespace Abuvi.API.Features.FamilyUnits;

using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Services;
using Microsoft.Extensions.Logging;

/// <summary>
/// Business logic service for family units and members management
/// </summary>
public class FamilyUnitsService(
    IFamilyUnitsRepository repository,
    IEncryptionService encryptionService,
    ILogger<FamilyUnitsService> logger)
{
    #region Family Unit CRUD

    /// <summary>
    /// Creates a new family unit for the user and automatically creates the representative as the first family member
    /// </summary>
    public async Task<FamilyUnitResponse> CreateFamilyUnitAsync(
        Guid userId, CreateFamilyUnitRequest request, CancellationToken ct)
    {
        // Get user
        var user = await repository.GetUserByIdAsync(userId, ct)
            ?? throw new NotFoundException("Usuario", userId);

        // Check if user already has a family unit
        if (user.FamilyUnitId is not null)
        {
            logger.LogWarning("User {UserId} attempted to create second family unit", userId);
            throw new BusinessRuleException("Ya tienes una unidad familiar");
        }

        // Create family unit
        var familyUnit = new FamilyUnit
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            RepresentativeUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.CreateFamilyUnitAsync(familyUnit, ct);

        // Update user's familyUnitId
        await repository.UpdateUserFamilyUnitIdAsync(userId, familyUnit.Id, ct);

        // Automatically create representative as family member
        var representativeMember = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyUnitId = familyUnit.Id,
            UserId = userId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DateOfBirth = DateOnly.MinValue, // User should update this later
            Relationship = FamilyRelationship.Parent,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.CreateFamilyMemberAsync(representativeMember, ct);

        logger.LogInformation(
            "Family unit {FamilyUnitId} created by user {UserId} with representative member {MemberId}",
            familyUnit.Id, userId, representativeMember.Id);

        return familyUnit.ToResponse();
    }

    /// <summary>
    /// Gets a family unit by its ID
    /// </summary>
    public async Task<FamilyUnitResponse> GetFamilyUnitByIdAsync(Guid id, CancellationToken ct)
    {
        var familyUnit = await repository.GetFamilyUnitByIdAsync(id, ct)
            ?? throw new NotFoundException("Unidad Familiar", id);

        return familyUnit.ToResponse();
    }

    /// <summary>
    /// Gets the family unit for the current user (representative)
    /// </summary>
    public async Task<FamilyUnitResponse> GetCurrentUserFamilyUnitAsync(Guid userId, CancellationToken ct)
    {
        var familyUnit = await repository.GetFamilyUnitByRepresentativeIdAsync(userId, ct)
            ?? throw new NotFoundException("No se encontró unidad familiar para el usuario actual");

        return familyUnit.ToResponse();
    }

    /// <summary>
    /// Updates an existing family unit
    /// </summary>
    public async Task<FamilyUnitResponse> UpdateFamilyUnitAsync(
        Guid id, UpdateFamilyUnitRequest request, CancellationToken ct)
    {
        var familyUnit = await repository.GetFamilyUnitByIdAsync(id, ct)
            ?? throw new NotFoundException("Unidad Familiar", id);

        familyUnit.Name = request.Name;
        await repository.UpdateFamilyUnitAsync(familyUnit, ct);

        logger.LogInformation("Family unit {FamilyUnitId} updated", id);

        return familyUnit.ToResponse();
    }

    /// <summary>
    /// Deletes a family unit and all its members (cascade delete)
    /// </summary>
    public async Task DeleteFamilyUnitAsync(Guid id, CancellationToken ct)
    {
        var familyUnit = await repository.GetFamilyUnitByIdAsync(id, ct)
            ?? throw new NotFoundException("Unidad Familiar", id);

        // Delete family unit (cascade deletes members)
        await repository.DeleteFamilyUnitAsync(id, ct);

        // Clear user's familyUnitId
        await repository.UpdateUserFamilyUnitIdAsync(familyUnit.RepresentativeUserId, null, ct);

        logger.LogInformation("Family unit {FamilyUnitId} deleted", id);
    }

    #endregion

    #region Authorization Helpers

    /// <summary>
    /// Checks if the user is the representative of the family unit
    /// </summary>
    public async Task<bool> IsRepresentativeAsync(Guid familyUnitId, Guid userId, CancellationToken ct)
    {
        var familyUnit = await repository.GetFamilyUnitByIdAsync(familyUnitId, ct);
        return familyUnit?.RepresentativeUserId == userId;
    }

    #endregion

    #region Family Member CRUD

    /// <summary>
    /// Creates a new family member with encrypted sensitive data
    /// </summary>
    public async Task<FamilyMemberResponse> CreateFamilyMemberAsync(
        Guid familyUnitId, CreateFamilyMemberRequest request, CancellationToken ct)
    {
        // Verify family unit exists
        var familyUnit = await repository.GetFamilyUnitByIdAsync(familyUnitId, ct)
            ?? throw new NotFoundException("Unidad Familiar", familyUnitId);

        // Encrypt sensitive data if provided
        var encryptedMedicalNotes = !string.IsNullOrEmpty(request.MedicalNotes)
            ? encryptionService.Encrypt(request.MedicalNotes)
            : null;

        var encryptedAllergies = !string.IsNullOrEmpty(request.Allergies)
            ? encryptionService.Encrypt(request.Allergies)
            : null;

        // Create family member
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyUnitId = familyUnitId,
            UserId = null,
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            Relationship = request.Relationship,
            DocumentNumber = request.DocumentNumber?.ToUpperInvariant(),
            Email = request.Email,
            Phone = request.Phone,
            MedicalNotes = encryptedMedicalNotes,
            Allergies = encryptedAllergies,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.CreateFamilyMemberAsync(member, ct);

        logger.LogInformation(
            "Family member {MemberId} created in family unit {FamilyUnitId}",
            member.Id, familyUnitId);

        return member.ToResponse();
    }

    /// <summary>
    /// Gets all family members for a family unit
    /// </summary>
    public async Task<IReadOnlyList<FamilyMemberResponse>> GetFamilyMembersByFamilyUnitIdAsync(
        Guid familyUnitId, CancellationToken ct)
    {
        var members = await repository.GetFamilyMembersByFamilyUnitIdAsync(familyUnitId, ct);
        return members.Select(m => m.ToResponse()).ToList();
    }

    /// <summary>
    /// Gets a single family member by ID
    /// </summary>
    public async Task<FamilyMemberResponse> GetFamilyMemberByIdAsync(Guid id, CancellationToken ct)
    {
        var member = await repository.GetFamilyMemberByIdAsync(id, ct)
            ?? throw new NotFoundException("Miembro Familiar", id);

        return member.ToResponse();
    }

    /// <summary>
    /// Updates an existing family member with encrypted sensitive data
    /// </summary>
    public async Task<FamilyMemberResponse> UpdateFamilyMemberAsync(
        Guid id, UpdateFamilyMemberRequest request, CancellationToken ct)
    {
        var member = await repository.GetFamilyMemberByIdAsync(id, ct)
            ?? throw new NotFoundException("Miembro Familiar", id);

        // Update basic fields
        member.FirstName = request.FirstName;
        member.LastName = request.LastName;
        member.DateOfBirth = request.DateOfBirth;
        member.Relationship = request.Relationship;
        member.DocumentNumber = request.DocumentNumber?.ToUpperInvariant();
        member.Email = request.Email;
        member.Phone = request.Phone;

        // Encrypt sensitive data if provided
        member.MedicalNotes = !string.IsNullOrEmpty(request.MedicalNotes)
            ? encryptionService.Encrypt(request.MedicalNotes)
            : null;

        member.Allergies = !string.IsNullOrEmpty(request.Allergies)
            ? encryptionService.Encrypt(request.Allergies)
            : null;

        await repository.UpdateFamilyMemberAsync(member, ct);

        logger.LogInformation("Family member {MemberId} updated", id);

        return member.ToResponse();
    }

    /// <summary>
    /// Deletes a family member (cannot delete representative's own record)
    /// </summary>
    public async Task DeleteFamilyMemberAsync(Guid id, CancellationToken ct)
    {
        var member = await repository.GetFamilyMemberByIdAsync(id, ct)
            ?? throw new NotFoundException("Miembro Familiar", id);

        // Check if this is the representative's own member record
        if (member.UserId.HasValue)
        {
            var familyUnit = await repository.GetFamilyUnitByIdAsync(member.FamilyUnitId, ct);
            if (familyUnit != null && familyUnit.RepresentativeUserId == member.UserId.Value)
            {
                throw new BusinessRuleException("No puedes eliminar tu propio perfil mientras seas representante");
            }
        }

        await repository.DeleteFamilyMemberAsync(id, ct);

        logger.LogInformation("Family member {MemberId} deleted", id);
    }

    #endregion

    #region Admin Operations

    /// <summary>
    /// Returns a paginated list of all family units for admin/board use.
    /// Supports search by family name or representative name, and sorting.
    /// </summary>
    public async Task<PagedFamilyUnitsResponse> GetAllFamilyUnitsAsync(
        int page,
        int pageSize,
        string? search,
        string? sortBy,
        string? sortOrder,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, totalCount) = await repository.GetAllPagedAsync(
            page, pageSize, search, sortBy, sortOrder, ct);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

        return new PagedFamilyUnitsResponse(
            Items: items.Select(p => new FamilyUnitListItemResponse(
                p.Id, p.Name, p.RepresentativeUserId,
                p.RepresentativeName, p.MembersCount, p.CreatedAt, p.UpdatedAt
            )).ToList(),
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages
        );
    }

    #endregion
}
