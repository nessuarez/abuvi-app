using Abuvi.API.Features.Camps;
using FluentValidation;

namespace Abuvi.API.Features.Registrations;

public class CreateRegistrationValidator : AbstractValidator<CreateRegistrationRequest>
{
    public CreateRegistrationValidator(ICampEditionsRepository editionsRepo)
    {
        RuleFor(x => x.CampEditionId)
            .NotEmpty()
            .WithMessage("La edición del campamento es obligatoria")
            .MustAsync(async (id, ct) =>
            {
                var edition = await editionsRepo.GetByIdAsync(id, ct);
                return edition?.Status == CampEditionStatus.Open;
            })
            .WithMessage("La edición del campamento no está abierta para inscripción");

        RuleFor(x => x.FamilyUnitId)
            .NotEmpty()
            .WithMessage("La unidad familiar es obligatoria");

        RuleFor(x => x.MemberIds)
            .NotEmpty()
            .WithMessage("Debe seleccionar al menos un miembro de la familia")
            .Must(ids => ids != null && ids.Distinct().Count() == ids.Count)
            .WithMessage("No se puede incluir el mismo miembro dos veces");

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .WithMessage("Las notas no pueden superar los 1000 caracteres");
    }
}
