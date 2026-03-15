namespace Abuvi.API.Features.FamilyUnits;

using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Services;
using Abuvi.API.Features.BlobStorage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Business logic service for family units and members management
/// </summary>
public class FamilyUnitsService(
    IFamilyUnitsRepository repository,
    IEncryptionService encryptionService,
    IBlobStorageService blobStorageService,
    IOptions<BlobStorageOptions> blobOptions,
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
            Email = user.Email,
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
              ?? await repository.GetFamilyUnitByMemberUserIdAsync(userId, ct)
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

    /// <summary>
    /// Checks if the user is a linked family member (not necessarily representative) of the family unit
    /// </summary>
    public async Task<bool> IsFamilyMemberOfUnitAsync(Guid familyUnitId, Guid userId, CancellationToken ct)
    {
        var familyUnit = await repository.GetFamilyUnitByMemberUserIdAsync(userId, ct);
        return familyUnit?.Id == familyUnitId;
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

        // Check if email belongs to an existing user and auto-link
        Guid? userId = null;
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var existingUser = await repository.GetUserByEmailAsync(request.Email, ct);
            if (existingUser != null)
            {
                userId = existingUser.Id;
                logger.LogInformation(
                    "Family member auto-linked to user {UserId} by email {Email}",
                    userId, request.Email);
            }
        }

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
            UserId = userId,
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

        // Update user linking based on email
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var existingUser = await repository.GetUserByEmailAsync(request.Email, ct);
            if (existingUser != null && member.UserId != existingUser.Id)
            {
                member.UserId = existingUser.Id;
                logger.LogInformation(
                    "Family member {MemberId} auto-linked to user {UserId} by email {Email}",
                    id, existingUser.Id, request.Email);
            }
            else if (existingUser == null && member.UserId.HasValue)
            {
                logger.LogInformation(
                    "Family member {MemberId} unlinked from user {UserId} - email no longer matches",
                    id, member.UserId.Value);
                member.UserId = null;
            }
        }
        else if (member.UserId.HasValue)
        {
            logger.LogInformation(
                "Family member {MemberId} unlinked from user {UserId} - email removed",
                id, member.UserId.Value);
            member.UserId = null;
        }

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
    /// Deletes a family member (cannot delete representative's own record).
    /// When isAdminOrBoard is true, also checks for active registrations.
    /// </summary>
    public async Task DeleteFamilyMemberAsync(Guid id, bool isAdminOrBoard, CancellationToken ct)
    {
        var member = await repository.GetFamilyMemberByIdAsync(id, ct)
            ?? throw new NotFoundException("Miembro Familiar", id);

        // Check if this is the representative's member record
        var familyUnit = await repository.GetFamilyUnitByIdAsync(member.FamilyUnitId, ct);
        if (familyUnit != null && member.UserId.HasValue
            && familyUnit.RepresentativeUserId == member.UserId.Value)
        {
            throw new BusinessRuleException(
                "No se puede eliminar al representante de la unidad familiar.");
        }

        // Admin/Board: check for active registrations before deleting
        if (isAdminOrBoard)
        {
            var hasActiveRegs = await repository.MemberHasActiveRegistrationsAsync(id, ct);
            if (hasActiveRegs)
                throw new BusinessRuleException(
                    "No se puede eliminar un miembro con inscripciones activas (Pendiente/Confirmada).");
        }

        await repository.DeleteFamilyMemberAsync(id, ct);

        logger.LogInformation(
            "Deleted family member {MemberId} ({FirstName} {LastName}) from family unit {FamilyUnitId}",
            id, member.FirstName, member.LastName, member.FamilyUnitId);
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
        string? membershipStatus,
        bool? isActive,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, totalCount) = await repository.GetAllPagedAsync(
            page, pageSize, search, sortBy, sortOrder, membershipStatus, isActive, ct);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

        return new PagedFamilyUnitsResponse(
            Items: items.Select(p => new FamilyUnitListItemResponse(
                p.Id, p.Name, p.RepresentativeUserId,
                p.RepresentativeName, p.FamilyNumber, p.IsActive, p.MembersCount, p.CreatedAt, p.UpdatedAt
            )).ToList(),
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages
        );
    }

    /// <summary>
    /// Admin hard-delete a family unit. Only allowed if no registrations exist.
    /// </summary>
    public async Task AdminDeleteFamilyUnitAsync(Guid familyUnitId, CancellationToken ct)
    {
        var familyUnit = await repository.GetFamilyUnitByIdAsync(familyUnitId, ct)
            ?? throw new NotFoundException("Unidad Familiar", familyUnitId);

        var hasRegistrations = await repository.HasRegistrationsAsync(familyUnitId, ct);
        if (hasRegistrations)
            throw new BusinessRuleException(
                "No se puede eliminar una unidad familiar con inscripciones. Desactívela en su lugar.");

        // Clear FamilyUnitId for all linked users
        await repository.ClearAllUserFamilyUnitLinksAsync(familyUnitId, ct);

        // Hard delete (cascade deletes members via EF config)
        await repository.DeleteFamilyUnitAsync(familyUnitId, ct);

        logger.LogInformation(
            "Admin deleted family unit {FamilyUnitId} ({FamilyName})",
            familyUnitId, familyUnit.Name);
    }

    /// <summary>
    /// Toggle family unit active status (Admin/Board only)
    /// </summary>
    public async Task<FamilyUnitResponse> UpdateFamilyUnitStatusAsync(
        Guid familyUnitId, UpdateFamilyUnitStatusRequest request, CancellationToken ct)
    {
        var familyUnit = await repository.GetFamilyUnitByIdAsync(familyUnitId, ct)
            ?? throw new NotFoundException("Unidad Familiar", familyUnitId);

        await repository.UpdateFamilyUnitStatusAsync(familyUnitId, request.IsActive, ct);

        logger.LogInformation(
            "Family unit {FamilyUnitId} ({FamilyName}) status changed to IsActive={IsActive}",
            familyUnitId, familyUnit.Name, request.IsActive);

        // Reload to return updated state
        var updated = await repository.GetFamilyUnitByIdAsync(familyUnitId, ct);
        return updated!.ToResponse();
    }

    public async Task<FamilyUnitResponse> UpdateFamilyNumberAsync(
        Guid familyUnitId,
        UpdateFamilyNumberRequest request,
        CancellationToken ct)
    {
        var familyUnit = await repository.GetFamilyUnitByIdAsync(familyUnitId, ct);
        if (familyUnit is null)
            throw new NotFoundException(nameof(FamilyUnit), familyUnitId);

        // Check uniqueness
        var isTaken = await repository.IsFamilyNumberTakenAsync(request.FamilyNumber, familyUnitId, ct);
        if (isTaken)
            throw new BusinessRuleException($"El número de familia {request.FamilyNumber} ya está en uso");

        familyUnit.FamilyNumber = request.FamilyNumber;
        await repository.UpdateFamilyUnitAsync(familyUnit, ct);

        return familyUnit.ToResponse();
    }

    #endregion

    #region Profile Photos

    /// <summary>
    /// Uploads a profile photo for a family member
    /// </summary>
    public async Task<FamilyMemberResponse> UploadFamilyMemberProfilePhotoAsync(
        Guid familyUnitId, Guid memberId, Guid userId, bool isAdmin,
        IFormFile file, CancellationToken ct)
    {
        var familyUnit = await repository.GetFamilyUnitByIdAsync(familyUnitId, ct)
            ?? throw new NotFoundException("Unidad Familiar", familyUnitId);

        if (!isAdmin && familyUnit.RepresentativeUserId != userId)
            throw new BusinessRuleException("No tienes permiso para gestionar fotos de esta unidad familiar");

        var member = await repository.GetFamilyMemberByIdAsync(memberId, ct)
            ?? throw new NotFoundException("Miembro Familiar", memberId);

        if (member.FamilyUnitId != familyUnitId)
            throw new BusinessRuleException("El miembro no pertenece a esta unidad familiar");

        ValidateImageFile(file);

        // Delete old photo if exists
        if (member.ProfilePhotoUrl is not null)
            await DeleteBlobByUrl(member.ProfilePhotoUrl, ct);

        await using var stream = file.OpenReadStream();
        var result = await blobStorageService.UploadAsync(
            stream, file.FileName, file.ContentType,
            "profile-photos", memberId, true, ct);

        member.ProfilePhotoUrl = result.ThumbnailUrl ?? result.FileUrl;
        member.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateFamilyMemberAsync(member, ct);

        logger.LogInformation(
            "Profile photo uploaded for family member {MemberId} in unit {FamilyUnitId}",
            memberId, familyUnitId);

        return member.ToResponse();
    }

    /// <summary>
    /// Removes the profile photo of a family member
    /// </summary>
    public async Task RemoveFamilyMemberProfilePhotoAsync(
        Guid familyUnitId, Guid memberId, Guid userId, bool isAdmin,
        CancellationToken ct)
    {
        var familyUnit = await repository.GetFamilyUnitByIdAsync(familyUnitId, ct)
            ?? throw new NotFoundException("Unidad Familiar", familyUnitId);

        if (!isAdmin && familyUnit.RepresentativeUserId != userId)
            throw new BusinessRuleException("No tienes permiso para gestionar fotos de esta unidad familiar");

        var member = await repository.GetFamilyMemberByIdAsync(memberId, ct)
            ?? throw new NotFoundException("Miembro Familiar", memberId);

        if (member.FamilyUnitId != familyUnitId)
            throw new BusinessRuleException("El miembro no pertenece a esta unidad familiar");

        if (member.ProfilePhotoUrl is null)
            return;

        await DeleteBlobByUrl(member.ProfilePhotoUrl, ct);

        member.ProfilePhotoUrl = null;
        member.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateFamilyMemberAsync(member, ct);

        logger.LogInformation(
            "Profile photo removed for family member {MemberId} in unit {FamilyUnitId}",
            memberId, familyUnitId);
    }

    /// <summary>
    /// Uploads a profile photo for a family unit
    /// </summary>
    public async Task<FamilyUnitResponse> UploadFamilyUnitProfilePhotoAsync(
        Guid familyUnitId, Guid userId, bool isAdmin,
        IFormFile file, CancellationToken ct)
    {
        var familyUnit = await repository.GetFamilyUnitByIdAsync(familyUnitId, ct)
            ?? throw new NotFoundException("Unidad Familiar", familyUnitId);

        if (!isAdmin && familyUnit.RepresentativeUserId != userId)
            throw new BusinessRuleException("No tienes permiso para gestionar fotos de esta unidad familiar");

        ValidateImageFile(file);

        // Delete old photo if exists
        if (familyUnit.ProfilePhotoUrl is not null)
            await DeleteBlobByUrl(familyUnit.ProfilePhotoUrl, ct);

        await using var stream = file.OpenReadStream();
        var result = await blobStorageService.UploadAsync(
            stream, file.FileName, file.ContentType,
            "profile-photos", familyUnitId, true, ct);

        familyUnit.ProfilePhotoUrl = result.ThumbnailUrl ?? result.FileUrl;
        familyUnit.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateFamilyUnitAsync(familyUnit, ct);

        logger.LogInformation(
            "Profile photo uploaded for family unit {FamilyUnitId}",
            familyUnitId);

        return familyUnit.ToResponse();
    }

    /// <summary>
    /// Removes the profile photo of a family unit
    /// </summary>
    public async Task RemoveFamilyUnitProfilePhotoAsync(
        Guid familyUnitId, Guid userId, bool isAdmin,
        CancellationToken ct)
    {
        var familyUnit = await repository.GetFamilyUnitByIdAsync(familyUnitId, ct)
            ?? throw new NotFoundException("Unidad Familiar", familyUnitId);

        if (!isAdmin && familyUnit.RepresentativeUserId != userId)
            throw new BusinessRuleException("No tienes permiso para gestionar fotos de esta unidad familiar");

        if (familyUnit.ProfilePhotoUrl is null)
            return;

        await DeleteBlobByUrl(familyUnit.ProfilePhotoUrl, ct);

        familyUnit.ProfilePhotoUrl = null;
        familyUnit.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateFamilyUnitAsync(familyUnit, ct);

        logger.LogInformation(
            "Profile photo removed for family unit {FamilyUnitId}",
            familyUnitId);
    }

    private void ValidateImageFile(IFormFile file)
    {
        var cfg = blobOptions.Value;

        if (file.Length > cfg.MaxFileSizeBytes)
            throw new BusinessRuleException(
                $"El archivo no puede superar {cfg.MaxFileSizeBytes / 1_048_576} MB");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!cfg.AllowedImageExtensions.Contains(ext))
            throw new BusinessRuleException(
                "El tipo de archivo no está permitido. Extensiones permitidas: " +
                string.Join(", ", cfg.AllowedImageExtensions));
    }

    private async Task DeleteBlobByUrl(string fileUrl, CancellationToken ct)
    {
        var key = new Uri(fileUrl).AbsolutePath.TrimStart('/');
        await blobStorageService.DeleteManyAsync([key], ct);
    }

    #endregion
}
