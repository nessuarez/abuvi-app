using FluentValidation;

namespace Abuvi.API.Features.Camps;

public class CreateAccommodationZoneRequestValidator
    : AbstractValidator<CreateAccommodationZoneRequest>
{
    public CreateAccommodationZoneRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la zona es obligatorio")
            .MaximumLength(100).WithMessage("El nombre de la zona no puede superar 100 caracteres");

        RuleFor(x => x.MaxCapacity)
            .GreaterThan(0).When(x => x.MaxCapacity.HasValue)
            .WithMessage("La capacidad máxima debe ser mayor que cero");

        RuleFor(x => x.DistributionNotes)
            .MaximumLength(500).When(x => x.DistributionNotes is not null)
            .WithMessage("Las notas de distribución no pueden superar 500 caracteres");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0)
            .WithMessage("El orden de visualización debe ser mayor o igual a cero");
    }
}

public class UpdateAccommodationZoneRequestValidator
    : AbstractValidator<UpdateAccommodationZoneRequest>
{
    public UpdateAccommodationZoneRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la zona es obligatorio")
            .MaximumLength(100).WithMessage("El nombre de la zona no puede superar 100 caracteres");

        RuleFor(x => x.MaxCapacity)
            .GreaterThan(0).When(x => x.MaxCapacity.HasValue)
            .WithMessage("La capacidad máxima debe ser mayor que cero");

        RuleFor(x => x.DistributionNotes)
            .MaximumLength(500).When(x => x.DistributionNotes is not null)
            .WithMessage("Las notas de distribución no pueden superar 500 caracteres");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0)
            .WithMessage("El orden de visualización debe ser mayor o igual a cero");
    }
}

public class CreateAccommodationAssignmentProposalRequestValidator
    : AbstractValidator<CreateAccommodationAssignmentProposalRequest>
{
    public CreateAccommodationAssignmentProposalRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la propuesta es obligatorio")
            .MaximumLength(100).WithMessage("El nombre no puede superar 100 caracteres");

        RuleFor(x => x.Notes)
            .MaximumLength(500).When(x => x.Notes is not null)
            .WithMessage("Las notas no pueden superar 500 caracteres");
    }
}

public class UpdateAccommodationAssignmentProposalRequestValidator
    : AbstractValidator<UpdateAccommodationAssignmentProposalRequest>
{
    public UpdateAccommodationAssignmentProposalRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la propuesta es obligatorio")
            .MaximumLength(100).WithMessage("El nombre no puede superar 100 caracteres");

        RuleFor(x => x.Notes)
            .MaximumLength(500).When(x => x.Notes is not null)
            .WithMessage("Las notas no pueden superar 500 caracteres");
    }
}
