using FluentValidation;

namespace Abuvi.API.Features.Memberships;

public class UpdateMemberNumberValidator : AbstractValidator<UpdateMemberNumberRequest>
{
    public UpdateMemberNumberValidator()
    {
        RuleFor(x => x.MemberNumber)
            .GreaterThan(0).WithMessage("El número de socio/a debe ser mayor a 0");
    }
}
