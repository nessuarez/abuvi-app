using FluentValidation;

namespace Abuvi.API.Features.Memberships;

public class PayFeeValidator : AbstractValidator<PayFeeRequest>
{
    public PayFeeValidator()
    {
        RuleFor(x => x.PaidDate)
            .NotEmpty().WithMessage("La fecha de pago es obligatoria")
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("La fecha de pago no puede ser futura");

        RuleFor(x => x.PaymentReference)
            .MaximumLength(100).WithMessage("La referencia de pago no puede exceder 100 caracteres");
    }
}
