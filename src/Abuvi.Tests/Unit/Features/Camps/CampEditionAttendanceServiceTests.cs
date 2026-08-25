using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.MediaItems;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

/// <summary>
/// "Yo estuve en este campamento".
///
/// Two rules carry the weight: you may only declare for family members of your own unit,
/// and attendance derived from a registration is real but not deletable — it is computed
/// at read time and never stored.
/// </summary>
public class CampEditionAttendanceServiceTests
{
    private readonly ICampEditionAttendanceRepository _repository =
        Substitute.For<ICampEditionAttendanceRepository>();
    private readonly ICampEditionsRepository _editions = Substitute.For<ICampEditionsRepository>();
    private readonly IFamilyUnitsRepository _families = Substitute.For<IFamilyUnitsRepository>();
    private readonly IUsersRepository _users = Substitute.For<IUsersRepository>();
    private readonly IMediaItemsRepository _items = Substitute.For<IMediaItemsRepository>();
    private readonly CampEditionAttendanceService _service;

    private static readonly Guid EditionId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid FamilyUnitId = Guid.NewGuid();

    public CampEditionAttendanceServiceTests()
    {
        _service = new CampEditionAttendanceService(
            _repository, _editions, _families, _users, _items,
            Substitute.For<ILogger<CampEditionAttendanceService>>());

        _editions.GetByIdAsync(EditionId, Arg.Any<CancellationToken>())
            .Returns(Edition(1998, EditionId));
        _users.GetByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = UserId, FamilyUnitId = FamilyUnitId });
        _items.GetAlbumCountsAsync(Arg.Any<CancellationToken>()).Returns([]);
    }

    private static CampEdition Edition(int year, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        CampId = Guid.NewGuid(),
        Year = year,
        StartDate = new DateTime(year, 7, 1),
        EndDate = new DateTime(year, 7, 15),
        Camp = new Camp { Name = $"Sede {year}" }
    };

    // ── Declaring ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Declare_ForSelf_RecordsAttendance()
    {
        _repository.GetAsync(EditionId, UserId, null, Arg.Any<CancellationToken>())
            .Returns((CampEditionAttendance?)null);

        var declared = await _service.DeclareAsync(EditionId, UserId, null, CancellationToken.None);

        declared.Should().BeTrue();
        await _repository.Received(1).AddAsync(
            Arg.Any<CampEditionAttendance>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Declare_Twice_IsIdempotentAndDoesNotDuplicate()
    {
        _repository.GetAsync(EditionId, UserId, null, Arg.Any<CancellationToken>())
            .Returns(new CampEditionAttendance { Id = Guid.NewGuid() });

        var declared = await _service.DeclareAsync(EditionId, UserId, null, CancellationToken.None);

        declared.Should().BeTrue("a toggle that errors when pressed twice is worse than one that shrugs");
        await _repository.DidNotReceive().AddAsync(
            Arg.Any<CampEditionAttendance>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Declare_ForOwnFamilyMember_IsAllowed()
    {
        var memberId = Guid.NewGuid();
        _families.GetFamilyMemberByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(new FamilyMember { Id = memberId, FamilyUnitId = FamilyUnitId });
        _repository.GetAsync(EditionId, UserId, memberId, Arg.Any<CancellationToken>())
            .Returns((CampEditionAttendance?)null);

        var declared = await _service.DeclareAsync(
            EditionId, UserId, memberId, CancellationToken.None);

        declared.Should().BeTrue();
    }

    [Fact]
    public async Task Declare_ForSomeoneElsesFamilyMember_IsRefused()
    {
        var memberId = Guid.NewGuid();
        _families.GetFamilyMemberByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(new FamilyMember { Id = memberId, FamilyUnitId = Guid.NewGuid() });

        var declared = await _service.DeclareAsync(
            EditionId, UserId, memberId, CancellationToken.None);

        declared.Should().BeFalse();
        await _repository.DidNotReceive().AddAsync(
            Arg.Any<CampEditionAttendance>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Declare_ByAUserWithNoFamilyUnit_CannotDeclareForAnyMember()
    {
        var lonerId = Guid.NewGuid();
        _users.GetByIdAsync(lonerId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = lonerId, FamilyUnitId = null });

        var declared = await _service.DeclareAsync(
            EditionId, lonerId, Guid.NewGuid(), CancellationToken.None);

        declared.Should().BeFalse();
    }

    [Fact]
    public async Task Declare_ForAMissingEdition_Throws()
    {
        var missing = Guid.NewGuid();
        _editions.GetByIdAsync(missing, Arg.Any<CancellationToken>()).Returns((CampEdition?)null);

        var act = () => _service.DeclareAsync(missing, UserId, null, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Withdrawing ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Withdraw_ADeclaredAttendance_RemovesIt()
    {
        var attendance = new CampEditionAttendance { Id = Guid.NewGuid() };
        _repository.GetAsync(EditionId, UserId, null, Arg.Any<CancellationToken>())
            .Returns(attendance);

        await _service.WithdrawAsync(EditionId, UserId, null, CancellationToken.None);

        await _repository.Received(1).DeleteAsync(attendance, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Withdraw_DerivedAttendance_ThrowsValidationRatherThanNotFound()
    {
        // The member can see this attendance, so "not found" would be a confusing answer.
        _repository.GetAsync(EditionId, UserId, null, Arg.Any<CancellationToken>())
            .Returns((CampEditionAttendance?)null);
        _repository.GetRegisteredEditionIdsForUserAsync(UserId, Arg.Any<CancellationToken>())
            .Returns([EditionId]);

        var act = () => _service.WithdrawAsync(EditionId, UserId, null, CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>())
            .WithMessage("*inscripción*");
    }

    [Fact]
    public async Task Withdraw_WhenNeverDeclared_ThrowsNotFound()
    {
        _repository.GetAsync(EditionId, UserId, null, Arg.Any<CancellationToken>())
            .Returns((CampEditionAttendance?)null);
        _repository.GetRegisteredEditionIdsForUserAsync(UserId, Arg.Any<CancellationToken>())
            .Returns([]);

        var act = () => _service.WithdrawAsync(EditionId, UserId, null, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Timeline ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Timeline_ReturnsEveryEditionIncludingUnattendedOnes()
    {
        var attended = Edition(1998);
        var notAttended = Edition(2003);
        _editions.GetAllAsync(
                Arg.Any<int?>(), Arg.Any<CampEditionStatus?>(), Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns([attended, notAttended]);
        _repository.GetDeclaredEditionIdsForUserAsync(UserId, Arg.Any<CancellationToken>())
            .Returns([attended.Id]);
        _repository.GetRegisteredEditionIdsForUserAsync(UserId, Arg.Any<CancellationToken>())
            .Returns([]);

        var timeline = await _service.GetTimelineAsync(UserId, CancellationToken.None);

        timeline.Entries.Should().HaveCount(2, "the map paints all editions, attended or not");
        timeline.TotalEditionsAttended.Should().Be(1);
        timeline.Entries.Single(e => e.CampEditionId == attended.Id).AttendanceSource
            .Should().Be(CampEditionAttendanceService.SourceDeclared);
        timeline.Entries.Single(e => e.CampEditionId == notAttended.Id).AttendanceSource
            .Should().Be(CampEditionAttendanceService.SourceNone);
    }

    [Fact]
    public async Task Timeline_MarksRegistrationDerivedAttendanceAsSuch()
    {
        var edition = Edition(2019);
        _editions.GetAllAsync(
                Arg.Any<int?>(), Arg.Any<CampEditionStatus?>(), Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns([edition]);
        _repository.GetDeclaredEditionIdsForUserAsync(UserId, Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.GetRegisteredEditionIdsForUserAsync(UserId, Arg.Any<CancellationToken>())
            .Returns([edition.Id]);

        var timeline = await _service.GetTimelineAsync(UserId, CancellationToken.None);

        timeline.Entries[0].Attended.Should().BeTrue();
        timeline.Entries[0].AttendanceSource
            .Should().Be(CampEditionAttendanceService.SourceRegistration);
    }

    [Fact]
    public async Task Timeline_PrefersDeclaredOverDerivedWhenBothApply()
    {
        var edition = Edition(2019);
        _editions.GetAllAsync(
                Arg.Any<int?>(), Arg.Any<CampEditionStatus?>(), Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns([edition]);
        _repository.GetDeclaredEditionIdsForUserAsync(UserId, Arg.Any<CancellationToken>())
            .Returns([edition.Id]);
        _repository.GetRegisteredEditionIdsForUserAsync(UserId, Arg.Any<CancellationToken>())
            .Returns([edition.Id]);

        var timeline = await _service.GetTimelineAsync(UserId, CancellationToken.None);

        timeline.Entries[0].AttendanceSource
            .Should().Be(CampEditionAttendanceService.SourceDeclared);
        timeline.TotalEditionsAttended.Should().Be(1, "the same edition must not count twice");
    }

    [Fact]
    public async Task GetAttendedEditionIds_UnionsBothSourcesWithoutDuplicates()
    {
        var shared = Guid.NewGuid();
        var declaredOnly = Guid.NewGuid();
        var derivedOnly = Guid.NewGuid();

        _repository.GetDeclaredEditionIdsForUserAsync(UserId, Arg.Any<CancellationToken>())
            .Returns([shared, declaredOnly]);
        _repository.GetRegisteredEditionIdsForUserAsync(UserId, Arg.Any<CancellationToken>())
            .Returns([shared, derivedOnly]);

        var ids = await _service.GetAttendedEditionIdsAsync(UserId, CancellationToken.None);

        ids.Should().BeEquivalentTo([shared, declaredOnly, derivedOnly]);
    }
}
