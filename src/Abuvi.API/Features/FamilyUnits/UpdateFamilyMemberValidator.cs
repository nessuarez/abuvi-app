namespace Abuvi.API.Features.FamilyUnits;

using FluentValidation;

public class UpdateFamilyMemberValidator : AbstractValidator<UpdateFamilyMemberRequest>
{
    public UpdateFamilyMemberValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("El nombre es obligatorio")
            .MaximumLength(100)
            .WithMessage("El nombre no puede exceder 100 caracteres");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Los apellidos son obligatorios")
            .MaximumLength(100)
            .WithMessage("Los apellidos no pueden exceder 100 caracteres");

        RuleFor(x => x.DateOfBirth)
            .NotEmpty()
            .WithMessage("La fecha de nacimiento es obligatoria")
            .LessThan(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("La fecha de nacimiento debe ser una fecha pasada");

        RuleFor(x => x.Relationship)
            .IsInEnum()
            .WithMessage("Tipo de relación inválido");

        When(x => !string.IsNullOrEmpty(x.DocumentNumber), () =>
        {
            RuleFor(x => x.DocumentNumber)
                .MaximumLength(50)
                .WithMessage("El número de documento no puede exceder 50 caracteres")
                .Matches("^[A-Z0-9]+$")
                .WithMessage("El número de documento debe contener solo letras mayúsculas y números");
        });

        When(x => !string.IsNullOrEmpty(x.Email), () =>
        {
            RuleFor(x => x.Email)
                .EmailAddress()
                .WithMessage("Formato de correo electrónico inválido")
                .MaximumLength(255)
                .WithMessage("El correo electrónico no puede exceder 255 caracteres");
        });

        When(x => !string.IsNullOrEmpty(x.Phone), () =>
        {
            RuleFor(x => x.Phone)
                .Matches(@"^\+[1-9]\d{1,14}$")
                .WithMessage("El teléfono debe estar en formato E.164 (ej. +34612345678)")
                .MaximumLength(20)
                .WithMessage("El teléfono no puede exceder 20 caracteres");
        });

        When(x => !string.IsNullOrEmpty(x.MedicalNotes), () =>
        {
            RuleFor(x => x.MedicalNotes)
                .MaximumLength(2000)
                .WithMessage("Las notas médicas no pueden exceder 2000 caracteres");
        });

        When(x => !string.IsNullOrEmpty(x.Allergies), () =>
        {
            RuleFor(x => x.Allergies)
                .MaximumLength(1000)
                .WithMessage("Las alergias no pueden exceder 1000 caracteres");
        });
    }
}
