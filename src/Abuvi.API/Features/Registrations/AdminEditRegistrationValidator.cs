using FluentValidation;

namespace Abuvi.API.Features.Registrations;

public class AdminEditRegistrationValidator : AbstractValidator<AdminEditRegistrationRequest>
{
    public AdminEditRegistrationValidator()
    {
        When(x => x.Members != null, () =>
        {
            RuleFor(x => x.Members!)
                .NotEmpty()
                .WithMessage("La lista de miembros no puede estar vacía si se proporciona")
                .Must(members => members.Select(m => m.MemberId).Distinct().Count() == members.Count)
                .WithMessage("No se puede incluir el mismo miembro dos veces");

            RuleForEach(x => x.Members!).ChildRules(member =>
            {
                member.RuleFor(m => m.MemberId)
                    .NotEmpty().WithMessage("El identificador del miembro es obligatorio");

                member.RuleFor(m => m.AttendancePeriod)
                    .IsInEnum().WithMessage("El periodo de asistencia no es válido");

                member.RuleFor(m => m.VisitStartDate)
                    .NotNull().WithMessage("La fecha de inicio de la visita es obligatoria para visitas de fin de semana")
                    .When(m => m.AttendancePeriod == AttendancePeriod.WeekendVisit);

                member.RuleFor(m => m.VisitStartDate)
                    .Null().WithMessage("La fecha de inicio de la visita solo aplica a visitas de fin de semana")
                    .When(m => m.AttendancePeriod != AttendancePeriod.WeekendVisit);

                member.RuleFor(m => m.VisitEndDate)
                    .NotNull().WithMessage("La fecha de fin de la visita es obligatoria para visitas de fin de semana")
                    .When(m => m.AttendancePeriod == AttendancePeriod.WeekendVisit);

                member.RuleFor(m => m.VisitEndDate)
                    .Null().WithMessage("La fecha de fin de la visita solo aplica a visitas de fin de semana")
                    .When(m => m.AttendancePeriod != AttendancePeriod.WeekendVisit);

                member.RuleFor(m => m)
                    .Must(m => m.VisitEndDate!.Value.DayNumber - m.VisitStartDate!.Value.DayNumber <= 3)
                    .WithMessage("La visita de fin de semana no puede superar los 3 días")
                    .WithName("VisitEndDate")
                    .When(m => m.AttendancePeriod == AttendancePeriod.WeekendVisit
                               && m.VisitStartDate.HasValue && m.VisitEndDate.HasValue);

                member.RuleFor(m => m)
                    .Must(m => m.VisitEndDate!.Value > m.VisitStartDate!.Value)
                    .WithMessage("La fecha de fin de la visita debe ser posterior a la de inicio")
                    .WithName("VisitEndDate")
                    .When(m => m.AttendancePeriod == AttendancePeriod.WeekendVisit
                               && m.VisitStartDate.HasValue && m.VisitEndDate.HasValue);
            });
        });

        When(x => x.Extras != null, () =>
        {
            RuleForEach(x => x.Extras!).ChildRules(extra =>
            {
                extra.RuleFor(e => e.CampEditionExtraId)
                    .NotEmpty().WithMessage("El identificador del extra es obligatorio");
                extra.RuleFor(e => e.Quantity)
                    .GreaterThan(0).WithMessage("La cantidad debe ser mayor que 0");
            });
        });

        When(x => x.Preferences != null, () =>
        {
            RuleForEach(x => x.Preferences!).ChildRules(pref =>
            {
                pref.RuleFor(p => p.CampEditionAccommodationId)
                    .NotEmpty().WithMessage("El identificador del alojamiento es obligatorio");
                pref.RuleFor(p => p.PreferenceOrder)
                    .GreaterThan(0).WithMessage("El orden de preferencia debe ser mayor que 0");
            });
        });

        RuleFor(x => x.Notes).MaximumLength(1000)
            .WithMessage("Las notas no pueden superar los 1000 caracteres");
        RuleFor(x => x.SpecialNeeds).MaximumLength(2000)
            .WithMessage("Las necesidades especiales no pueden superar los 2000 caracteres");
        RuleFor(x => x.CampatesPreference).MaximumLength(500)
            .WithMessage("La preferencia de acampantes no puede superar los 500 caracteres");
    }
}
