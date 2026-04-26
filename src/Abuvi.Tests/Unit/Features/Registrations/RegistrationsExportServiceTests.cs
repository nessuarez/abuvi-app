using System.Text;
using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Services;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Memberships;
using Abuvi.API.Features.Payments;
using Abuvi.API.Features.Registrations;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Abuvi.Tests.Unit.Features.Registrations;

public class RegistrationsExportServiceTests
{
    private readonly IRegistrationsRepository _repo;
    private readonly ICampEditionExtrasRepository _extrasDefinitionRepo;
    private readonly ICampEditionsRepository _editionsRepo;
    private readonly RegistrationsService _sut;

    private static readonly Guid CampEditionId = Guid.NewGuid();

    public RegistrationsExportServiceTests()
    {
        _repo = Substitute.For<IRegistrationsRepository>();
        _extrasDefinitionRepo = Substitute.For<ICampEditionExtrasRepository>();
        var extrasRepo = Substitute.For<IRegistrationExtrasRepository>();
        var accommodationPrefsRepo = Substitute.For<IRegistrationAccommodationPreferencesRepository>();
        var familyUnitsRepo = Substitute.For<IFamilyUnitsRepository>();
        _editionsRepo = Substitute.For<ICampEditionsRepository>();
        var accommodationsRepo = Substitute.For<ICampEditionAccommodationsRepository>();
        var emailService = Substitute.For<IEmailService>();
        var paymentsService = Substitute.For<IPaymentsService>();
        var logger = Substitute.For<ILogger<RegistrationsService>>();
        var settingsRepo = Substitute.For<IAssociationSettingsRepository>();
        var membershipsRepo = Substitute.For<IMembershipsRepository>();
        membershipsRepo.HasPaidCurrentYearFeeForFamilyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var pricingService = new RegistrationPricingService(settingsRepo);

        _sut = new RegistrationsService(
            _repo, extrasRepo, accommodationPrefsRepo, familyUnitsRepo,
            _editionsRepo, accommodationsRepo, _extrasDefinitionRepo,
            pricingService, emailService, paymentsService, membershipsRepo, logger);
    }

    [Fact]
    public async Task ExportToCsvAsync_WhenEditionNotFound_ThrowsNotFoundException()
    {
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>())
            .Returns((CampEdition?)null);

        var act = () => _sut.ExportToCsvAsync(
            CampEditionId, null, null, null, null, null, null, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExportToCsvAsync_WhenNoRegistrations_ReturnsHeaderOnlyFile()
    {
        SetupEdition();
        _extrasDefinitionRepo
            .GetByCampEditionAsync(CampEditionId, activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(new List<CampEditionExtra>());
        _repo.GetAllForExportAsync(
                CampEditionId, null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<Registration>());

        var (content, fileName) = await _sut.ExportToCsvAsync(
            CampEditionId, null, null, null, null, null, null, CancellationToken.None);

        var text = Encoding.UTF8.GetString(content).TrimStart('﻿');
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(1, "only the header row should be present");
        lines[0].Should().Contain("ID Inscripción");
        lines[0].Should().Contain("Familia");
        fileName.Should().StartWith("inscripciones-").And.EndWith(".csv");
    }

    [Fact]
    public async Task ExportToCsvAsync_WithRegistrations_IncludesDynamicExtraColumns()
    {
        SetupEdition();
        var extra = BuildExtra("Kayak", requiresUserInput: false);
        var extraWithInput = BuildExtra("Camiseta", requiresUserInput: true);
        _extrasDefinitionRepo
            .GetByCampEditionAsync(CampEditionId, activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(new List<CampEditionExtra> { extra, extraWithInput });

        var registration = BuildRegistration();
        registration.Extras = new List<RegistrationExtra>
        {
            new() { CampEditionExtraId = extra.Id, Quantity = 2, CampEditionExtra = extra },
            new() { CampEditionExtraId = extraWithInput.Id, Quantity = 1, UserInput = "M", CampEditionExtra = extraWithInput }
        };
        _repo.GetAllForExportAsync(
                CampEditionId, null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<Registration> { registration });

        var (content, _) = await _sut.ExportToCsvAsync(
            CampEditionId, null, null, null, null, null, null, CancellationToken.None);

        var text = Encoding.UTF8.GetString(content).TrimStart('﻿');
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2, "header + 1 data row");
        lines[0].Should().Contain("Kayak");
        lines[0].Should().Contain("Camiseta");
        lines[0].Should().Contain("Camiseta - Detalle");
        lines[1].Should().Contain(",2,");
        lines[1].Should().Contain(",1,");
        lines[1].Should().Contain(",M");
    }

    [Fact]
    public async Task ExportToCsvAsync_WhenRegistrationHasDangerousValue_EscapesCsvInjection()
    {
        SetupEdition();
        _extrasDefinitionRepo
            .GetByCampEditionAsync(CampEditionId, activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(new List<CampEditionExtra>());

        var registration = BuildRegistration();
        registration.Notes = "=HYPERLINK(\"evil.com\",\"click\")";
        _repo.GetAllForExportAsync(
                CampEditionId, null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<Registration> { registration });

        var (content, _) = await _sut.ExportToCsvAsync(
            CampEditionId, null, null, null, null, null, null, CancellationToken.None);

        var text = Encoding.UTF8.GetString(content).TrimStart('﻿');
        text.Should().NotContain(",=HYPERLINK");
        text.Should().Contain(" =HYPERLINK");
    }

    [Fact]
    public async Task ExportToCsvAsync_WithExtrasFilter_PassesFilterToRepository()
    {
        SetupEdition();
        var extraId = Guid.NewGuid();
        _extrasDefinitionRepo
            .GetByCampEditionAsync(CampEditionId, activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(new List<CampEditionExtra>());
        _repo.GetAllForExportAsync(
                CampEditionId, null, null, null, Arg.Any<IReadOnlyList<Guid>>(), null, null, Arg.Any<CancellationToken>())
            .Returns(new List<Registration>());

        await _sut.ExportToCsvAsync(
            CampEditionId, null, null, null, new[] { extraId }, null, null, CancellationToken.None);

        await _repo.Received(1).GetAllForExportAsync(
            CampEditionId, null, null, null,
            Arg.Is<IReadOnlyList<Guid>>(ids => ids.Contains(extraId)),
            null, null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportToCsvAsync_WithAccommodationPreferenceFilter_PassesFilterToRepository()
    {
        SetupEdition();
        var accommodationId = Guid.NewGuid();
        var filter = new AccommodationPreferenceFilter(accommodationId, 1);
        _extrasDefinitionRepo
            .GetByCampEditionAsync(CampEditionId, activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(new List<CampEditionExtra>());
        _repo.GetAllForExportAsync(
                CampEditionId, null, null,
                Arg.Any<IReadOnlyList<AccommodationPreferenceFilter>>(),
                null, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<Registration>());

        await _sut.ExportToCsvAsync(
            CampEditionId, null, null,
            new[] { filter }, null, null, null, CancellationToken.None);

        await _repo.Received(1).GetAllForExportAsync(
            CampEditionId, null, null,
            Arg.Is<IReadOnlyList<AccommodationPreferenceFilter>>(f =>
                f.Count == 1 && f[0].AccommodationId == accommodationId && f[0].PreferenceOrder == 1),
            null, null, null,
            Arg.Any<CancellationToken>());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetupEdition()
    {
        var camp = new Camp { Id = Guid.NewGuid(), Name = "Campamento Abuvi" };
        var edition = new CampEdition
        {
            Id = CampEditionId,
            Year = 2026,
            Camp = camp,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 14)
        };
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
    }

    private static CampEditionExtra BuildExtra(string name, bool requiresUserInput) =>
        new() { Id = Guid.NewGuid(), Name = name, RequiresUserInput = requiresUserInput, SortOrder = 0 };

    private static Registration BuildRegistration() => new()
    {
        Id = Guid.NewGuid(),
        FamilyUnit = new FamilyUnit { Id = Guid.NewGuid(), Name = "Familia Test" },
        RegisteredByUser = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Juan",
            LastName = "García",
            Email = "juan@example.com",
            Phone = "600000000"
        },
        Status = RegistrationStatus.Confirmed,
        BaseTotalAmount = 500m,
        ExtrasAmount = 50m,
        TotalAmount = 550m,
        Members = [],
        Extras = [],
        AccommodationPreferences = [],
        Payments = [],
        CreatedAt = DateTime.UtcNow
    };
}
