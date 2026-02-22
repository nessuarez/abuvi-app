using Abuvi.API.Features.Camps;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Users;

namespace Abuvi.API.Features.Registrations;

// ── Domain Entities ──────────────────────────────────────────────────────────

public class Registration
{
    public Guid Id { get; set; }
    public Guid FamilyUnitId { get; set; }
    public Guid CampEditionId { get; set; }
    public Guid RegisteredByUserId { get; set; }
    public decimal BaseTotalAmount { get; set; }
    public decimal ExtrasAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public FamilyUnit FamilyUnit { get; set; } = null!;
    public CampEdition CampEdition { get; set; } = null!;
    public User RegisteredByUser { get; set; } = null!;
    public ICollection<RegistrationMember> Members { get; set; } = [];
    public ICollection<RegistrationExtra> Extras { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
}

public class RegistrationMember
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public Guid FamilyMemberId { get; set; }
    public int AgeAtCamp { get; set; }
    public AgeCategory AgeCategory { get; set; }
    public decimal IndividualAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public Registration Registration { get; set; } = null!;
    public FamilyMember FamilyMember { get; set; } = null!;
}

public class RegistrationExtra
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public Guid CampEditionExtraId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }         // price snapshot at selection time
    public int CampDurationDays { get; set; }      // camp duration snapshot
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public Registration Registration { get; set; } = null!;
    public CampEditionExtra CampEditionExtra { get; set; } = null!;
}

public class Payment
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? ExternalReference { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Registration Registration { get; set; } = null!;
}

// ── Enums ─────────────────────────────────────────────────────────────────────

public enum RegistrationStatus { Pending, Confirmed, Cancelled }
public enum AgeCategory { Baby, Child, Adult }
public enum PaymentMethod { Card, Transfer, Cash }
public enum PaymentStatus { Pending, Completed, Failed, Refunded }

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record CreateRegistrationRequest(
    Guid CampEditionId,
    Guid FamilyUnitId,
    List<Guid> MemberIds,
    string? Notes
);

public record UpdateRegistrationMembersRequest(List<Guid> MemberIds);

public record UpdateRegistrationExtrasRequest(List<ExtraSelectionRequest> Extras);

public record ExtraSelectionRequest(Guid CampEditionExtraId, int Quantity);

// ── Response DTOs ─────────────────────────────────────────────────────────────

public record AvailableCampEditionResponse(
    Guid Id,
    string CampName,
    int Year,
    DateTime StartDate,
    DateTime EndDate,
    string? Location,
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    int? MaxCapacity,
    int CurrentRegistrations,
    int? SpotsRemaining,
    string Status,
    AgeRangesInfo AgeRanges
);

public record AgeRangesInfo(int BabyMaxAge, int ChildMinAge, int ChildMaxAge, int AdultMinAge);

public record RegistrationResponse(
    Guid Id,
    RegistrationFamilyUnitSummary FamilyUnit,
    RegistrationCampEditionSummary CampEdition,
    RegistrationStatus Status,
    string? Notes,
    PricingBreakdown Pricing,
    List<PaymentSummary> Payments,
    decimal AmountPaid,
    decimal AmountRemaining,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record RegistrationFamilyUnitSummary(Guid Id, string Name);
public record RegistrationCampEditionSummary(Guid Id, string CampName, int Year, DateTime StartDate, DateTime EndDate, int Duration);
public record PricingBreakdown(
    List<MemberPricingDetail> Members,
    decimal BaseTotalAmount,
    List<ExtraPricingDetail> Extras,
    decimal ExtrasAmount,
    decimal TotalAmount
);
public record MemberPricingDetail(Guid FamilyMemberId, string FullName, int AgeAtCamp, AgeCategory AgeCategory, decimal IndividualAmount);
public record ExtraPricingDetail(Guid CampEditionExtraId, string Name, decimal UnitPrice, string PricingType, string PricingPeriod, int Quantity, int? CampDurationDays, string Calculation, decimal TotalAmount);
public record PaymentSummary(Guid Id, decimal Amount, DateTime PaymentDate, string Method, string Status);

public record RegistrationListResponse(
    Guid Id,
    RegistrationFamilyUnitSummary FamilyUnit,
    RegistrationCampEditionSummary CampEdition,
    RegistrationStatus Status,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal AmountRemaining,
    DateTime CreatedAt
);

public record CancelRegistrationResponse(string Message);

// ── Mapping Extensions ────────────────────────────────────────────────────────

public static class RegistrationMappingExtensions
{
    public static RegistrationResponse ToResponse(
        this Registration r,
        decimal amountPaid) => new(
        r.Id,
        new(r.FamilyUnit.Id, r.FamilyUnit.Name),
        new(r.CampEdition.Id, r.CampEdition.Camp.Name, r.CampEdition.Year,
            r.CampEdition.StartDate, r.CampEdition.EndDate,
            (r.CampEdition.EndDate - r.CampEdition.StartDate).Days),
        r.Status,
        r.Notes,
        new PricingBreakdown(
            r.Members.Select(m => new MemberPricingDetail(
                m.FamilyMemberId,
                $"{m.FamilyMember.FirstName} {m.FamilyMember.LastName}",
                m.AgeAtCamp, m.AgeCategory, m.IndividualAmount)).ToList(),
            r.BaseTotalAmount,
            r.Extras.Select(e => new ExtraPricingDetail(
                e.CampEditionExtraId, e.CampEditionExtra.Name, e.UnitPrice,
                e.CampEditionExtra.PricingType.ToString(),
                e.CampEditionExtra.PricingPeriod.ToString(),
                e.Quantity, e.CampDurationDays,
                BuildCalculation(e), e.TotalAmount)).ToList(),
            r.ExtrasAmount,
            r.TotalAmount),
        r.Payments.Select(p => new PaymentSummary(
            p.Id, p.Amount, p.PaymentDate, p.Method.ToString(), p.Status.ToString())).ToList(),
        amountPaid,
        r.TotalAmount - amountPaid,
        r.CreatedAt,
        r.UpdatedAt
    );

    private static string BuildCalculation(RegistrationExtra e)
    {
        var extra = e.CampEditionExtra;
        return (extra.PricingType, extra.PricingPeriod) switch
        {
            (PricingType.PerPerson, PricingPeriod.OneTime) =>
                $"€{e.UnitPrice} × {e.Quantity} persona(s)",
            (PricingType.PerPerson, PricingPeriod.PerDay) =>
                $"€{e.UnitPrice} × {e.Quantity} persona(s) × {e.CampDurationDays} días",
            (PricingType.PerFamily, PricingPeriod.OneTime) =>
                $"€{e.UnitPrice} (por familia)",
            (PricingType.PerFamily, PricingPeriod.PerDay) =>
                $"€{e.UnitPrice} × {e.CampDurationDays} días",
            _ => string.Empty
        };
    }
}
