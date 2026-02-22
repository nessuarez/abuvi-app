using FluentValidation;

namespace Abuvi.API.Features.Registrations;

public class UpdateRegistrationMembersValidator : AbstractValidator<UpdateRegistrationMembersRequest>
{
    public UpdateRegistrationMembersValidator()
    {
        RuleFor(x => x.MemberIds)
            .NotEmpty()
            .WithMessage("Debe seleccionar al menos un miembro de la familia")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("No se puede incluir el mismo miembro dos veces");
    }
}
