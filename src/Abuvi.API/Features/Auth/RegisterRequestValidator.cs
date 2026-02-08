using FluentValidation;

namespace Abuvi.API.Features.Auth;

/// <summary>
/// Validator for registration requests with strong password requirements
/// </summary>
public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255)
            .WithMessage("A valid email address (max 255 characters) is required");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters long")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one number");

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("First name is required (max 100 characters)");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Last name is required (max 100 characters)");

        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .When(x => x.Phone != null)
            .WithMessage("Phone number must not exceed 20 characters");
    }
}
