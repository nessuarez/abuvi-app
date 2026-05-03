using FluentValidation;

namespace Abuvi.API.Features.Registrations;

public class ChangeRegistrationStatusRequestValidator : AbstractValidator<ChangeRegistrationStatusRequest>
{
    public ChangeRegistrationStatusRequestValidator()
    {
        RuleFor(x => x.NewStatus).IsInEnum()
            .WithMessage("El estado de inscripción no es válido");
    }
}
