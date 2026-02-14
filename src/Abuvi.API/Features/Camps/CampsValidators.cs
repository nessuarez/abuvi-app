using FluentValidation;

namespace Abuvi.API.Features.Camps;

/// <summary>
/// Validator for CreateCampRequest
/// </summary>
public class CreateCampRequestValidator : AbstractValidator<CreateCampRequest>
{
    public CreateCampRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Camp name is required")
            .MaximumLength(200).WithMessage("Camp name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.Location)
            .MaximumLength(500).WithMessage("Location must not exceed 500 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Location));

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90).WithMessage("Latitude must be between -90 and 90")
            .When(x => x.Latitude.HasValue);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180).WithMessage("Longitude must be between -180 and 180")
            .When(x => x.Longitude.HasValue);

        RuleFor(x => x.PricePerAdult)
            .GreaterThanOrEqualTo(0).WithMessage("Price per adult must be greater than or equal to 0");

        RuleFor(x => x.PricePerChild)
            .GreaterThanOrEqualTo(0).WithMessage("Price per child must be greater than or equal to 0");

        RuleFor(x => x.PricePerBaby)
            .GreaterThanOrEqualTo(0).WithMessage("Price per baby must be greater than or equal to 0");
    }
}

/// <summary>
/// Validator for UpdateCampRequest
/// </summary>
public class UpdateCampRequestValidator : AbstractValidator<UpdateCampRequest>
{
    public UpdateCampRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Camp name is required")
            .MaximumLength(200).WithMessage("Camp name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.Location)
            .MaximumLength(500).WithMessage("Location must not exceed 500 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Location));

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90).WithMessage("Latitude must be between -90 and 90")
            .When(x => x.Latitude.HasValue);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180).WithMessage("Longitude must be between -180 and 180")
            .When(x => x.Longitude.HasValue);

        RuleFor(x => x.PricePerAdult)
            .GreaterThanOrEqualTo(0).WithMessage("Price per adult must be greater than or equal to 0");

        RuleFor(x => x.PricePerChild)
            .GreaterThanOrEqualTo(0).WithMessage("Price per child must be greater than or equal to 0");

        RuleFor(x => x.PricePerBaby)
            .GreaterThanOrEqualTo(0).WithMessage("Price per baby must be greater than or equal to 0");
    }
}

/// <summary>
/// Validator for UpdateAgeRangesRequest
/// </summary>
public class UpdateAgeRangesRequestValidator : AbstractValidator<UpdateAgeRangesRequest>
{
    public UpdateAgeRangesRequestValidator()
    {
        RuleFor(x => x.BabyMaxAge)
            .GreaterThanOrEqualTo(0).WithMessage("Baby max age must be greater than or equal to 0");

        RuleFor(x => x.ChildMinAge)
            .GreaterThanOrEqualTo(0).WithMessage("Child min age must be greater than or equal to 0");

        RuleFor(x => x.ChildMaxAge)
            .GreaterThanOrEqualTo(0).WithMessage("Child max age must be greater than or equal to 0");

        RuleFor(x => x.AdultMinAge)
            .GreaterThanOrEqualTo(0).WithMessage("Adult min age must be greater than or equal to 0");

        // Validate that baby max age is less than child min age
        RuleFor(x => x)
            .Must(x => x.BabyMaxAge < x.ChildMinAge)
            .WithMessage("Baby max age must be less than child min age")
            .WithName("BabyMaxAge");

        // Validate that child max age is less than adult min age
        RuleFor(x => x)
            .Must(x => x.ChildMaxAge < x.AdultMinAge)
            .WithMessage("Child max age must be less than adult min age")
            .WithName("ChildMaxAge");
    }
}
