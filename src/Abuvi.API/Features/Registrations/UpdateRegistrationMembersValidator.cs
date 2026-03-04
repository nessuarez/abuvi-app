using FluentValidation;

namespace Abuvi.API.Features.Registrations;

public class UpdateRegistrationMembersValidator : AbstractValidator<UpdateRegistrationMembersRequest>
{
    public UpdateRegistrationMembersValidator()
    {
        RuleFor(x => x.Members)
            .NotEmpty()
            .WithMessage("Debe seleccionar al menos un miembro de la familia")
            .Must(members => members.Select(m => m.MemberId).Distinct().Count() == members.Count)
            .WithMessage("No se puede incluir el mismo miembro dos veces");

        RuleForEach(x => x.Members).ChildRules(member =>
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
    }
}
