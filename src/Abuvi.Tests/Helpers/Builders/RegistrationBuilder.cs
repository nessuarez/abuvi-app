using Abuvi.API.Features.Camps;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Registrations;

namespace Abuvi.Tests.Helpers.Builders;

public class RegistrationBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _familyUnitId = Guid.NewGuid();
    private Guid _campEditionId = Guid.NewGuid();
    private Guid _registeredByUserId = Guid.NewGuid();
    private decimal _baseTotalAmount = 300m;
    private decimal _extrasAmount = 0m;
    private RegistrationStatus _status = RegistrationStatus.Pending;

    public RegistrationBuilder WithId(Guid id) { _id = id; return this; }
    public RegistrationBuilder WithFamilyUnitId(Guid id) { _familyUnitId = id; return this; }
    public RegistrationBuilder WithCampEditionId(Guid id) { _campEditionId = id; return this; }
    public RegistrationBuilder WithRegisteredByUserId(Guid id) { _registeredByUserId = id; return this; }
    public RegistrationBuilder WithStatus(RegistrationStatus status) { _status = status; return this; }
    public RegistrationBuilder WithBaseTotalAmount(decimal amount) { _baseTotalAmount = amount; return this; }
    public RegistrationBuilder WithExtrasAmount(decimal amount) { _extrasAmount = amount; return this; }

    public Registration Build() => new()
    {
        Id = _id,
        FamilyUnitId = _familyUnitId,
        CampEditionId = _campEditionId,
        RegisteredByUserId = _registeredByUserId,
        BaseTotalAmount = _baseTotalAmount,
        ExtrasAmount = _extrasAmount,
        TotalAmount = _baseTotalAmount + _extrasAmount,
        Status = _status,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        FamilyUnit = new FamilyUnit
        {
            Id = _familyUnitId,
            Name = "Test Family",
            RepresentativeUserId = _registeredByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        CampEdition = new CampEdition
        {
            Id = _campEditionId,
            CampId = Guid.NewGuid(),
            Year = 2025,
            StartDate = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2025, 7, 14, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult = 500m,
            PricePerChild = 300m,
            PricePerBaby = 100m,
            Status = CampEditionStatus.Open,
            Camp = new Camp
            {
                Id = Guid.NewGuid(),
                Name = "Test Camp",
                PricePerAdult = 500m,
                PricePerChild = 300m,
                PricePerBaby = 100m
            }
        },
        Members = [],
        Extras = [],
        Payments = []
    };
}
