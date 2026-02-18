using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Common.Exceptions;

namespace Abuvi.API.Features.Guests;

public class GuestsService(
    IGuestsRepository repository,
    IFamilyUnitsRepository familyUnitsRepository)
{
    public async Task<GuestResponse> CreateAsync(
        Guid familyUnitId,
        CreateGuestRequest request,
        CancellationToken ct)
    {
        var familyUnit = await familyUnitsRepository.GetFamilyUnitByIdAsync(familyUnitId, ct);
        if (familyUnit is null)
            throw new NotFoundException(nameof(FamilyUnit), familyUnitId);

        var guest = new Guest
        {
            Id = Guid.NewGuid(),
            FamilyUnitId = familyUnitId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            DocumentNumber = request.DocumentNumber?.ToUpperInvariant(),
            Email = request.Email,
            Phone = request.Phone,
            MedicalNotes = request.MedicalNotes,
            Allergies = request.Allergies,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(guest, ct);

        return guest.ToResponse();
    }

    public async Task<GuestResponse> UpdateAsync(
        Guid id,
        UpdateGuestRequest request,
        CancellationToken ct)
    {
        var guest = await repository.GetByIdAsync(id, ct);
        if (guest is null)
            throw new NotFoundException(nameof(Guest), id);

        guest.FirstName = request.FirstName;
        guest.LastName = request.LastName;
        guest.DateOfBirth = request.DateOfBirth;
        guest.DocumentNumber = request.DocumentNumber?.ToUpperInvariant();
        guest.Email = request.Email;
        guest.Phone = request.Phone;
        guest.MedicalNotes = request.MedicalNotes;
        guest.Allergies = request.Allergies;
        guest.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(guest, ct);

        return guest.ToResponse();
    }

    public async Task<GuestResponse> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var guest = await repository.GetByIdAsync(id, ct);
        if (guest is null)
            throw new NotFoundException(nameof(Guest), id);

        return guest.ToResponse();
    }

    public async Task<IReadOnlyList<GuestResponse>> GetByFamilyUnitAsync(
        Guid familyUnitId,
        CancellationToken ct)
    {
        var guests = await repository.GetByFamilyUnitAsync(familyUnitId, ct);
        return guests.Select(g => g.ToResponse()).ToList();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var guest = await repository.GetByIdAsync(id, ct);
        if (guest is null)
            throw new NotFoundException(nameof(Guest), id);

        guest.IsActive = false;
        guest.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(guest, ct);
    }
}
