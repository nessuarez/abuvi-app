using FluentValidation;

namespace Abuvi.API.Features.MediaThemes;

public class CreateMediaThemeRequestValidator : AbstractValidator<CreateMediaThemeRequest>
{
    public CreateMediaThemeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del tema es obligatorio")
            .MaximumLength(100).WithMessage("El nombre no puede superar los 100 caracteres");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("La descripción no puede superar los 500 caracteres");
    }
}

public class UpdateMediaThemeRequestValidator : AbstractValidator<UpdateMediaThemeRequest>
{
    public UpdateMediaThemeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del tema es obligatorio")
            .MaximumLength(100).WithMessage("El nombre no puede superar los 100 caracteres");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("La descripción no puede superar los 500 caracteres");
    }
}

public class AttachThemeRequestValidator : AbstractValidator<AttachThemeRequest>
{
    public AttachThemeRequestValidator()
    {
        RuleFor(x => x.ThemeId)
            .NotEmpty().WithMessage("Debes indicar el tema");
    }
}
