namespace Abuvi.API.Features.Auth;

using FluentValidation;

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("El token de recuperación es obligatorio");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("La nueva contraseña es obligatoria")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres")
            .Matches(@"[A-Z]").WithMessage("La contraseña debe contener al menos una letra mayúscula")
            .Matches(@"[a-z]").WithMessage("La contraseña debe contener al menos una letra minúscula")
            .Matches(@"\d").WithMessage("La contraseña debe contener al menos un dígito")
            .Matches(@"[@$!%*?&#]").WithMessage("La contraseña debe contener al menos un carácter especial");
    }
}
