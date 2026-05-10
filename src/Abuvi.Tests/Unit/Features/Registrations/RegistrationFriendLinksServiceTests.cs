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

public class RegistrationFriendLinksServiceTests
{
    private readonly IRegistrationsRepository _repo;
    private readonly IRegistrationFriendLinksRepository _friendLinksRepo;
    private readonly RegistrationsService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RegistrationId = Guid.NewGuid();
    private static readonly Guid CampEditionId = Guid.NewGuid();
    private static readonly Guid LinkedRegistrationId = Guid.NewGuid();

    public RegistrationFriendLinksServiceTests()
    {
        _repo = Substitute.For<IRegistrationsRepository>();
        _friendLinksRepo = Substitute.For<IRegistrationFriendLinksRepository>();

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
        var needsRepo = Substitute.For<IRegistrationAccommodationNeedsRepository>();
        var featuresRepo = Substitute.For<IAccommodationFeaturesRepository>();
        var logger = Substitute.For<ILogger<RegistrationsService>>();

        _sut = new RegistrationsService(
            _repo, extrasRepo, accommodationPrefsRepo, familyUnitsRepo,
            editionsRepo, accommodationsRepo, extrasDefinitionRepo, pricingService, emailService,
            paymentsService, membershipsRepo, needsRepo, _friendLinksRepo, featuresRepo, logger);
    }

    // ── UpdateFriendLinksAsync — Successful Cases ────────────────────────────

    [Fact]
    public async Task UpdateFriendLinksAsync_WithValidLinks_CallsReplaceAndReturnsFriendLinks()
    {
        var registration = CreateRegistration(RegistrationId, CampEditionId);
        var linked = CreateRegistration(LinkedRegistrationId, CampEditionId);
        var familyUnit = CreateFamilyUnit("Martínez Family");
        linked.FamilyUnit = familyUnit;

        var savedLink = new RegistrationFriendLink
        {
            Id = Guid.NewGuid(),
            RegistrationId = RegistrationId,
            LinkedRegistrationId = LinkedRegistrationId,
            CreatedByUserId = UserId,
            CreatedAt = DateTime.UtcNow,
            LinkedRegistration = linked
        };

        _repo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>()).Returns(registration);
        _repo.GetByIdAsync(LinkedRegistrationId, Arg.Any<CancellationToken>()).Returns(linked);
        _friendLinksRepo.ReplaceAsync(RegistrationId, Arg.Any<IEnumerable<Guid>>(), UserId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _friendLinksRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(new List<RegistrationFriendLink> { savedLink });

        var request = new UpdateFriendLinksRequest([LinkedRegistrationId]);
        var result = await _sut.UpdateFriendLinksAsync(RegistrationId, UserId, request, CancellationToken.None);

        result.RegistrationId.Should().Be(RegistrationId);
        result.FriendLinks.Should().HaveCount(1);
        result.FriendLinks[0].LinkedRegistrationId.Should().Be(LinkedRegistrationId);
        result.FriendLinks[0].LinkedFamilyName.Should().Be("Martínez Family");

        await _friendLinksRepo.Received(1).ReplaceAsync(
            RegistrationId,
            Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(LinkedRegistrationId)),
            UserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateFriendLinksAsync_WithEmptyList_RemovesAllLinks()
    {
        var registration = CreateRegistration(RegistrationId, CampEditionId);

        _repo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>()).Returns(registration);
        _friendLinksRepo.ReplaceAsync(RegistrationId, Arg.Any<IEnumerable<Guid>>(), UserId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _friendLinksRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(new List<RegistrationFriendLink>());

        var request = new UpdateFriendLinksRequest([]);
        var result = await _sut.UpdateFriendLinksAsync(RegistrationId, UserId, request, CancellationToken.None);

        result.FriendLinks.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFriendLinksAsync_ReturnsLinksForRegistration()
    {
        var registration = CreateRegistration(RegistrationId, CampEditionId);
        var linked = CreateRegistration(LinkedRegistrationId, CampEditionId);
        linked.FamilyUnit = CreateFamilyUnit("González Family");

        var link = new RegistrationFriendLink
        {
            Id = Guid.NewGuid(),
            RegistrationId = RegistrationId,
            LinkedRegistrationId = LinkedRegistrationId,
            CreatedByUserId = UserId,
            CreatedAt = DateTime.UtcNow,
            LinkedRegistration = linked
        };

        _repo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>()).Returns(registration);
        _friendLinksRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(new List<RegistrationFriendLink> { link });

        var result = await _sut.GetFriendLinksAsync(RegistrationId, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].LinkedRegistrationId.Should().Be(LinkedRegistrationId);
        result[0].LinkedFamilyName.Should().Be("González Family");
    }

    // ── UpdateFriendLinksAsync — Business Rule Violations ───────────────────

    [Fact]
    public async Task UpdateFriendLinksAsync_WithSelfLink_ThrowsBusinessRuleException()
    {
        var registration = CreateRegistration(RegistrationId, CampEditionId);
        _repo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>()).Returns(registration);

        var request = new UpdateFriendLinksRequest([RegistrationId]);

        Func<Task> act = async () => await _sut.UpdateFriendLinksAsync(RegistrationId, UserId, request, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("NO_SELF_LINK*");
    }

    [Fact]
    public async Task UpdateFriendLinksAsync_WithDifferentEdition_ThrowsBusinessRuleException()
    {
        var registration = CreateRegistration(RegistrationId, CampEditionId);
        var differentEditionId = Guid.NewGuid();
        var linkedInOtherEdition = CreateRegistration(LinkedRegistrationId, differentEditionId);

        _repo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>()).Returns(registration);
        _repo.GetByIdAsync(LinkedRegistrationId, Arg.Any<CancellationToken>()).Returns(linkedInOtherEdition);

        var request = new UpdateFriendLinksRequest([LinkedRegistrationId]);

        Func<Task> act = async () => await _sut.UpdateFriendLinksAsync(RegistrationId, UserId, request, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("SAME_EDITION_REQUIRED*");
    }

    // ── UpdateFriendLinksAsync — Not Found ───────────────────────────────────

    [Fact]
    public async Task UpdateFriendLinksAsync_WithNonExistentRegistration_ThrowsNotFoundException()
    {
        _repo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>()).ReturnsNull();

        var request = new UpdateFriendLinksRequest([LinkedRegistrationId]);

        Func<Task> act = async () => await _sut.UpdateFriendLinksAsync(RegistrationId, UserId, request, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateFriendLinksAsync_WithNonExistentLinkedRegistration_ThrowsNotFoundException()
    {
        var registration = CreateRegistration(RegistrationId, CampEditionId);
        _repo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>()).Returns(registration);
        _repo.GetByIdAsync(LinkedRegistrationId, Arg.Any<CancellationToken>()).ReturnsNull();

        var request = new UpdateFriendLinksRequest([LinkedRegistrationId]);

        Func<Task> act = async () => await _sut.UpdateFriendLinksAsync(RegistrationId, UserId, request, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetFriendLinksAsync_WithNonExistentRegistration_ThrowsNotFoundException()
    {
        _repo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>()).ReturnsNull();

        Func<Task> act = async () => await _sut.GetFriendLinksAsync(RegistrationId, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Validator Tests ──────────────────────────────────────────────────────

    [Fact]
    public void UpdateFriendLinksValidator_WithMoreThan10Ids_FailsValidation()
    {
        var validator = new UpdateFriendLinksValidator();
        var ids = Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToList();
        var result = validator.Validate(new UpdateFriendLinksRequest(ids));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("10"));
    }

    [Fact]
    public void UpdateFriendLinksValidator_WithDuplicateIds_FailsValidation()
    {
        var validator = new UpdateFriendLinksValidator();
        var id = Guid.NewGuid();
        var result = validator.Validate(new UpdateFriendLinksRequest([id, id]));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("duplicados"));
    }

    [Fact]
    public void UpdateFriendLinksValidator_WithValidList_PassesValidation()
    {
        var validator = new UpdateFriendLinksValidator();
        var result = validator.Validate(new UpdateFriendLinksRequest([Guid.NewGuid(), Guid.NewGuid()]));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateFriendLinksValidator_WithEmptyList_PassesValidation()
    {
        var validator = new UpdateFriendLinksValidator();
        var result = validator.Validate(new UpdateFriendLinksRequest([]));
        result.IsValid.Should().BeTrue();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Registration CreateRegistration(Guid id, Guid campEditionId) => new()
    {
        Id = id,
        FamilyUnitId = Guid.NewGuid(),
        CampEditionId = campEditionId,
        RegisteredByUserId = UserId,
        Status = RegistrationStatus.Pending,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        FamilyUnit = new FamilyUnit { Id = Guid.NewGuid(), Name = "Test Family", RepresentativeUserId = UserId }
    };

    private static FamilyUnit CreateFamilyUnit(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        RepresentativeUserId = Guid.NewGuid()
    };
}
