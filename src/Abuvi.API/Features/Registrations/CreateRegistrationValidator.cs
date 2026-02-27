using Abuvi.API.Features.Camps;
using FluentValidation;

namespace Abuvi.API.Features.Registrations;

public class CreateRegistrationValidator : AbstractValidator<CreateRegistrationRequest>
{
    public CreateRegistrationValidator(ICampEditionsRepository editionsRepo)
    {
        RuleFor(x => x.CampEditionId)
            .NotEmpty()
            .WithMessage("La edición del campamento es obligatoria")
            .MustAsync(async (id, ct) =>
            {
                var edition = await editionsRepo.GetByIdAsync(id, ct);
                return edition?.Status == CampEditionStatus.Open;
            })
            .WithMessage("La edición del campamento no está abierta para inscripción");

        RuleFor(x => x.FamilyUnitId)
            .NotEmpty()
            .WithMessage("La unidad familiar es obligatoria");

        RuleFor(x => x.Members)
            .NotEmpty()
            .WithMessage("Debe seleccionar al menos un miembro de la familia")
            .Must(members => members != null && members.Select(m => m.MemberId).Distinct().Count() == members.Count)
            .WithMessage("No se puede incluir el mismo miembro dos veces");

        RuleForEach(x => x.Members).ChildRules(member =>
        {
            member.RuleFor(m => m.MemberId)
                .NotEmpty().WithMessage("El identificador del miembro es obligatorio");

            member.RuleFor(m => m.AttendancePeriod)
                .IsInEnum().WithMessage("El periodo de asistencia no es válido");

            // VisitStartDate required when WeekendVisit, must be null otherwise
            member.RuleFor(m => m.VisitStartDate)
                .NotNull().WithMessage("La fecha de inicio de la visita es obligatoria para visitas de fin de semana")
                .When(m => m.AttendancePeriod == AttendancePeriod.WeekendVisit);

            member.RuleFor(m => m.VisitStartDate)
                .Null().WithMessage("La fecha de inicio de la visita solo aplica a visitas de fin de semana")
                .When(m => m.AttendancePeriod != AttendancePeriod.WeekendVisit);

            // VisitEndDate required when WeekendVisit, must be null otherwise
            member.RuleFor(m => m.VisitEndDate)
                .NotNull().WithMessage("La fecha de fin de la visita es obligatoria para visitas de fin de semana")
                .When(m => m.AttendancePeriod == AttendancePeriod.WeekendVisit);

            member.RuleFor(m => m.VisitEndDate)
                .Null().WithMessage("La fecha de fin de la visita solo aplica a visitas de fin de semana")
                .When(m => m.AttendancePeriod != AttendancePeriod.WeekendVisit);

            // Duration ≤ 3 days
            member.RuleFor(m => m)
                .Must(m => m.VisitEndDate!.Value.DayNumber - m.VisitStartDate!.Value.DayNumber <= 3)
                .WithMessage("La visita de fin de semana no puede superar los 3 días")
                .WithName("VisitEndDate")
                .When(m => m.AttendancePeriod == AttendancePeriod.WeekendVisit
                           && m.VisitStartDate.HasValue && m.VisitEndDate.HasValue);

            // VisitEndDate must be after VisitStartDate
            member.RuleFor(m => m)
                .Must(m => m.VisitEndDate!.Value > m.VisitStartDate!.Value)
                .WithMessage("La fecha de fin de la visita debe ser posterior a la de inicio")
                .WithName("VisitEndDate")
                .When(m => m.AttendancePeriod == AttendancePeriod.WeekendVisit
                           && m.VisitStartDate.HasValue && m.VisitEndDate.HasValue);

            member.RuleFor(m => m.GuardianName)
                .MaximumLength(200)
                .WithMessage("El nombre del tutor no puede superar los 200 caracteres")
                .When(m => m.GuardianName is not null);

            member.RuleFor(m => m.GuardianDocumentNumber)
                .MaximumLength(50)
                .WithMessage("El documento del tutor no puede superar los 50 caracteres")
                .When(m => m.GuardianDocumentNumber is not null);
        });

        // Visit dates within camp bounds: validated in RegistrationsService.CreateAsync (cross-entity constraint)

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .WithMessage("Las notas no pueden superar los 1000 caracteres");

        RuleFor(x => x.SpecialNeeds)
            .MaximumLength(2000)
            .WithMessage("Las necesidades especiales no pueden superar los 2000 caracteres")
            .When(x => x.SpecialNeeds is not null);

        RuleFor(x => x.CampatesPreference)
            .MaximumLength(500)
            .WithMessage("La preferencia de acampantes no puede superar los 500 caracteres")
            .When(x => x.CampatesPreference is not null);
    }
}
