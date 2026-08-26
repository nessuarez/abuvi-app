using FluentValidation;

namespace Abuvi.API.Features.MediaSources;

public class CreateMediaSourceRequestValidator : AbstractValidator<CreateMediaSourceRequest>
{
    public CreateMediaSourceRequestValidator()
    {
        RuleFor(x => x.ContributorName)
            .NotEmpty().WithMessage("El nombre del aportante es obligatorio")
            .MaximumLength(200).WithMessage("El nombre no puede superar los 200 caracteres");

        RuleFor(x => x.ContributorContact)
            .MaximumLength(200).WithMessage("El contacto no puede superar los 200 caracteres");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Las notas no pueden superar los 1000 caracteres");

        RuleFor(x => x.ReceivedAt)
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1))
            .When(x => x.ReceivedAt.HasValue)
            .WithMessage("La fecha de recepción no puede estar en el futuro");
    }
}

public class UpdateMediaSourceRequestValidator : AbstractValidator<UpdateMediaSourceRequest>
{
    public UpdateMediaSourceRequestValidator()
    {
        RuleFor(x => x.ContributorName)
            .NotEmpty().WithMessage("El nombre del aportante es obligatorio")
            .MaximumLength(200).WithMessage("El nombre no puede superar los 200 caracteres");

        RuleFor(x => x.ContributorContact)
            .MaximumLength(200).WithMessage("El contacto no puede superar los 200 caracteres");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Las notas no pueden superar los 1000 caracteres");

        RuleFor(x => x.ReceivedAt)
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1))
            .When(x => x.ReceivedAt.HasValue)
            .WithMessage("La fecha de recepción no puede estar en el futuro");
    }
}

public class MergeMediaSourceRequestValidator : AbstractValidator<MergeMediaSourceRequest>
{
    public MergeMediaSourceRequestValidator()
    {
        RuleFor(x => x.TargetId)
            .NotEmpty().WithMessage("Debes indicar el aportante de destino");
    }
}
