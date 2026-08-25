using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.MediaComments;
using Abuvi.API.Features.MediaItems;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.MediaComments;

/// <summary>
/// Comments publish immediately on approved media; moderation happens afterwards. The
/// rules worth pinning down are the 15-minute author edit window and the fact that an
/// unapproved item is not open for comment to ordinary members.
/// </summary>
public class MediaCommentsServiceTests
{
    private readonly IMediaCommentsRepository _repository = Substitute.For<IMediaCommentsRepository>();
    private readonly IMediaItemsRepository _items = Substitute.For<IMediaItemsRepository>();
    private readonly MediaCommentsService _service;

    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid AuthorId = Guid.NewGuid();

    public MediaCommentsServiceTests()
    {
        _service = new MediaCommentsService(
            _repository, _items, Substitute.For<ILogger<MediaCommentsService>>());

        _repository.GetReportedCommentIdsForUserAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private MediaItem GivenItem(bool approved = true)
    {
        var item = new MediaItem { Id = ItemId, Title = "Foto", IsApproved = approved };
        _items.GetByIdAsync(ItemId, Arg.Any<CancellationToken>()).Returns(item);
        return item;
    }

    private MediaComment GivenComment(Guid? authorId = null, int ageMinutes = 0)
    {
        var comment = new MediaComment
        {
            Id = Guid.NewGuid(),
            MediaItemId = ItemId,
            AuthorUserId = authorId ?? AuthorId,
            Body = "Este es mi padre",
            CreatedAt = DateTime.UtcNow.AddMinutes(-ageMinutes),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-ageMinutes),
            Author = new User { FirstName = "Ana", LastName = "Socia" }
        };

        _repository.GetByIdAsync(comment.Id, Arg.Any<CancellationToken>()).Returns(comment);
        return comment;
    }

    // ── Creating ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_OnApprovedItem_Succeeds()
    {
        GivenItem(approved: true);
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => GivenCommentStub((Guid)ci[0]));

        var result = await _service.CreateAsync(
            ItemId, AuthorId, false, new CreateMediaCommentRequest("  Este es mi padre  "),
            CancellationToken.None);

        result.Should().NotBeNull();
        await _repository.Received(1).AddAsync(Arg.Any<MediaComment>(), Arg.Any<CancellationToken>());

        var added = (MediaComment)_repository.ReceivedCalls()
            .First(c => c.GetMethodInfo().Name == nameof(IMediaCommentsRepository.AddAsync))
            .GetArguments()[0]!;

        added.Body.Should().Be("Este es mi padre", "the body is trimmed on the way in");
    }

    [Fact]
    public async Task Create_OnUnapprovedItem_ByMember_IsRefused()
    {
        GivenItem(approved: false);

        var result = await _service.CreateAsync(
            ItemId, AuthorId, isAdminOrBoard: false,
            new CreateMediaCommentRequest("hola"), CancellationToken.None);

        result.Should().BeNull("unapproved media is not published yet");
        await _repository.DidNotReceive().AddAsync(
            Arg.Any<MediaComment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_OnUnapprovedItem_ByModerator_Succeeds()
    {
        GivenItem(approved: false);
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => GivenCommentStub((Guid)ci[0]));

        var result = await _service.CreateAsync(
            ItemId, AuthorId, isAdminOrBoard: true,
            new CreateMediaCommentRequest("nota interna"), CancellationToken.None);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_OnMissingItem_Throws()
    {
        _items.GetByIdAsync(ItemId, Arg.Any<CancellationToken>()).Returns((MediaItem?)null);

        var act = () => _service.CreateAsync(
            ItemId, AuthorId, false, new CreateMediaCommentRequest("hola"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── The 15-minute edit window ────────────────────────────────────────────

    [Fact]
    public async Task Update_JustInsideTheWindow_IsAllowed()
    {
        var comment = GivenComment(ageMinutes: 14);

        var result = await _service.UpdateAsync(
            comment.Id, AuthorId, new UpdateMediaCommentRequest("corregido"), CancellationToken.None);

        result.Should().NotBeNull();
        comment.Body.Should().Be("corregido");
    }

    [Fact]
    public async Task Update_AfterTheWindow_IsRefused()
    {
        var comment = GivenComment(ageMinutes: 16);

        var result = await _service.UpdateAsync(
            comment.Id, AuthorId, new UpdateMediaCommentRequest("corregido"), CancellationToken.None);

        result.Should().BeNull("the archive stays stable once the window closes");
        comment.Body.Should().Be("Este es mi padre");
    }

    [Fact]
    public async Task Update_ByAnotherMember_IsRefused()
    {
        var comment = GivenComment(ageMinutes: 1);

        var result = await _service.UpdateAsync(
            comment.Id, Guid.NewGuid(), new UpdateMediaCommentRequest("otro"), CancellationToken.None);

        result.Should().BeNull();
    }

    // ── Deleting ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ByAuthorInsideTheWindow_SoftDeletes()
    {
        var comment = GivenComment(ageMinutes: 5);

        var deleted = await _service.DeleteAsync(
            comment.Id, AuthorId, isAdminOrBoard: false, CancellationToken.None);

        deleted.Should().BeTrue();
        await _repository.Received(1).SoftDeleteAsync(comment, AuthorId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_ByAuthorAfterTheWindow_IsRefused()
    {
        var comment = GivenComment(ageMinutes: 20);

        var deleted = await _service.DeleteAsync(
            comment.Id, AuthorId, isAdminOrBoard: false, CancellationToken.None);

        deleted.Should().BeFalse();
        await _repository.DidNotReceive().SoftDeleteAsync(
            Arg.Any<MediaComment>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_ByModerator_IsAllowedAtAnyAge()
    {
        var comment = GivenComment(ageMinutes: 500);
        var moderator = Guid.NewGuid();

        var deleted = await _service.DeleteAsync(
            comment.Id, moderator, isAdminOrBoard: true, CancellationToken.None);

        deleted.Should().BeTrue();
        await _repository.Received(1).SoftDeleteAsync(comment, moderator, Arg.Any<CancellationToken>());
    }

    // ── Thread permissions ───────────────────────────────────────────────────

    [Fact]
    public async Task GetThread_MarksAuthorsOwnRecentCommentEditable()
    {
        GivenItem();
        var mine = GivenComment(AuthorId, ageMinutes: 2);
        var theirs = GivenComment(Guid.NewGuid(), ageMinutes: 2);
        _repository.GetThreadAsync(ItemId, Arg.Any<CancellationToken>()).Returns([mine, theirs]);

        var thread = await _service.GetThreadAsync(
            ItemId, AuthorId, isAdminOrBoard: false, CancellationToken.None);

        thread.Single(c => c.Id == mine.Id).CanEdit.Should().BeTrue();
        thread.Single(c => c.Id == mine.Id).CanDelete.Should().BeTrue();
        thread.Single(c => c.Id == theirs.Id).CanEdit.Should().BeFalse();
        thread.Single(c => c.Id == theirs.Id).CanDelete.Should().BeFalse();
    }

    [Fact]
    public async Task GetThread_ForModerator_MarksEverythingDeletableButNotEditable()
    {
        GivenItem();
        var theirs = GivenComment(Guid.NewGuid(), ageMinutes: 300);
        _repository.GetThreadAsync(ItemId, Arg.Any<CancellationToken>()).Returns([theirs]);

        var thread = await _service.GetThreadAsync(
            ItemId, Guid.NewGuid(), isAdminOrBoard: true, CancellationToken.None);

        thread[0].CanDelete.Should().BeTrue();
        thread[0].CanEdit.Should().BeFalse("moderators remove comments, they do not rewrite them");
    }

    [Fact]
    public async Task GetThread_ResolvesViewerReportsInOneBatchedLookup()
    {
        GivenItem();
        var a = GivenComment(Guid.NewGuid());
        var b = GivenComment(Guid.NewGuid());
        _repository.GetThreadAsync(ItemId, Arg.Any<CancellationToken>()).Returns([a, b]);
        _repository.GetReportedCommentIdsForUserAsync(
                AuthorId, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([a.Id]);

        var thread = await _service.GetThreadAsync(ItemId, AuthorId, false, CancellationToken.None);

        thread.Single(c => c.Id == a.Id).ViewerReported.Should().BeTrue();
        thread.Single(c => c.Id == b.Id).ViewerReported.Should().BeFalse();

        await _repository.Received(1).GetReportedCommentIdsForUserAsync(
            AuthorId, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
    }

    // ── Reporting ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Report_FirstTime_IsRecorded()
    {
        var comment = GivenComment();
        _repository.ReportExistsAsync(comment.Id, AuthorId, Arg.Any<CancellationToken>())
            .Returns(false);

        await _service.ReportAsync(
            comment.Id, AuthorId,
            new ReportMediaCommentRequest(MediaCommentReportReason.PrivacyConcern, "sale mi hija"),
            CancellationToken.None);

        await _repository.Received(1).AddReportAsync(
            Arg.Any<MediaCommentReport>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Report_Twice_ThrowsBusinessRule()
    {
        var comment = GivenComment();
        _repository.ReportExistsAsync(comment.Id, AuthorId, Arg.Any<CancellationToken>())
            .Returns(true);

        var act = () => _service.ReportAsync(
            comment.Id, AuthorId,
            new ReportMediaCommentRequest(MediaCommentReportReason.Offensive, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task ReviewReport_BackToPending_ThrowsValidation()
    {
        var report = new MediaCommentReport
        {
            Id = Guid.NewGuid(),
            MediaCommentId = Guid.NewGuid(),
            Status = MediaCommentReportStatus.Actioned
        };
        _repository.GetReportByIdAsync(report.Id, Arg.Any<CancellationToken>()).Returns(report);

        var act = () => _service.ReviewReportAsync(
            report.Id, Guid.NewGuid(), MediaCommentReportStatus.Pending, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    private static MediaComment GivenCommentStub(Guid id) => new()
    {
        Id = id,
        MediaItemId = ItemId,
        AuthorUserId = AuthorId,
        Body = "Este es mi padre",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        Author = new User { FirstName = "Ana", LastName = "Socia" }
    };
}
