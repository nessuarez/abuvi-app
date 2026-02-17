using FluentValidation;

namespace Abuvi.API.Features.Memberships;

public class CreateMembershipValidator : AbstractValidator<CreateMembershipRequest>
{
    public CreateMembershipValidator()
    {
        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("La fecha de inicio es obligatoria")
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("La fecha de inicio no puede ser futura");
    }
}
