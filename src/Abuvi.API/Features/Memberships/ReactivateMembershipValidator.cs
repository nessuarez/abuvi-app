using FluentValidation;

namespace Abuvi.API.Features.Memberships;

public class ReactivateMembershipValidator : AbstractValidator<ReactivateMembershipRequest>
{
    public ReactivateMembershipValidator()
    {
        RuleFor(x => x.Year)
            .GreaterThan(2000)
            .LessThanOrEqualTo(_ => DateTime.UtcNow.Year)
            .WithMessage("Year must be between 2001 and the current year.");
    }
}
