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

        // Optional proposal metadata — no required constraint, only length guard
        RuleFor(x => x.ProposalReason)
            .MaximumLength(1000).WithMessage("Proposal reason must not exceed 1000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.ProposalReason));

        RuleFor(x => x.ProposalNotes)
            .MaximumLength(2000).WithMessage("Proposal notes must not exceed 2000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.ProposalNotes));
        // Week pricing: all three prices required together
        RuleFor(x => x)
            .Must(x => (x.PricePerAdultWeek.HasValue && x.PricePerChildWeek.HasValue && x.PricePerBabyWeek.HasValue)
                    || (!x.PricePerAdultWeek.HasValue && !x.PricePerChildWeek.HasValue && !x.PricePerBabyWeek.HasValue))
            .WithMessage("Si se configura precio semanal, los tres precios (adulto, niño, bebé) son obligatorios")
            .WithName("PricePerAdultWeek")
            .When(x => x.PricePerAdultWeek.HasValue || x.PricePerChildWeek.HasValue || x.PricePerBabyWeek.HasValue);

        // Weekend visit: dates and prices required together
        RuleFor(x => x)
            .Must(x => x.WeekendStartDate.HasValue && x.WeekendEndDate.HasValue
                    && x.PricePerAdultWeekend.HasValue && x.PricePerChildWeekend.HasValue && x.PricePerBabyWeekend.HasValue)
            .WithMessage("Para visitas de fin de semana se deben especificar fechas (inicio y fin) y los tres precios")
            .WithName("WeekendStartDate")
            .When(x => x.WeekendStartDate.HasValue || x.WeekendEndDate.HasValue
                    || x.PricePerAdultWeekend.HasValue || x.PricePerChildWeekend.HasValue || x.PricePerBabyWeekend.HasValue);

        RuleFor(x => x.WeekendEndDate)
            .GreaterThan(x => x.WeekendStartDate)
            .WithMessage("La fecha de fin del fin de semana debe ser posterior a la fecha de inicio")
            .When(x => x.WeekendStartDate.HasValue && x.WeekendEndDate.HasValue);

        RuleFor(x => x.MaxWeekendCapacity)
            .GreaterThan(0).WithMessage("La capacidad máxima de fin de semana debe ser mayor a 0")
            .When(x => x.MaxWeekendCapacity.HasValue);

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

/// <summary>
/// Validator for UpdateCampEditionRequest
/// </summary>
public class UpdateCampEditionRequestValidator : AbstractValidator<UpdateCampEditionRequest>
{
    public UpdateCampEditionRequestValidator()
    {
        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("La fecha de inicio es obligatoria");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("La fecha de fin es obligatoria")
            .GreaterThan(x => x.StartDate).WithMessage("La fecha de fin debe ser posterior a la fecha de inicio");

        RuleFor(x => x.PricePerAdult)
            .GreaterThanOrEqualTo(0).WithMessage("El precio por adulto debe ser mayor o igual a 0");

        RuleFor(x => x.PricePerChild)
            .GreaterThanOrEqualTo(0).WithMessage("El precio por niño debe ser mayor o igual a 0");

        RuleFor(x => x.PricePerBaby)
            .GreaterThanOrEqualTo(0).WithMessage("El precio por bebé debe ser mayor o igual a 0");

        RuleFor(x => x.MaxCapacity)
            .GreaterThan(0).WithMessage("La capacidad máxima debe ser mayor a 0")
            .When(x => x.MaxCapacity.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Las notas no deben superar los 2000 caracteres")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));

        When(x => x.UseCustomAgeRanges, () =>
        {
            RuleFor(x => x.CustomBabyMaxAge)
                .NotNull().WithMessage("La edad máxima de bebé es obligatoria con rangos personalizados");

            RuleFor(x => x.CustomChildMinAge)
                .NotNull().WithMessage("La edad mínima de niño es obligatoria con rangos personalizados");

            RuleFor(x => x.CustomChildMaxAge)
                .NotNull().WithMessage("La edad máxima de niño es obligatoria con rangos personalizados");

            RuleFor(x => x.CustomAdultMinAge)
                .NotNull().WithMessage("La edad mínima de adulto es obligatoria con rangos personalizados");

            RuleFor(x => x)
                .Must(x => x.CustomBabyMaxAge!.Value < x.CustomChildMinAge!.Value)
                .When(x => x.CustomBabyMaxAge.HasValue && x.CustomChildMinAge.HasValue)
                .WithMessage("La edad máxima de bebé debe ser menor a la edad mínima de niño")
                .WithName("CustomBabyMaxAge");

            RuleFor(x => x)
                .Must(x => x.CustomChildMaxAge!.Value < x.CustomAdultMinAge!.Value)
                .When(x => x.CustomChildMaxAge.HasValue && x.CustomAdultMinAge.HasValue)
                .WithMessage("La edad máxima de niño debe ser menor a la edad mínima de adulto")
                .WithName("CustomChildMaxAge");
        });

        // Week pricing: all three prices required together
        RuleFor(x => x)
            .Must(x => (x.PricePerAdultWeek.HasValue && x.PricePerChildWeek.HasValue && x.PricePerBabyWeek.HasValue)
                    || (!x.PricePerAdultWeek.HasValue && !x.PricePerChildWeek.HasValue && !x.PricePerBabyWeek.HasValue))
            .WithMessage("Si se configura precio semanal, los tres precios (adulto, niño, bebé) son obligatorios")
            .WithName("PricePerAdultWeek")
            .When(x => x.PricePerAdultWeek.HasValue || x.PricePerChildWeek.HasValue || x.PricePerBabyWeek.HasValue);

        // Weekend visit: dates and prices required together
        RuleFor(x => x)
            .Must(x => x.WeekendStartDate.HasValue && x.WeekendEndDate.HasValue
                    && x.PricePerAdultWeekend.HasValue && x.PricePerChildWeekend.HasValue && x.PricePerBabyWeekend.HasValue)
            .WithMessage("Para visitas de fin de semana se deben especificar fechas (inicio y fin) y los tres precios")
            .WithName("WeekendStartDate")
            .When(x => x.WeekendStartDate.HasValue || x.WeekendEndDate.HasValue
                    || x.PricePerAdultWeekend.HasValue || x.PricePerChildWeekend.HasValue || x.PricePerBabyWeekend.HasValue);

        RuleFor(x => x.WeekendEndDate)
            .GreaterThan(x => x.WeekendStartDate)
            .WithMessage("La fecha de fin del fin de semana debe ser posterior a la fecha de inicio")
            .When(x => x.WeekendStartDate.HasValue && x.WeekendEndDate.HasValue);

        RuleFor(x => x.MaxWeekendCapacity)
            .GreaterThan(0).WithMessage("La capacidad máxima de fin de semana debe ser mayor a 0")
            .When(x => x.MaxWeekendCapacity.HasValue);
    }
}

/// <summary>
/// Validator for ChangeEditionStatusRequest
/// </summary>
public class ChangeEditionStatusRequestValidator : AbstractValidator<ChangeEditionStatusRequest>
{
    public ChangeEditionStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("El estado proporcionado no es válido");
    }
}

/// <summary>
/// Validator for AddCampPhotoRequest
/// </summary>
public class AddCampPhotoRequestValidator : AbstractValidator<AddCampPhotoRequest>
{
    public AddCampPhotoRequestValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("Photo URL is required")
            .MaximumLength(1000).WithMessage("Photo URL must not exceed 1000 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Display order must be greater than or equal to 0");
    }
}

/// <summary>
/// Validator for UpdateCampPhotoRequest
/// </summary>
public class UpdateCampPhotoRequestValidator : AbstractValidator<UpdateCampPhotoRequest>
{
    public UpdateCampPhotoRequestValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("Photo URL is required")
            .MaximumLength(1000).WithMessage("Photo URL must not exceed 1000 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Display order must be greater than or equal to 0");
    }
}

/// <summary>
/// Validator for ReorderCampPhotosRequest
/// </summary>
public class ReorderCampPhotosRequestValidator : AbstractValidator<ReorderCampPhotosRequest>
{
    public ReorderCampPhotosRequestValidator()
    {
        RuleFor(x => x.Photos)
            .NotEmpty().WithMessage("Photos list must not be empty");

        RuleForEach(x => x.Photos).ChildRules(photo =>
        {
            photo.RuleFor(p => p.Id)
                .NotEmpty().WithMessage("Photo ID is required");

            photo.RuleFor(p => p.DisplayOrder)
                .GreaterThanOrEqualTo(0).WithMessage("Display order must be greater than or equal to 0");
        });
    }
}
