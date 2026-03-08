using FluentValidation;

namespace Abuvi.API.Features.Payments;

public class RejectPaymentRequestValidator : AbstractValidator<RejectPaymentRequest>
{
    public RejectPaymentRequestValidator()
    {
        RuleFor(x => x.Notes)
            .NotEmpty().WithMessage("Las notas de rechazo son obligatorias.")
            .MinimumLength(10).WithMessage("Las notas de rechazo deben tener al menos 10 caracteres.");
    }
}

public class PaymentSettingsRequestValidator : AbstractValidator<PaymentSettingsRequest>
{
    public PaymentSettingsRequestValidator()
    {
        RuleFor(x => x.Iban)
            .NotEmpty().WithMessage("El IBAN es obligatorio.")
            .Matches(@"^ES\d{22}$").WithMessage("El IBAN debe tener el formato ES seguido de 22 dígitos.");

        RuleFor(x => x.BankName)
            .NotEmpty().WithMessage("El nombre del banco es obligatorio.")
            .MaximumLength(200);

        RuleFor(x => x.AccountHolder)
            .NotEmpty().WithMessage("El titular de la cuenta es obligatorio.")
            .MaximumLength(200);

        RuleFor(x => x.SecondInstallmentDaysBefore)
            .InclusiveBetween(1, 90)
            .WithMessage("Los días de antelación deben estar entre 1 y 90.");

        RuleFor(x => x.TransferConceptPrefix)
            .NotEmpty().WithMessage("El prefijo del concepto es obligatorio.")
            .MaximumLength(20)
            .Matches(@"^[A-Z0-9\-]+$").WithMessage("El prefijo solo puede contener letras mayúsculas, números y guiones.");
    }
}

public class PaymentFilterRequestValidator : AbstractValidator<PaymentFilterRequest>
{
    public PaymentFilterRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
