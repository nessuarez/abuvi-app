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
using NSubstitute.ReturnsExtensions;

namespace Abuvi.Tests.Unit.Features.Registrations;

public class RegistrationAccommodationNeedsServiceTests
{
    private readonly IRegistrationsRepository _repo;
    private readonly IRegistrationAccommodationNeedsRepository _needsRepo;
    private readonly IAccommodationFeaturesRepository _featuresRepo;
    private readonly RegistrationsService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RegistrationId = Guid.NewGuid();
    private static readonly Guid Feature1Id = Guid.NewGuid();
    private static readonly Guid Feature2Id = Guid.NewGuid();

    public RegistrationAccommodationNeedsServiceTests()
    {
        _repo = Substitute.For<IRegistrationsRepository>();
        _needsRepo = Substitute.For<IRegistrationAccommodationNeedsRepository>();
        _featuresRepo = Substitute.For<IAccommodationFeaturesRepository>();

        var extrasRepo = Substitute.For<IRegistrationExtrasRepository>();
        var accommodationPrefsRepo = Substitute.For<IRegistrationAccommodationPreferencesRepository>();
        var familyUnitsRepo = Substitute.For<IFamilyUnitsRepository>();
        var editionsRepo = Substitute.For<ICampEditionsRepository>();
        var accommodationsRepo = Substitute.For<ICampEditionAccommodationsRepository>();
        var extrasDefinitionRepo = Substitute.For<ICampEditionExtrasRepository>();
        var settingsRepo = Substitute.For<IAssociationSettingsRepository>();
        var pricingService = new RegistrationPricingService(settingsRepo);
        var emailService = Substitute.For<IEmailService>();
        var paymentsService = Substitute.For<IPaymentsService>();
        var membershipsRepo = Substitute.For<IMembershipsRepository>();
        var friendLinksRepo = Substitute.For<IRegistrationFriendLinksRepository>();
        var logger = Substitute.For<ILogger<RegistrationsService>>();

        _sut = new RegistrationsService(
            _repo, extrasRepo, accommodationPrefsRepo, familyUnitsRepo,
            editionsRepo, accommodationsRepo, extrasDefinitionRepo, pricingService, emailService,
            paymentsService, membershipsRepo, _needsRepo, friendLinksRepo, _featuresRepo, logger);
    }

    // ── UpdateAccommodationNeedsAsync — Successful Cases ─────────────────────

    [Fact]
    public async Task UpdateAccommodationNeedsAsync_WithValidFeatureIds_ReturnsPopulatedResponse()
    {
        var registration = CreateRegistration();
        var features = new List<AccommodationFeature>
        {
            CreateFeature(Feature1Id, "Habitación privada"),
            CreateFeature(Feature2Id, "Acceso adaptado")
        };
        var saved = new List<RegistrationAccommodationNeed>
        {
            CreateNeed(Feature1Id, UserId, features[0]),
            CreateNeed(Feature2Id, UserId, features[1])
        };

        _repo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>()).Returns(registration);
        _featuresRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(features.AsReadOnly());
        _needsRepo.ReplaceAsync(RegistrationId, Arg.Any<IEnumerable<RegistrationAccommodationNeed>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _needsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(saved);

        var request = new UpdateAccommodationNeedsRequest([Feature1Id, Feature2Id]);
        var result = await _sut.UpdateAccommodationNeedsAsync(RegistrationId, UserId, request, CancellationToken.None);

        result.RegistrationId.Should().Be(RegistrationId);
        result.Needs.Should().HaveCount(2);
        result.Needs[0].FeatureId.Should().Be(Feature1Id);
        result.Needs[0].FeatureName.Should().Be("Habitación privada");
        result.Needs[1].FeatureId.Should().Be(Feature2Id);
    }

    [Fact]
    public async Task UpdateAccommodationNeedsAsync_WithEmptyList_ClearsAllNeedsAndReturnsEmpty()
    {
        var registration = CreateRegistration();
        _repo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>()).Returns(registration);
        _needsRepo.ReplaceAsync(RegistrationId, Arg.Any<IEnumerable<RegistrationAccommodationNeed>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _needsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(new List<RegistrationAccommodationNeed>());

        var request = new UpdateAccommodationNeedsRequest([]);
        var result = await _sut.UpdateAccommodationNeedsAsync(RegistrationId, UserId, request, CancellationToken.None);

        result.Needs.Should().BeEmpty();
        await _needsRepo.Received(1).ReplaceAsync(RegistrationId, Arg.Any<IEnumerable<RegistrationAccommodationNeed>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAccommodationNeedsAsync_ReturnsNeedsForRegistration()
    {
        var registration = CreateRegistration();
        var feature = CreateFeature(Feature1Id, "Cuna disponible");
        var needs = new List<RegistrationAccommodationNeed> { CreateNeed(Feature1Id, UserId, feature) };

        _repo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>()).Returns(registration);
        _needsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>()).Returns(needs);

        var result = await _sut.GetAccommodationNeedsAsync(RegistrationId, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].FeatureId.Should().Be(Feature1Id);
        result[0].FeatureName.Should().Be("Cuna disponible");
    }

    // ── UpdateAccommodationNeedsAsync — Validation Errors ───────────────────

    [Fact]
    public async Task UpdateAccommodationNeedsAsync_WithNonExistentFeatureId_ThrowsValidationException()
    {
        var registration = CreateRegistration();
        var existingFeatures = new List<AccommodationFeature> { CreateFeature(Feature1Id, "X") };

        _repo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>()).Returns(registration);
        _featuresRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(existingFeatures.AsReadOnly());

        var unknownId = Guid.NewGuid();
        var request = new UpdateAccommodationNeedsRequest([Feature1Id, unknownId]);

        Func<Task> act = async () => await _sut.UpdateAccommodationNeedsAsync(RegistrationId, UserId, request, CancellationToken.None);

        await act.Should().ThrowAsync<Abuvi.API.Common.Exceptions.ValidationException>()
            .WithMessage("*catálogo*");
    }

    // ── UpdateAccommodationNeedsAsync — Not Found ────────────────────────────

    [Fact]
    public async Task UpdateAccommodationNeedsAsync_WithNonExistentRegistration_ThrowsNotFoundException()
    {
        _repo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>()).ReturnsNull();

        var request = new UpdateAccommodationNeedsRequest([Feature1Id]);

        Func<Task> act = async () => await _sut.UpdateAccommodationNeedsAsync(RegistrationId, UserId, request, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetAccommodationNeedsAsync_WithNonExistentRegistration_ThrowsNotFoundException()
    {
        _repo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>()).ReturnsNull();

        Func<Task> act = async () => await _sut.GetAccommodationNeedsAsync(RegistrationId, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── UpdateAccommodationNotesAsync — Successful Cases ────────────────────

    [Fact]
    public async Task UpdateAccommodationNotesAsync_SetsNotesCorrectly()
    {
        var registration = CreateRegistration();
        _repo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>()).Returns(registration);
        _repo.UpdateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        const string notes = "La familia necesita habitaciones contiguas.";
        var request = new UpdateAccommodationNotesRequest(notes);
        var result = await _sut.UpdateAccommodationNotesAsync(RegistrationId, request, CancellationToken.None);

        result.RegistrationId.Should().Be(RegistrationId);
        result.AccommodationInternalNotes.Should().Be(notes);
        await _repo.Received(1).UpdateAsync(
            Arg.Is<Registration>(r => r.AccommodationInternalNotes == notes),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAccommodationNotesAsync_WithEmptyString_SetsNullNotes()
    {
        var registration = CreateRegistration();
        _repo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>()).Returns(registration);
        _repo.UpdateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var request = new UpdateAccommodationNotesRequest(string.Empty);
        var result = await _sut.UpdateAccommodationNotesAsync(RegistrationId, request, CancellationToken.None);

        result.AccommodationInternalNotes.Should().BeNull();
        await _repo.Received(1).UpdateAsync(
            Arg.Is<Registration>(r => r.AccommodationInternalNotes == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAccommodationNotesAsync_WithNull_SetsNullNotes()
    {
        var registration = CreateRegistration();
        _repo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>()).Returns(registration);
        _repo.UpdateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var request = new UpdateAccommodationNotesRequest(null);
        var result = await _sut.UpdateAccommodationNotesAsync(RegistrationId, request, CancellationToken.None);

        result.AccommodationInternalNotes.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAccommodationNotesAsync_WithNonExistentRegistration_ThrowsNotFoundException()
    {
        _repo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>()).ReturnsNull();

        Func<Task> act = async () =>
            await _sut.UpdateAccommodationNotesAsync(RegistrationId, new UpdateAccommodationNotesRequest("notes"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Validator Tests ──────────────────────────────────────────────────────

    [Fact]
    public void UpdateAccommodationNeedsValidator_WithMoreThan20Ids_FailsValidation()
    {
        var validator = new UpdateAccommodationNeedsValidator();
        var ids = Enumerable.Range(0, 21).Select(_ => Guid.NewGuid()).ToList();
        var result = validator.Validate(new UpdateAccommodationNeedsRequest(ids));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("20"));
    }

    [Fact]
    public void UpdateAccommodationNeedsValidator_WithDuplicateIds_FailsValidation()
    {
        var validator = new UpdateAccommodationNeedsValidator();
        var id = Guid.NewGuid();
        var result = validator.Validate(new UpdateAccommodationNeedsRequest([id, id]));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("duplicados"));
    }

    [Fact]
    public void UpdateAccommodationNotesValidator_ExceededLength_FailsValidation()
    {
        var validator = new UpdateAccommodationNotesValidator();
        var longText = new string('x', 4001);
        var result = validator.Validate(new UpdateAccommodationNotesRequest(longText));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("4000"));
    }

    [Fact]
    public void UpdateAccommodationNotesValidator_Within4000Chars_PassesValidation()
    {
        var validator = new UpdateAccommodationNotesValidator();
        var text = new string('x', 4000);
        var result = validator.Validate(new UpdateAccommodationNotesRequest(text));
        result.IsValid.Should().BeTrue();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Registration CreateRegistration() => new()
    {
        Id = RegistrationId,
        FamilyUnitId = Guid.NewGuid(),
        CampEditionId = Guid.NewGuid(),
        RegisteredByUserId = UserId,
        Status = RegistrationStatus.Pending,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static AccommodationFeature CreateFeature(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        Icon = "icon",
        ApplicabilityLevel = FeatureApplicabilityLevel.Any,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static RegistrationAccommodationNeed CreateNeed(
        Guid featureId, Guid taggedByUserId, AccommodationFeature feature) => new()
    {
        Id = Guid.NewGuid(),
        RegistrationId = RegistrationId,
        AccommodationFeatureId = featureId,
        TaggedByUserId = taggedByUserId,
        CreatedAt = DateTime.UtcNow,
        AccommodationFeature = feature
    };
}
