using FluentValidation;

namespace Abuvi.API.Features.MediaComments;

public class CreateMediaCommentRequestValidator : AbstractValidator<CreateMediaCommentRequest>
{
    public CreateMediaCommentRequestValidator()
    {
        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("El comentario no puede estar vacío")
            .MaximumLength(1000).WithMessage("El comentario no puede superar los 1000 caracteres");
    }
}

public class UpdateMediaCommentRequestValidator : AbstractValidator<UpdateMediaCommentRequest>
{
    public UpdateMediaCommentRequestValidator()
    {
        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("El comentario no puede estar vacío")
            .MaximumLength(1000).WithMessage("El comentario no puede superar los 1000 caracteres");
    }
}

public class ReportMediaCommentRequestValidator : AbstractValidator<ReportMediaCommentRequest>
{
    public ReportMediaCommentRequestValidator()
    {
        RuleFor(x => x.Reason)
            .IsInEnum().WithMessage("El motivo de la denuncia no es válido");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Las notas no pueden superar los 500 caracteres");
    }
}
