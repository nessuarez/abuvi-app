using FluentValidation;

namespace Abuvi.API.Features.MediaItems;

public class CreateMediaItemRequestValidator : AbstractValidator<CreateMediaItemRequest>
{
    public CreateMediaItemRequestValidator()
    {
        RuleFor(x => x.FileUrl)
            .NotEmpty().WithMessage("La URL del archivo es obligatoria")
            .MaximumLength(2048).WithMessage("La URL del archivo no puede exceder 2048 caracteres");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("El tipo de medio no es válido");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("El título es obligatorio")
            .MaximumLength(200).WithMessage("El título no puede exceder 200 caracteres");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("La descripción no puede exceder 1000 caracteres")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Year)
            .InclusiveBetween(1975, 2026).WithMessage("El año debe estar entre 1975 y 2026")
            .When(x => x.Year.HasValue);

        RuleFor(x => x.Context)
            .MaximumLength(50).WithMessage("El contexto no puede exceder 50 caracteres")
            .When(x => !string.IsNullOrEmpty(x.Context));

        RuleFor(x => x.ThumbnailUrl)
            .NotEmpty().WithMessage("La URL de miniatura es obligatoria para tipos Foto y Video")
            .When(x => x.Type is MediaItemType.Photo or MediaItemType.Video);
    }
}
