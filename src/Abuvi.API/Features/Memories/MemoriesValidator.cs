using FluentValidation;

namespace Abuvi.API.Features.Memories;

public class CreateMemoryRequestValidator : AbstractValidator<CreateMemoryRequest>
{
    public CreateMemoryRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("El título es obligatorio")
            .MaximumLength(200).WithMessage("El título no puede exceder 200 caracteres");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("El contenido es obligatorio");

        RuleFor(x => x.Year)
            .InclusiveBetween(1975, 2026).WithMessage("El año debe estar entre 1975 y 2026")
            .When(x => x.Year.HasValue);
    }
}
