using FluentValidation;

namespace Abuvi.API.Features.FamilyUnits;

public class UpdateFamilyNumberValidator : AbstractValidator<UpdateFamilyNumberRequest>
{
    public UpdateFamilyNumberValidator()
    {
        RuleFor(x => x.FamilyNumber)
            .GreaterThan(0).WithMessage("El número de familia debe ser mayor a 0");
    }
}
