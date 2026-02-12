using FluentValidation;

namespace Abuvi.API.Features.Auth;

/// <summary>
/// Validator for resend verification email requests
/// </summary>
public class ResendVerificationValidator : AbstractValidator<ResendVerificationRequest>
{
    public ResendVerificationValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Email must be a valid email address");
    }
}
