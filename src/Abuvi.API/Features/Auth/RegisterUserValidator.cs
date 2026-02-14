namespace Abuvi.API.Features.Auth;

using FluentValidation;

/// <summary>
/// Validator for user registration requests
/// </summary>
public class RegisterUserValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio")
            .EmailAddress().WithMessage("Formato de correo electrónico inválido")
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres")
            .Matches(@"[A-Z]").WithMessage("La contraseña debe contener al menos una letra mayúscula")
            .Matches(@"[a-z]").WithMessage("La contraseña debe contener al menos una letra minúscula")
            .Matches(@"\d").WithMessage("La contraseña debe contener al menos un dígito")
            .Matches(@"[@$!%*?&#]").WithMessage("La contraseña debe contener al menos un carácter especial");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("El nombre es obligatorio")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Los apellidos son obligatorios")
            .MaximumLength(100);

        RuleFor(x => x.DocumentNumber)
            .MaximumLength(50)
            .Matches(@"^[A-Z0-9]+$").When(x => !string.IsNullOrEmpty(x.DocumentNumber))
            .WithMessage("El número de documento solo debe contener letras mayúsculas y números");

        RuleFor(x => x.Phone)
            .Matches(@"^\+?[1-9]\d{1,14}$").When(x => !string.IsNullOrEmpty(x.Phone))
            .WithMessage("Formato de número de teléfono inválido (E.164)");

        RuleFor(x => x.AcceptedTerms)
            .Equal(true).WithMessage("Debes aceptar los términos y condiciones");
    }
}
