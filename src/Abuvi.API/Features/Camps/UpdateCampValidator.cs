namespace Abuvi.API.Features.Camps;

using FluentValidation;

public class UpdateCampValidator : AbstractValidator<UpdateCampRequest>
{
    public UpdateCampValidator()
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

        RuleFor(x => x.Province)
            .MaximumLength(100).When(x => x.Province != null)
            .WithMessage("La provincia no puede exceder 100 caracteres");

        RuleFor(x => x.ContactEmail)
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.ContactEmail))
            .WithMessage("El email de contacto no es válido");

        RuleFor(x => x.ContactPerson)
            .MaximumLength(200).When(x => x.ContactPerson != null)
            .WithMessage("El nombre de contacto no puede exceder 200 caracteres");

        RuleFor(x => x.ContactCompany)
            .MaximumLength(200).When(x => x.ContactCompany != null)
            .WithMessage("La empresa de contacto no puede exceder 200 caracteres");

        RuleFor(x => x.SecondaryWebsiteUrl)
            .MaximumLength(500).When(x => x.SecondaryWebsiteUrl != null)
            .WithMessage("La URL secundaria no puede exceder 500 caracteres");

        RuleFor(x => x.BasePrice)
            .GreaterThanOrEqualTo(0).When(x => x.BasePrice.HasValue)
            .WithMessage("El precio base debe ser mayor o igual a 0");

        RuleFor(x => x.AbuviContactedAt)
            .MaximumLength(100).When(x => x.AbuviContactedAt != null)
            .WithMessage("La fecha de contacto ABUVI no puede exceder 100 caracteres");

        RuleFor(x => x.AbuviPossibility)
            .MaximumLength(100).When(x => x.AbuviPossibility != null)
            .WithMessage("La posibilidad ABUVI no puede exceder 100 caracteres");

        RuleFor(x => x.AbuviLastVisited)
            .MaximumLength(200).When(x => x.AbuviLastVisited != null)
            .WithMessage("La última visita ABUVI no puede exceder 200 caracteres");
    }
}
