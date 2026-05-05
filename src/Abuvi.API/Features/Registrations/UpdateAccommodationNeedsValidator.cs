using FluentValidation;

namespace Abuvi.API.Features.Registrations;

public class UpdateAccommodationNeedsValidator : AbstractValidator<UpdateAccommodationNeedsRequest>
{
    public UpdateAccommodationNeedsValidator()
    {
        RuleFor(x => x.FeatureIds)
            .NotNull().WithMessage("La lista de características es obligatoria")
            .Must(ids => ids.Count <= 20)
            .WithMessage("No se pueden etiquetar más de 20 características")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("La lista contiene identificadores duplicados");

        RuleForEach(x => x.FeatureIds)
            .NotEmpty().WithMessage("El identificador de característica no puede estar vacío");
    }
}
