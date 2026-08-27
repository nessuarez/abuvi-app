using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.MediaSources;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.MediaSources;

/// <summary>
/// Provenance, and the two privacy rules that go with it.
///
/// ContributorContact and SourcePath both carry personal data about people who may not be
/// members and never agreed to be listed. Both are filtered server-side, in the mapper —
/// the frontend is not a security boundary — so these are the tests that actually hold
/// that guarantee.
/// </summary>
public class MediaSourcesServiceTests
{
    private readonly IMediaSourcesRepository _repository = Substitute.For<IMediaSourcesRepository>();
    private readonly IUsersRepository _users = Substitute.For<IUsersRepository>();
    private readonly MediaSourcesService _service;

    public MediaSourcesServiceTests()
    {
        _service = new MediaSourcesService(
            _repository, _users, Substitute.For<ILogger<MediaSourcesService>>());
    }

    private MediaSource GivenSource(
        Guid? id = null,
        string name = "Manolo García",
        string? contact = "manolo@example.com",
        Guid? registeredBy = null)
    {
        var source = new MediaSource
        {
            Id = id ?? Guid.NewGuid(),
            ContributorName = name,
            ContributorContact = contact,
            RegisteredByUserId = registeredBy ?? Guid.NewGuid(),
            RegisteredBy = new User { FirstName = "Ana", LastName = "Socia" }
        };

        _repository.GetByIdAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        _repository.GetStatsAsync(
                Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, MediaSourceStats>());

        return source;
    }

    // ── SourcePath trimming ──────────────────────────────────────────────────

    [Theory]
    [InlineData("D:/Users/maria.carmen/Fotos/Verano 98/Selva de Oza/img.jpg",
                ".../Verano 98/Selva de Oza/img.jpg")]
    [InlineData("a/b/c/d/e.jpg", ".../c/d/e.jpg")]
    [InlineData("Verano 98/Selva/img.jpg", "Verano 98/Selva/img.jpg")]
    [InlineData("img.jpg", "img.jpg")]
    public void TrimSourcePath_ForMember_KeepsOnlyTheTrailingSegments(string input, string expected)
    {
        MediaSourcesService.TrimSourcePath(input, isAdminOrBoard: false).Should().Be(expected);
    }

    [Fact]
    public void TrimSourcePath_ForMember_DropsTheDonorsHomeDirectory()
    {
        var trimmed = MediaSourcesService.TrimSourcePath(
            "C:/Users/maria.carmen.lopez/Fotos privadas/1998/foto.jpg", isAdminOrBoard: false);

        trimmed.Should().NotContain("maria.carmen.lopez");
        trimmed.Should().NotContain("Users");
    }

    [Fact]
    public void TrimSourcePath_NormalisesWindowsBackslashes()
    {
        MediaSourcesService.TrimSourcePath(@"D:\Fotos\Verano 98\Selva\img.jpg", false)
            .Should().Be(".../Verano 98/Selva/img.jpg");
    }

    [Fact]
    public void TrimSourcePath_ForAdmin_ReturnsTheFullPath()
    {
        const string path = "D:/Users/maria.carmen/Fotos/Verano 98/Selva de Oza/img.jpg";
        MediaSourcesService.TrimSourcePath(path, isAdminOrBoard: true).Should().Be(path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TrimSourcePath_WithNoPath_ReturnsNull(string? path)
    {
        MediaSourcesService.TrimSourcePath(path, false).Should().BeNull();
        MediaSourcesService.TrimSourcePath(path, true).Should().BeNull();
    }

    // ── Contact visibility ───────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ForMember_StripsContributorContact()
    {
        var source = GivenSource(contact: "manolo@example.com");

        var result = await _service.GetByIdAsync(source.Id, isAdminOrBoard: false, CancellationToken.None);

        result.ContributorContact.Should().BeNull(
            "contact details belong to someone who never agreed to be listed to the association");
        result.ContributorName.Should().Be("Manolo García", "attribution is the point of the feature");
    }

    [Fact]
    public async Task GetById_ForAdmin_ReturnsContributorContact()
    {
        var source = GivenSource(contact: "manolo@example.com");

        var result = await _service.GetByIdAsync(source.Id, isAdminOrBoard: true, CancellationToken.None);

        result.ContributorContact.Should().Be("manolo@example.com");
    }

    [Fact]
    public async Task GetList_ForMember_StripsContactFromEveryRow()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new MediaSource { Id = Guid.NewGuid(), ContributorName = "A", ContributorContact = "a@x.com" },
            new MediaSource { Id = Guid.NewGuid(), ContributorName = "B", ContributorContact = "b@x.com" }
        ]);
        _repository.GetStatsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, MediaSourceStats>());

        var result = await _service.GetListAsync(isAdminOrBoard: false, CancellationToken.None);

        result.Should().OnlyContain(r => r.ContributorContact == null);
    }

    // ── Merge ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Merge_DelegatesToTheRepositoryAndReturnsRowsMoved()
    {
        var from = GivenSource();
        var to = GivenSource();
        _repository.MergeAsync(from.Id, to.Id, Arg.Any<CancellationToken>()).Returns(800);

        var moved = await _service.MergeAsync(from.Id, to.Id, CancellationToken.None);

        moved.Should().Be(800);
        await _repository.Received(1).MergeAsync(from.Id, to.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Merge_IntoItself_ThrowsValidation()
    {
        var source = GivenSource();

        var act = () => _service.MergeAsync(source.Id, source.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        await _repository.DidNotReceive().MergeAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Merge_WithUnknownTarget_ThrowsAndDoesNotTouchAnything()
    {
        var from = GivenSource();
        var missing = Guid.NewGuid();
        _repository.GetByIdAsync(missing, Arg.Any<CancellationToken>()).Returns((MediaSource?)null);

        var act = () => _service.MergeAsync(from.Id, missing, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        await _repository.DidNotReceive().MergeAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ── RGPD erasure ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Anonymise_ClearsIdentifyingFieldsButKeepsTheRow()
    {
        var source = GivenSource(contact: "manolo@example.com");
        source.ContributorUserId = Guid.NewGuid();

        await _service.AnonymiseAsync(source.Id, CancellationToken.None);

        source.ContributorName.Should().Be("(anónimo)");
        source.ContributorContact.Should().BeNull();
        source.ContributorUserId.Should().BeNull();

        await _repository.Received(1).UpdateAsync(source, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().DeleteAsync(
            Arg.Any<MediaSource>(), Arg.Any<CancellationToken>());
    }

    // ── Edit permissions ─────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ByTheRegistrar_IsAllowed()
    {
        var registrar = Guid.NewGuid();
        var source = GivenSource(registeredBy: registrar);

        var result = await _service.UpdateAsync(
            source.Id, registrar, isAdminOrBoard: false,
            new UpdateMediaSourceRequest("Manuel García", null, null, null, null),
            CancellationToken.None);

        result.Should().NotBeNull();
        source.ContributorName.Should().Be("Manuel García");
    }

    [Fact]
    public async Task Update_ByAnUnrelatedMember_IsRefused()
    {
        var source = GivenSource(registeredBy: Guid.NewGuid());

        var result = await _service.UpdateAsync(
            source.Id, Guid.NewGuid(), isAdminOrBoard: false,
            new UpdateMediaSourceRequest("Otro nombre", null, null, null, null),
            CancellationToken.None);

        result.Should().BeNull("only Admin/Board or the registrar may edit a contributor");
        await _repository.DidNotReceive().UpdateAsync(
            Arg.Any<MediaSource>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_ByAdmin_IsAllowedEvenWhenNotTheRegistrar()
    {
        var source = GivenSource(registeredBy: Guid.NewGuid());

        var result = await _service.UpdateAsync(
            source.Id, Guid.NewGuid(), isAdminOrBoard: true,
            new UpdateMediaSourceRequest("Manuel García", null, null, null, null),
            CancellationToken.None);

        result.Should().NotBeNull();
    }
}
