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
    // Extra fields from Google Forms 2026
    public string? SpecialNeeds { get; set; }
    public string? CampatesPreference { get; set; }
    public bool HasPet { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? AdminModifiedAt { get; set; }

    // Navigation properties
    public FamilyUnit FamilyUnit { get; set; } = null!;
    public CampEdition CampEdition { get; set; } = null!;
    public User RegisteredByUser { get; set; } = null!;
    public ICollection<RegistrationMember> Members { get; set; } = [];
    public ICollection<RegistrationExtra> Extras { get; set; } = [];
    public ICollection<RegistrationAccommodationPreference> AccommodationPreferences { get; set; } = [];
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
    public AttendancePeriod AttendancePeriod { get; set; } = AttendancePeriod.Complete;
    // Only populated when AttendancePeriod = WeekendVisit
    public DateOnly? VisitStartDate { get; set; }
    public DateOnly? VisitEndDate { get; set; }
    // Guardian info (only meaningful for minors: AgeCategory Baby or Child)
    public string? GuardianName { get; set; }
    public string? GuardianDocumentNumber { get; set; }
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
    public string? UserInput { get; set; }
    public DateTime CreatedAt { get; set; }
    public Registration Registration { get; set; } = null!;
    public CampEditionExtra CampEditionExtra { get; set; } = null!;
}

public class RegistrationAccommodationPreference
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public Guid CampEditionAccommodationId { get; set; }
    public int PreferenceOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Registration Registration { get; set; } = null!;
    public CampEditionAccommodation CampEditionAccommodation { get; set; } = null!;
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
    public int InstallmentNumber { get; set; }
    public DateTime? DueDate { get; set; }
    public string? TransferConcept { get; set; }
    public string? ProofFileUrl { get; set; }
    public string? ProofFileName { get; set; }
    public DateTime? ProofUploadedAt { get; set; }
    public string? AdminNotes { get; set; }
    public string? ConceptLinesSerialized { get; set; }
    public bool IsManual { get; set; } = false;
    public Guid? ConfirmedByUserId { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Registration Registration { get; set; } = null!;
}

// ── Enums ─────────────────────────────────────────────────────────────────────

public enum RegistrationStatus { Pending, Confirmed, Cancelled, Draft }
public enum AgeCategory { Baby, Child, Adult }
public enum PaymentMethod { Card, Transfer, Cash }
public enum PaymentStatus { Pending, PendingReview, Completed, Failed, Refunded }

public enum AttendancePeriod
{
    Complete,      // Full camp (default)
    FirstWeek,
    SecondWeek,
    WeekendVisit   // Short visit, max 3 days, configurable window
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record MemberAttendanceRequest(
    Guid MemberId,
    AttendancePeriod AttendancePeriod,
    DateOnly? VisitStartDate = null,   // Required when AttendancePeriod = WeekendVisit
    DateOnly? VisitEndDate = null,     // Required when AttendancePeriod = WeekendVisit
    string? GuardianName = null,
    string? GuardianDocumentNumber = null
);

public record CreateRegistrationRequest(
    Guid CampEditionId,
    Guid FamilyUnitId,
    List<MemberAttendanceRequest> Members,
    string? Notes,
    string? SpecialNeeds,
    string? CampatesPreference,
    bool HasPet = false
);

public record UpdateRegistrationMembersRequest(List<MemberAttendanceRequest> Members);

public record UpdateRegistrationExtrasRequest(List<ExtraSelectionRequest> Extras);

public record ExtraSelectionRequest(Guid CampEditionExtraId, int Quantity, string? UserInput = null);

public record AccommodationPreferenceRequest(Guid CampEditionAccommodationId, int PreferenceOrder);

public record UpdateRegistrationAccommodationPreferencesRequest(
    List<AccommodationPreferenceRequest> Preferences
);

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
    AgeRangesInfo AgeRanges,
    // Partial attendance:
    bool AllowsPartialAttendance,
    decimal? PricePerAdultWeek,
    decimal? PricePerChildWeek,
    decimal? PricePerBabyWeek,
    DateOnly? HalfDate,
    int FirstWeekDays,
    int SecondWeekDays,
    // Weekend visit:
    bool AllowsWeekendVisit,
    decimal? PricePerAdultWeekend,
    decimal? PricePerChildWeekend,
    decimal? PricePerBabyWeekend,
    DateOnly? WeekendStartDate,
    DateOnly? WeekendEndDate,
    int WeekendDays,
    int? MaxWeekendCapacity,
    int? WeekendSpotsRemaining,
    DateTime? FirstPaymentDeadline,
    DateTime? SecondPaymentDeadline
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
    DateTime UpdatedAt,
    string? SpecialNeeds,
    string? CampatesPreference,
    bool HasPet,
    bool IsAdminModified
);

public record RegistrationFamilyUnitSummary(Guid Id, string Name, Guid RepresentativeUserId);
public record RegistrationCampEditionSummary(Guid Id, string CampName, int Year, DateTime StartDate, DateTime EndDate, int Duration, string? Location);
public record PricingBreakdown(
    List<MemberPricingDetail> Members,
    decimal BaseTotalAmount,
    List<ExtraPricingDetail> Extras,
    decimal ExtrasAmount,
    decimal TotalAmount
);
public record MemberPricingDetail(
    Guid FamilyMemberId,
    string FullName,
    int AgeAtCamp,
    AgeCategory AgeCategory,
    AttendancePeriod AttendancePeriod,
    int AttendanceDays,
    DateOnly? VisitStartDate,
    DateOnly? VisitEndDate,
    decimal IndividualAmount,
    string? GuardianName,
    string? GuardianDocumentNumber
);

public record PaymentSummary(
    Guid Id,
    int InstallmentNumber,
    decimal Amount,
    DateTime? DueDate,
    string Method,
    string Status,
    string? TransferConcept,
    string? ProofFileUrl,
    string? ProofFileName,
    DateTime? ProofUploadedAt,
    string? AdminNotes
);
public record ExtraPricingDetail(
  Guid CampEditionExtraId,
  string Name,
  decimal UnitPrice,
  string PricingType,
  string PricingPeriod,
  int Quantity,
  int? CampDurationDays,
  string Calculation,
  decimal TotalAmount,
  string? UserInput);

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

public record AccommodationPreferenceResponse(
    Guid CampEditionAccommodationId,
    string AccommodationName,
    AccommodationType AccommodationType,
    int PreferenceOrder
);

// ── Admin DTOs ───────────────────────────────────────────────────────────────

public record AdminRegistrationListResponse(
    List<AdminRegistrationListItem> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    AdminRegistrationTotals Totals
);

public record AdminRegistrationListItem(
    Guid Id,
    RegistrationFamilyUnitSummary FamilyUnit,
    RepresentativeSummary Representative,
    RegistrationStatus Status,
    int MemberCount,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal AmountRemaining,
    DateTime CreatedAt
);

public record RepresentativeSummary(
    Guid Id,
    string FirstName,
    string LastName,
    string Email
);

public record AdminRegistrationTotals(
    int TotalRegistrations,
    int TotalMembers,
    decimal TotalAmount,
    decimal TotalPaid,
    decimal TotalRemaining
);

public record AdminRegistrationProjection(
    Guid Id,
    Guid FamilyUnitId,
    string FamilyUnitName,
    Guid RepresentativeUserId,
    string RepresentativeFirstName,
    string RepresentativeLastName,
    string RepresentativeEmail,
    RegistrationStatus Status,
    int MemberCount,
    decimal TotalAmount,
    decimal AmountPaid,
    DateTime CreatedAt
);

public record AdminEditRegistrationRequest(
    List<MemberAttendanceRequest>? Members,
    List<ExtraSelectionRequest>? Extras,
    List<AccommodationPreferenceRequest>? Preferences,
    string? Notes,
    string? SpecialNeeds,
    string? CampatesPreference,
    bool? HasPet
);

// ── Mapping Extensions ────────────────────────────────────────────────────────

public static class RegistrationMappingExtensions
{
    public static RegistrationResponse ToResponse(
        this Registration r,
        decimal amountPaid) => new(
        r.Id,
        new(r.FamilyUnit.Id, r.FamilyUnit.Name, r.FamilyUnit.RepresentativeUserId),
        new(r.CampEdition.Id, r.CampEdition.Camp.Name, r.CampEdition.Year,
            r.CampEdition.StartDate, r.CampEdition.EndDate,
            (r.CampEdition.EndDate - r.CampEdition.StartDate).Days,
            r.CampEdition.Camp.Location),
        r.Status,
        r.Notes,
        new PricingBreakdown(
            r.Members.Select(m => new MemberPricingDetail(
                m.FamilyMemberId,
                $"{m.FamilyMember.FirstName} {m.FamilyMember.LastName}",
                m.AgeAtCamp,
                m.AgeCategory,
                m.AttendancePeriod,
                RegistrationPricingService.GetPeriodDays(
                    m.AttendancePeriod, r.CampEdition, m.VisitStartDate, m.VisitEndDate),
                m.VisitStartDate,
                m.VisitEndDate,
                m.IndividualAmount,
                m.GuardianName,
                m.GuardianDocumentNumber)).ToList(),
            r.BaseTotalAmount,
            r.Extras.Select(e => new ExtraPricingDetail(
                e.CampEditionExtraId, e.CampEditionExtra.Name, e.UnitPrice,
                e.CampEditionExtra.PricingType.ToString(),
                e.CampEditionExtra.PricingPeriod.ToString(),
                e.Quantity, e.CampDurationDays,
                BuildCalculation(e), e.TotalAmount, e.UserInput)).ToList(),
            r.ExtrasAmount,
            r.TotalAmount),
        r.Payments.Select(p => new PaymentSummary(
            p.Id, p.InstallmentNumber, p.Amount, p.DueDate,
            p.Method.ToString(), p.Status.ToString(),
            p.TransferConcept, p.ProofFileUrl, p.ProofFileName,
            p.ProofUploadedAt, p.AdminNotes)).ToList(),
        amountPaid,
        r.TotalAmount - amountPaid,
        r.CreatedAt,
        r.UpdatedAt,
        r.SpecialNeeds,
        r.CampatesPreference,
        r.HasPet,
        r.AdminModifiedAt != null && r.Status == RegistrationStatus.Draft
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
