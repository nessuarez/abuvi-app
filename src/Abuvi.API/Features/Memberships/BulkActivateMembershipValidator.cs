using FluentValidation;

namespace Abuvi.API.Features.Memberships;

public class BulkActivateMembershipValidator : AbstractValidator<BulkActivateMembershipRequest>
{
    public BulkActivateMembershipValidator()
    {
        RuleFor(x => x.Year)
            .GreaterThan(2000).WithMessage("El año de inicio no es válido")
            .LessThanOrEqualTo(DateTime.UtcNow.Year).WithMessage("El año de inicio no puede ser futuro");
    }
}
