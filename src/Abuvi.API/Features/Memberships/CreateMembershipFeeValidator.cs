using FluentValidation;

namespace Abuvi.API.Features.Memberships;

public class CreateMembershipFeeValidator : AbstractValidator<CreateMembershipFeeRequest>
{
    public CreateMembershipFeeValidator()
    {
        RuleFor(x => x.Year)
            .GreaterThan(2000)
            .LessThanOrEqualTo(_ => DateTime.UtcNow.Year)
            .WithMessage("Year must be between 2001 and the current year.");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Amount must be >= 0.");
    }
}
