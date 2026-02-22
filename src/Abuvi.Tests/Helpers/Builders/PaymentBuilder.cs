using Abuvi.API.Features.Registrations;

namespace Abuvi.Tests.Helpers.Builders;

public class PaymentBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _registrationId = Guid.NewGuid();
    private decimal _amount = 100m;
    private DateTime _paymentDate = DateTime.UtcNow;
    private PaymentMethod _method = PaymentMethod.Transfer;
    private PaymentStatus _status = PaymentStatus.Completed;
    private string? _externalReference = null;

    public PaymentBuilder WithId(Guid id) { _id = id; return this; }
    public PaymentBuilder WithRegistrationId(Guid id) { _registrationId = id; return this; }
    public PaymentBuilder WithAmount(decimal amount) { _amount = amount; return this; }
    public PaymentBuilder WithPaymentDate(DateTime date) { _paymentDate = date; return this; }
    public PaymentBuilder WithMethod(PaymentMethod method) { _method = method; return this; }
    public PaymentBuilder WithStatus(PaymentStatus status) { _status = status; return this; }
    public PaymentBuilder WithExternalReference(string? reference) { _externalReference = reference; return this; }

    public Payment Build() => new()
    {
        Id = _id,
        RegistrationId = _registrationId,
        Amount = _amount,
        PaymentDate = _paymentDate,
        Method = _method,
        Status = _status,
        ExternalReference = _externalReference,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
