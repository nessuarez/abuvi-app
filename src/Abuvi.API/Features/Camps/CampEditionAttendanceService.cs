using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.MediaItems;
using Abuvi.API.Features.Users;

namespace Abuvi.API.Features.Camps;

/// <summary>
/// "Yo estuve en este campamento".
///
/// Attendance has two sources and they are treated differently: what a member declares
/// here, and what can be derived from their family's registrations. Derived attendance is
/// computed at read time and never written to the table — which is why it cannot be
/// withdrawn through the API.
/// </summary>
public class CampEditionAttendanceService(
    ICampEditionAttendanceRepository repository,
    ICampEditionsRepository editionsRepository,
    IFamilyUnitsRepository familyUnitsRepository,
    IUsersRepository usersRepository,
    IMediaItemsRepository mediaItemsRepository,
    ILogger<CampEditionAttendanceService> logger)
{
    public const string SourceDeclared = "Declared";
    public const string SourceRegistration = "Registration";
    public const string SourceNone = "None";

    /// <summary>
    /// Records attendance. Returns false when the caller may not declare for the given
    /// family member, so the endpoint can answer 403.
    ///
    /// Declaring twice is a no-op returning success — an idempotent 200, not a 409. A
    /// toggle button that errors when pressed twice is worse than one that shrugs.
    /// </summary>
    public async Task<bool> DeclareAsync(
        Guid editionId, Guid userId, Guid? familyMemberId, CancellationToken ct)
    {
        _ = await editionsRepository.GetByIdAsync(editionId, ct)
            ?? throw new NotFoundException("edición", editionId);

        if (familyMemberId is { } memberId && !await OwnsFamilyMemberAsync(userId, memberId, ct))
            return false;

        var existing = await repository.GetAsync(editionId, userId, familyMemberId, ct);
        if (existing is not null) return true;

        await repository.AddAsync(new CampEditionAttendance
        {
            Id = Guid.NewGuid(),
            CampEditionId = editionId,
            UserId = userId,
            FamilyMemberId = familyMemberId,
            CreatedAt = DateTime.UtcNow
        }, ct);

        logger.LogInformation(
            "Attendance declared for edition {EditionId} by user {UserId} (member {MemberId})",
            editionId, userId, familyMemberId);

        return true;
    }

    public async Task WithdrawAsync(
        Guid editionId, Guid userId, Guid? familyMemberId, CancellationToken ct)
    {
        var existing = await repository.GetAsync(editionId, userId, familyMemberId, ct);

        if (existing is null)
        {
            // Distinguish "never declared" from "derived from a registration": the second
            // is a real attendance record the member can see but cannot remove here.
            var derived = await repository.GetRegisteredEditionIdsForUserAsync(userId, ct);
            if (derived.Contains(editionId))
                throw new ValidationException(
                    "La asistencia derivada de una inscripción no se puede eliminar");

            throw new NotFoundException("asistencia", editionId);
        }

        await repository.DeleteAsync(existing, ct);
        logger.LogInformation(
            "Attendance withdrawn for edition {EditionId} by user {UserId}", editionId, userId);
    }

    public async Task<IReadOnlyList<AttendanceEntryResponse>> GetForEditionAsync(
        Guid editionId, CancellationToken ct)
    {
        _ = await editionsRepository.GetByIdAsync(editionId, ct)
            ?? throw new NotFoundException("edición", editionId);

        var declared = await repository.GetDeclaredForEditionAsync(editionId, ct);

        var entries = declared
            .Select(a => new AttendanceEntryResponse(
                a.CampEditionId,
                a.UserId,
                a.User is null ? "Unknown" : $"{a.User.FirstName} {a.User.LastName}",
                a.FamilyMemberId,
                a.FamilyMember is null
                    ? null
                    : $"{a.FamilyMember.FirstName} {a.FamilyMember.LastName}",
                SourceDeclared))
            .ToList();

        // Union with registration-derived attendance, skipping anyone already declared.
        var registrations = await repository.GetRegistrationsForEditionAsync(editionId, ct);
        var declaredUserIds = declared.Select(a => a.UserId).ToHashSet();

        foreach (var registration in registrations)
        {
            var registrant = await usersRepository.GetByIdAsync(registration.RegisteredByUserId, ct);
            if (registrant is null || declaredUserIds.Contains(registrant.Id)) continue;

            entries.Add(new AttendanceEntryResponse(
                editionId,
                registrant.Id,
                $"{registrant.FirstName} {registrant.LastName}",
                null,
                null,
                SourceRegistration));

            declaredUserIds.Add(registrant.Id);
        }

        return entries;
    }

    /// <summary>
    /// The caller's personal timeline across EVERY edition, attended or not, so the
    /// frontend can paint "tus campamentos" over the full map in one call.
    /// </summary>
    public async Task<CampTimelineResponse> GetTimelineAsync(Guid userId, CancellationToken ct)
    {
        var editions = await editionsRepository.GetAllAsync(cancellationToken: ct);
        var declared = (await repository.GetDeclaredEditionIdsForUserAsync(userId, ct)).ToHashSet();
        var derived = (await repository.GetRegisteredEditionIdsForUserAsync(userId, ct)).ToHashSet();
        var counts = await mediaItemsRepository.GetAlbumCountsAsync(ct);

        var mediaByEdition = counts
            .GroupBy(c => c.CampEditionId)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Count));

        var entries = editions
            .OrderByDescending(e => e.Year)
            .Select(e =>
            {
                var source = declared.Contains(e.Id) ? SourceDeclared
                    : derived.Contains(e.Id) ? SourceRegistration
                    : SourceNone;

                return new CampTimelineEntryResponse(
                    e.Id,
                    e.Year,
                    e.Camp?.Name ?? "Desconocido",
                    e.Camp?.Latitude,
                    e.Camp?.Longitude,
                    source != SourceNone,
                    source,
                    mediaByEdition.TryGetValue(e.Id, out var n) ? n : 0);
            })
            .ToList();

        return new CampTimelineResponse(entries.Count(e => e.Attended), entries);
    }

    /// <summary>Editions the user attended, from either source. Feeds album badges and dating hints.</summary>
    public async Task<IReadOnlyList<Guid>> GetAttendedEditionIdsAsync(Guid userId, CancellationToken ct)
    {
        var declared = await repository.GetDeclaredEditionIdsForUserAsync(userId, ct);
        var derived = await repository.GetRegisteredEditionIdsForUserAsync(userId, ct);
        return declared.Concat(derived).Distinct().ToList();
    }

    private async Task<bool> OwnsFamilyMemberAsync(Guid userId, Guid familyMemberId, CancellationToken ct)
    {
        var user = await usersRepository.GetByIdAsync(userId, ct);
        if (user?.FamilyUnitId is not { } familyUnitId) return false;

        var member = await familyUnitsRepository.GetFamilyMemberByIdAsync(familyMemberId, ct);
        return member is not null && member.FamilyUnitId == familyUnitId;
    }
}
