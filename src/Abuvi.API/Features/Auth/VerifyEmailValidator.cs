using FluentValidation;

namespace Abuvi.API.Features.Auth;

/// <summary>
/// Validator for email verification requests
/// </summary>
public class VerifyEmailValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .WithMessage("El código de verificación es obligatorio");
    }
}
