namespace Abuvi.API.Features.FamilyUnits;

using FluentValidation;

public class CreateFamilyUnitValidator : AbstractValidator<CreateFamilyUnitRequest>
{
    public CreateFamilyUnitValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("El nombre de la unidad familiar es obligatorio")
            .MaximumLength(200)
            .WithMessage("El nombre de la unidad familiar no puede exceder 200 caracteres");
    }
}
