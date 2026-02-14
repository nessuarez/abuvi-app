using FluentValidation;

namespace Abuvi.API.Features.Auth;

/// <summary>
/// Validator for resend verification email requests
/// </summary>
public class ResendVerificationValidator : AbstractValidator<ResendVerificationRequest>
{
    public ResendVerificationValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("El correo electrónico es obligatorio")
            .EmailAddress()
            .WithMessage("El correo electrónico debe ser una dirección válida");
    }
}
