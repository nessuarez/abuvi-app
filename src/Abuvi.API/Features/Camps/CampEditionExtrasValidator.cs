using FluentValidation;

namespace Abuvi.API.Features.Camps;

public class CreateCampEditionExtraRequestValidator
    : AbstractValidator<CreateCampEditionExtraRequest>
{
    public CreateCampEditionExtraRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio")
            .MaximumLength(200).WithMessage("El nombre no puede superar los 200 caracteres");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("La descripción no puede superar los 1000 caracteres");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("El precio debe ser 0 o mayor")
            .PrecisionScale(10, 2, ignoreTrailingZeros: true)
            .WithMessage("El precio no puede tener más de 2 decimales");

        RuleFor(x => x.PricingType)
            .IsInEnum().WithMessage("El tipo de precio no es válido");

        RuleFor(x => x.PricingPeriod)
            .IsInEnum().WithMessage("El período de precio no es válido");

        RuleFor(x => x.MaxQuantity)
            .GreaterThan(0).WithMessage("La cantidad máxima debe ser mayor que 0")
            .When(x => x.MaxQuantity.HasValue);

        RuleFor(x => x.UserInputLabel)
            .MaximumLength(200).WithMessage("La etiqueta de entrada no puede superar los 200 caracteres")
            .When(x => x.UserInputLabel != null);
    }
}

public class UpdateCampEditionExtraRequestValidator
    : AbstractValidator<UpdateCampEditionExtraRequest>
{
    public UpdateCampEditionExtraRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio")
            .MaximumLength(200).WithMessage("El nombre no puede superar los 200 caracteres");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("La descripción no puede superar los 1000 caracteres");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("El precio debe ser 0 o mayor")
            .PrecisionScale(10, 2, ignoreTrailingZeros: true)
            .WithMessage("El precio no puede tener más de 2 decimales");

        RuleFor(x => x.MaxQuantity)
            .GreaterThan(0).WithMessage("La cantidad máxima debe ser mayor que 0")
            .When(x => x.MaxQuantity.HasValue);

        RuleFor(x => x.UserInputLabel)
            .MaximumLength(200).WithMessage("La etiqueta de entrada no puede superar los 200 caracteres")
            .When(x => x.UserInputLabel != null);
    }
}
