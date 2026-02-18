namespace Abuvi.API.Features.Auth;

using FluentValidation;

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio")
            .EmailAddress().WithMessage("Formato de correo electrónico inválido")
            .MaximumLength(255);
    }
}
