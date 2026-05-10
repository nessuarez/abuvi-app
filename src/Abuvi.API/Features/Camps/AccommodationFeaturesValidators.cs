using FluentValidation;

namespace Abuvi.API.Features.Camps;

public class CreateAccommodationFeatureValidator : AbstractValidator<CreateAccommodationFeatureRequest>
{
    public CreateAccommodationFeatureValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la característica es obligatorio")
            .MaximumLength(100).WithMessage("El nombre no puede superar los 100 caracteres");

        RuleFor(x => x.Icon)
            .NotEmpty().WithMessage("El icono es obligatorio")
            .MaximumLength(100).WithMessage("El icono no puede superar los 100 caracteres");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("La descripción no puede superar los 500 caracteres")
            .When(x => x.Description is not null);

        RuleFor(x => x.ApplicabilityLevel)
            .IsInEnum().WithMessage("El nivel de aplicación no es válido");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("El orden debe ser mayor o igual a 0");
    }
}

public class UpdateAccommodationFeatureValidator : AbstractValidator<UpdateAccommodationFeatureRequest>
{
    public UpdateAccommodationFeatureValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la característica es obligatorio")
            .MaximumLength(100).WithMessage("El nombre no puede superar los 100 caracteres");

        RuleFor(x => x.Icon)
            .NotEmpty().WithMessage("El icono es obligatorio")
            .MaximumLength(100).WithMessage("El icono no puede superar los 100 caracteres");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("La descripción no puede superar los 500 caracteres")
            .When(x => x.Description is not null);

        RuleFor(x => x.ApplicabilityLevel)
            .IsInEnum().WithMessage("El nivel de aplicación no es válido");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("El orden debe ser mayor o igual a 0");
    }
}

public class SetFeatureAssignmentsValidator : AbstractValidator<SetFeatureAssignmentsRequest>
{
    public SetFeatureAssignmentsValidator()
    {
        RuleFor(x => x.FeatureIds)
            .NotNull().WithMessage("La lista de características no puede ser nula");

        RuleForEach(x => x.FeatureIds)
            .NotEmpty().WithMessage("El identificador de característica no puede ser vacío");
    }
}
