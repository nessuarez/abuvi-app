using FluentValidation;

namespace Abuvi.API.Features.Registrations;

public class UpdateFriendLinksValidator : AbstractValidator<UpdateFriendLinksRequest>
{
    public UpdateFriendLinksValidator()
    {
        RuleFor(x => x.LinkedRegistrationIds)
            .NotNull().WithMessage("La lista de inscripciones vinculadas es obligatoria")
            .Must(ids => ids.Count <= 10)
            .WithMessage("No se pueden vincular más de 10 familias amigas")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("La lista contiene identificadores duplicados");

        RuleForEach(x => x.LinkedRegistrationIds)
            .NotEmpty().WithMessage("El identificador de inscripción vinculada no puede estar vacío");
    }
}
