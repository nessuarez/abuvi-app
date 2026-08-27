using FluentValidation;

namespace Abuvi.API.Features.MediaDating;

public class UpsertYearProposalRequestValidator : AbstractValidator<UpsertYearProposalRequest>
{
    /// <summary>ABUVI's founding year — nothing in the archive predates it.</summary>
    private const int FoundingYear = 1975;

    public UpsertYearProposalRequestValidator()
    {
        RuleFor(x => x.ProposedYear)
            .InclusiveBetween(FoundingYear, DateTime.UtcNow.Year)
            .WithMessage($"El año debe estar entre {FoundingYear} y el año actual");

        RuleFor(x => x.Rationale)
            .MaximumLength(500).WithMessage("La explicación no puede superar los 500 caracteres");
    }
}

public class SetYearRequestValidator : AbstractValidator<SetYearRequest>
{
    private const int FoundingYear = 1975;

    public SetYearRequestValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(FoundingYear, DateTime.UtcNow.Year)
            .WithMessage($"El año debe estar entre {FoundingYear} y el año actual");
    }
}
