using FluentValidation;

namespace Abuvi.API.Features.Users;

/// <summary>
/// Validator for CreateUserRequest
/// </summary>
public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio")
            .EmailAddress().WithMessage("El correo electrónico debe ser una dirección válida")
            .MaximumLength(255).WithMessage("El correo electrónico no debe exceder 255 caracteres");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres")
            .MaximumLength(100).WithMessage("La contraseña no debe exceder 100 caracteres");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("El nombre es obligatorio")
            .MaximumLength(100).WithMessage("El nombre no debe exceder 100 caracteres");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Los apellidos son obligatorios")
            .MaximumLength(100).WithMessage("Los apellidos no deben exceder 100 caracteres");

        RuleFor(x => x.Phone)
            .MaximumLength(20).WithMessage("El teléfono no debe exceder 20 caracteres")
            .Matches(@"^\+?[0-9\s\-\(\)]+$").WithMessage("El teléfono debe ser un número válido")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("El rol debe ser un rol de usuario válido");
    }
}

/// <summary>
/// Validator for UpdateUserRequest
/// </summary>
public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("El nombre es obligatorio")
            .MaximumLength(100).WithMessage("El nombre no debe exceder 100 caracteres");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Los apellidos son obligatorios")
            .MaximumLength(100).WithMessage("Los apellidos no deben exceder 100 caracteres");

        RuleFor(x => x.Phone)
            .MaximumLength(20).WithMessage("El teléfono no debe exceder 20 caracteres")
            .Matches(@"^\+?[0-9\s\-\(\)]+$").WithMessage("El teléfono debe ser un número válido")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
    }
}

/// <summary>
/// Validator for UpdateUserRoleRequest
/// </summary>
public class UpdateUserRoleRequestValidator : AbstractValidator<UpdateUserRoleRequest>
{
    public UpdateUserRoleRequestValidator()
    {
        RuleFor(x => x.NewRole)
            .IsInEnum()
            .WithMessage("Rol inválido especificado. Debe ser Admin, Board o Member.");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => x.Reason is not null)
            .WithMessage("La razón no debe exceder 500 caracteres.");
    }
}
