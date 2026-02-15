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

/// <summary>
/// Validator for ProposeCampEditionRequest
/// </summary>
public class ProposeCampEditionRequestValidator : AbstractValidator<ProposeCampEditionRequest>
{
    public ProposeCampEditionRequestValidator()
    {
        RuleFor(x => x.CampId)
            .NotEmpty().WithMessage("Camp ID is required");

        RuleFor(x => x.Year)
            .GreaterThanOrEqualTo(2000).WithMessage("Year must be 2000 or later")
            .LessThanOrEqualTo(2100).WithMessage("Year must be 2100 or earlier");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("End date is required")
            .GreaterThan(x => x.StartDate).WithMessage("End date must be after start date");

        // Optional prices - if provided, must be >= 0
        RuleFor(x => x.PricePerAdult)
            .GreaterThanOrEqualTo(0).WithMessage("Price per adult must be greater than or equal to 0")
            .When(x => x.PricePerAdult.HasValue);

        RuleFor(x => x.PricePerChild)
            .GreaterThanOrEqualTo(0).WithMessage("Price per child must be greater than or equal to 0")
            .When(x => x.PricePerChild.HasValue);

        RuleFor(x => x.PricePerBaby)
            .GreaterThanOrEqualTo(0).WithMessage("Price per baby must be greater than or equal to 0")
            .When(x => x.PricePerBaby.HasValue);

        // Optional max capacity - if provided, must be > 0
        RuleFor(x => x.MaxCapacity)
            .GreaterThan(0).WithMessage("Max capacity must be greater than 0")
            .When(x => x.MaxCapacity.HasValue);

        // Optional notes - max length check
        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));

        // Custom age ranges validation - if UseCustomAgeRanges is true, all custom age fields are required
        When(x => x.UseCustomAgeRanges, () =>
        {
            RuleFor(x => x.CustomBabyMaxAge)
                .NotNull().WithMessage("Custom baby max age is required when using custom age ranges")
                .GreaterThanOrEqualTo(0).WithMessage("Custom baby max age must be greater than or equal to 0");

            RuleFor(x => x.CustomChildMinAge)
                .NotNull().WithMessage("Custom child min age is required when using custom age ranges")
                .GreaterThanOrEqualTo(0).WithMessage("Custom child min age must be greater than or equal to 0");

            RuleFor(x => x.CustomChildMaxAge)
                .NotNull().WithMessage("Custom child max age is required when using custom age ranges")
                .GreaterThanOrEqualTo(0).WithMessage("Custom child max age must be greater than or equal to 0");

            RuleFor(x => x.CustomAdultMinAge)
                .NotNull().WithMessage("Custom adult min age is required when using custom age ranges")
                .GreaterThanOrEqualTo(0).WithMessage("Custom adult min age must be greater than or equal to 0");

            // Validate age range logical consistency
            RuleFor(x => x)
                .Must(x => x.CustomBabyMaxAge!.Value < x.CustomChildMinAge!.Value)
                .WithMessage("Custom baby max age must be less than custom child min age")
                .WithName("CustomBabyMaxAge");

            RuleFor(x => x)
                .Must(x => x.CustomChildMaxAge!.Value < x.CustomAdultMinAge!.Value)
                .WithMessage("Custom child max age must be less than custom adult min age")
                .WithName("CustomChildMaxAge");
        });
    }
}
