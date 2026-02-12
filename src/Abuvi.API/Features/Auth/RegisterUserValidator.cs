namespace Abuvi.API.Features.Auth;

using FluentValidation;

/// <summary>
/// Validator for user registration requests
/// </summary>
public class RegisterUserValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"\d").WithMessage("Password must contain at least one digit")
            .Matches(@"[@$!%*?&#]").WithMessage("Password must contain at least one special character");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100);

        RuleFor(x => x.DocumentNumber)
            .MaximumLength(50)
            .Matches(@"^[A-Z0-9]+$").When(x => !string.IsNullOrEmpty(x.DocumentNumber))
            .WithMessage("Document number must contain only uppercase letters and numbers");

        RuleFor(x => x.Phone)
            .Matches(@"^\+?[1-9]\d{1,14}$").When(x => !string.IsNullOrEmpty(x.Phone))
            .WithMessage("Invalid phone number format (E.164)");

        RuleFor(x => x.AcceptedTerms)
            .Equal(true).WithMessage("You must accept the terms and conditions");
    }
}
