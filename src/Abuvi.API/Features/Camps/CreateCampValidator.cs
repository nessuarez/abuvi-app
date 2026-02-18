namespace Abuvi.API.Features.Camps;

using FluentValidation;

public class CreateCampValidator : AbstractValidator<CreateCampRequest>
{
    public CreateCampValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio")
            .MaximumLength(200).WithMessage("El nombre no puede exceder 200 caracteres");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("La descripción no puede exceder 2000 caracteres");

        RuleFor(x => x.Location)
            .MaximumLength(500).WithMessage("La ubicación no puede exceder 500 caracteres");

        RuleFor(x => x.GooglePlaceId)
            .MaximumLength(255).WithMessage("El ID de lugar de Google no puede exceder 255 caracteres");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue)
            .WithMessage("La latitud debe estar entre -90 y 90");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue)
            .WithMessage("La longitud debe estar entre -180 y 180");

        RuleFor(x => x.PricePerAdult)
            .GreaterThanOrEqualTo(0).WithMessage("El precio por adulto debe ser mayor o igual a 0");

        RuleFor(x => x.PricePerChild)
            .GreaterThanOrEqualTo(0).WithMessage("El precio por niño debe ser mayor o igual a 0");

        RuleFor(x => x.PricePerBaby)
            .GreaterThanOrEqualTo(0).WithMessage("El precio por bebé debe ser mayor o igual a 0");
    }
}
