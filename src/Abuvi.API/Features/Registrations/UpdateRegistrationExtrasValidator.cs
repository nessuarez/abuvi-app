using FluentValidation;

namespace Abuvi.API.Features.Registrations;

public class UpdateRegistrationExtrasValidator : AbstractValidator<UpdateRegistrationExtrasRequest>
{
    public UpdateRegistrationExtrasValidator()
    {
        RuleFor(x => x.Extras)
            .NotNull()
            .WithMessage("La lista de extras es obligatoria");

        RuleForEach(x => x.Extras).ChildRules(extra =>
        {
            extra.RuleFor(e => e.CampEditionExtraId)
                .NotEmpty()
                .WithMessage("El identificador del extra es obligatorio");
            extra.RuleFor(e => e.Quantity)
                .GreaterThan(0)
                .WithMessage("La cantidad debe ser mayor que cero");
            extra.RuleFor(e => e.UserInput)
                .MaximumLength(500)
                .WithMessage("El texto de entrada no puede superar los 500 caracteres")
                .When(e => e.UserInput != null);
        });
    }
}
