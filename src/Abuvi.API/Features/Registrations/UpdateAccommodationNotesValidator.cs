using FluentValidation;

namespace Abuvi.API.Features.Registrations;

public class UpdateAccommodationNotesValidator : AbstractValidator<UpdateAccommodationNotesRequest>
{
    public UpdateAccommodationNotesValidator()
    {
        RuleFor(x => x.AccommodationInternalNotes)
            .MaximumLength(4000)
            .WithMessage("Las notas internas no pueden superar los 4000 caracteres");
    }
}
