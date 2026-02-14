using FluentValidation;

namespace Abuvi.API.Features.Auth;

/// <summary>
/// Validator for login requests
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .WithMessage("Se requiere una dirección de correo electrónico válida");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("La contraseña es obligatoria");
    }
}
