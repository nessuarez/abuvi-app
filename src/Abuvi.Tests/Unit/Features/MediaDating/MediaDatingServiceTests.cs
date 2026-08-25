using Abuvi.API.Features.Camps;
using Abuvi.API.Features.MediaDating;
using Abuvi.API.Features.MediaItems;
using Abuvi.API.Features.MediaSources;
using Abuvi.API.Features.MediaThemes;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.MediaDating;

/// <summary>
/// The consensus rule: a year wins at >=3 proposals AND >=66% share.
///
/// The cases that matter most are the ones that must NOT resolve — two agreeing votes, a
/// split field, an admin-set date — and the withdrawal path, which has to be able to
/// un-resolve an item. A vote that can only ever add is not a vote.
/// </summary>
public class MediaDatingServiceTests
{
    private readonly IMediaDatingRepository _proposals = Substitute.For<IMediaDatingRepository>();
    private readonly IMediaItemsRepository _items = Substitute.For<IMediaItemsRepository>();
    private readonly IMediaThemesRepository _themes = Substitute.For<IMediaThemesRepository>();
    private readonly ICampEditionsRepository _editions = Substitute.For<ICampEditionsRepository>();
    private readonly IMediaSourcesRepository _sources = Substitute.For<IMediaSourcesRepository>();
    private readonly MediaDatingService _service;

    private static readonly Guid ItemId = Guid.NewGuid();

    public MediaDatingServiceTests()
    {
        _service = new MediaDatingService(
            _proposals, _items, _themes, _editions, _sources,
            Substitute.For<ILogger<MediaDatingService>>());

        _themes.GetThemesForItemsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private MediaItem GivenItem(
        MediaItemYearSource yearSource = MediaItemYearSource.Unknown,
        int? year = null,
        Guid? editionId = null)
    {
        var item = new MediaItem
        {
            Id = ItemId,
            Title = "Foto sin datar",
            YearSource = yearSource,
            Year = year,
            CampEditionId = editionId
        };

        _items.GetByIdAsync(ItemId, Arg.Any<CancellationToken>()).Returns(item);
        return item;
    }

    private void GivenProposals(params (int Year, Guid? EditionId)[] proposals)
    {
        var list = proposals
            .Select(p => new MediaItemYearProposal
            {
                Id = Guid.NewGuid(),
                MediaItemId = ItemId,
                ProposedByUserId = Guid.NewGuid(),
                ProposedYear = p.Year,
                ProposedCampEditionId = p.EditionId,
                ProposedBy = new User { FirstName = "Socio", LastName = "Abuvi" }
            })
            .ToList();

        _proposals.GetForItemAsync(ItemId, Arg.Any<CancellationToken>()).Returns(list);
    }

    private static CampEdition Edition(int year, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        CampId = Guid.NewGuid(),
        Year = year,
        StartDate = new DateTime(year, 7, 1),
        EndDate = new DateTime(year, 7, 15)
    };

    /// <summary>Re-runs consensus through the public path, without adding a vote.</summary>
    private async Task WhenConsensusReevaluated(Guid voterId)
    {
        _proposals.GetByItemAndUserAsync(ItemId, voterId, Arg.Any<CancellationToken>())
            .Returns(new MediaItemYearProposal
            {
                Id = Guid.NewGuid(), MediaItemId = ItemId, ProposedByUserId = voterId,
                ProposedYear = 1998
            });

        await _service.WithdrawAsync(ItemId, voterId, isAdminOrBoard: false, CancellationToken.None);
    }

    // ── Reaching consensus ───────────────────────────────────────────────────

    [Fact]
    public async Task Consensus_AtExactlyThreeUnanimousProposals_Applies()
    {
        var item = GivenItem();
        var edition = Edition(1998);
        _editions.GetByYearAsync(1998, Arg.Any<CancellationToken>()).Returns([edition]);
        GivenProposals((1998, null), (1998, null), (1998, null));

        await _service.UpsertAsync(
            ItemId, Guid.NewGuid(), false,
            new UpsertYearProposalRequest(1998, null, null), CancellationToken.None);

        item.Year.Should().Be(1998);
        item.Decade.Should().Be("90s");
        item.CampEditionId.Should().Be(edition.Id);
        item.YearSource.Should().Be(MediaItemYearSource.Community);
    }

    [Fact]
    public async Task Consensus_WithTwoProposals_DoesNotApply()
    {
        var item = GivenItem();
        GivenProposals((1998, null), (1998, null));

        await _service.UpsertAsync(
            ItemId, Guid.NewGuid(), false,
            new UpsertYearProposalRequest(1998, null, null), CancellationToken.None);

        item.Year.Should().BeNull("two people agreeing is not yet a community answer");
        item.YearSource.Should().Be(MediaItemYearSource.Unknown);
    }

    [Fact]
    public async Task Consensus_WithThreeOfSixAgreeing_DoesNotApply()
    {
        // 50% clears the vote count but not the ratio: the field is genuinely split.
        var item = GivenItem();
        GivenProposals((1998, null), (1998, null), (1998, null),
                       (1999, null), (2000, null), (2001, null));

        await _service.UpsertAsync(
            ItemId, Guid.NewGuid(), false,
            new UpsertYearProposalRequest(1998, null, null), CancellationToken.None);

        item.Year.Should().BeNull();
    }

    [Fact]
    public async Task Consensus_WithFourOfFiveAgreeing_Applies()
    {
        var item = GivenItem();
        _editions.GetByYearAsync(1998, Arg.Any<CancellationToken>()).Returns([]);
        GivenProposals((1998, null), (1998, null), (1998, null), (1998, null), (1999, null));

        await _service.UpsertAsync(
            ItemId, Guid.NewGuid(), false,
            new UpsertYearProposalRequest(1998, null, null), CancellationToken.None);

        item.Year.Should().Be(1998);
    }

    [Fact]
    public async Task Consensus_PicksTheMostProposedEditionWithinTheWinningYear()
    {
        var item = GivenItem();
        var popularEdition = Edition(1998);
        var otherEdition = Edition(1998);

        _editions.GetByIdAsync(popularEdition.Id, Arg.Any<CancellationToken>())
            .Returns(popularEdition);

        var popular = popularEdition.Id;
        var other = otherEdition.Id;
        GivenProposals((1998, popular), (1998, popular), (1998, other));

        await _service.UpsertAsync(
            ItemId, Guid.NewGuid(), false,
            new UpsertYearProposalRequest(1998, popular, null), CancellationToken.None);

        item.CampEditionId.Should().Be(popular);
    }

    // ── Admin freeze ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Consensus_WhenYearSourceIsAdmin_NeverOverwrites()
    {
        var adminEdition = Guid.NewGuid();
        var item = GivenItem(MediaItemYearSource.Admin, year: 1977, editionId: adminEdition);
        GivenProposals((1998, null), (1998, null), (1998, null), (1998, null));

        await _service.UpsertAsync(
            ItemId, Guid.NewGuid(), false,
            new UpsertYearProposalRequest(1998, null, null), CancellationToken.None);

        item.Year.Should().Be(1977, "a human decision is not up for a vote");
        item.CampEditionId.Should().Be(adminEdition);
        item.YearSource.Should().Be(MediaItemYearSource.Admin);
    }

    [Fact]
    public async Task SetYearAsAdmin_FreezesTheItemAgainstConsensus()
    {
        var item = GivenItem();
        var edition = Edition(1977);
        _editions.GetByYearAsync(1977, Arg.Any<CancellationToken>()).Returns([edition]);
        GivenProposals();

        await _service.SetYearAsAdminAsync(
            ItemId, Guid.NewGuid(), new SetYearRequest(1977, null), CancellationToken.None);

        item.Year.Should().Be(1977);
        item.CampEditionId.Should().Be(edition.Id);
        item.YearSource.Should().Be(MediaItemYearSource.Admin);
    }

    // ── Withdrawal can un-resolve ────────────────────────────────────────────

    [Fact]
    public async Task Withdraw_DroppingBelowThreshold_ReturnsItemToTheUnplacedPile()
    {
        var voter = Guid.NewGuid();
        var item = GivenItem(MediaItemYearSource.Community, year: 1998, editionId: Guid.NewGuid());

        // After the withdrawal only two proposals remain — below the minimum.
        GivenProposals((1998, null), (1998, null));

        await WhenConsensusReevaluated(voter);

        item.Year.Should().BeNull();
        item.Decade.Should().BeNull();
        item.CampEditionId.Should().BeNull();
        item.YearSource.Should().Be(MediaItemYearSource.Unknown);
    }

    [Fact]
    public async Task Withdraw_LeavingNoProposals_ReturnsItemToTheUnplacedPile()
    {
        var voter = Guid.NewGuid();
        var item = GivenItem(MediaItemYearSource.Community, year: 1998);
        GivenProposals();

        await WhenConsensusReevaluated(voter);

        item.YearSource.Should().Be(MediaItemYearSource.Unknown);
    }

    [Fact]
    public async Task Withdraw_WhenConsensusStillHolds_KeepsTheDate()
    {
        var voter = Guid.NewGuid();
        var item = GivenItem(MediaItemYearSource.Community, year: 1998);
        _editions.GetByYearAsync(1998, Arg.Any<CancellationToken>()).Returns([]);
        GivenProposals((1998, null), (1998, null), (1998, null));

        await WhenConsensusReevaluated(voter);

        item.Year.Should().Be(1998);
        item.YearSource.Should().Be(MediaItemYearSource.Community);
    }

    [Fact]
    public async Task Withdraw_DoesNotUnresolveAnAdminDatedItem()
    {
        var voter = Guid.NewGuid();
        var item = GivenItem(MediaItemYearSource.Admin, year: 1977);
        GivenProposals();

        await WhenConsensusReevaluated(voter);

        item.Year.Should().Be(1977);
        item.YearSource.Should().Be(MediaItemYearSource.Admin);
    }

    // ── Upsert semantics ─────────────────────────────────────────────────────

    [Fact]
    public async Task Upsert_WhenUserAlreadyVoted_UpdatesInsteadOfAddingASecondVote()
    {
        GivenItem();
        GivenProposals((1998, null));

        var voter = Guid.NewGuid();
        var existing = new MediaItemYearProposal
        {
            Id = Guid.NewGuid(), MediaItemId = ItemId, ProposedByUserId = voter,
            ProposedYear = 1998
        };
        _proposals.GetByItemAndUserAsync(ItemId, voter, Arg.Any<CancellationToken>())
            .Returns(existing);

        await _service.UpsertAsync(
            ItemId, voter, false,
            new UpsertYearProposalRequest(2003, null, "me acordé mejor"), CancellationToken.None);

        await _proposals.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
        await _proposals.DidNotReceive().AddAsync(
            Arg.Any<MediaItemYearProposal>(), Arg.Any<CancellationToken>());

        existing.ProposedYear.Should().Be(2003);
        existing.Rationale.Should().Be("me acordé mejor");
    }

    [Fact]
    public async Task Upsert_WithEditionFromADifferentYear_ThrowsValidation()
    {
        GivenItem();
        var edition = Edition(2003);
        _editions.GetByIdAsync(edition.Id, Arg.Any<CancellationToken>()).Returns(edition);

        var act = () => _service.UpsertAsync(
            ItemId, Guid.NewGuid(), false,
            new UpsertYearProposalRequest(1998, edition.Id, null), CancellationToken.None);

        await act.Should().ThrowAsync<Abuvi.API.Common.Exceptions.ValidationException>();
    }

    // ── Tally ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTally_GroupsByYearOrderedByVoteCount()
    {
        GivenItem();
        GivenProposals((1998, null), (1998, null), (2003, null));

        var tally = await _service.GetTallyAsync(
            ItemId, Guid.NewGuid(), false, CancellationToken.None);

        tally.Groups.Should().HaveCount(2);
        tally.Groups[0].Year.Should().Be(1998);
        tally.Groups[0].Count.Should().Be(2);
        tally.Groups[1].Year.Should().Be(2003);
    }

    [Fact]
    public async Task GetTally_CapsProposerNamesAtFive()
    {
        GivenItem();
        GivenProposals(Enumerable.Repeat((1998, (Guid?)null), 8).ToArray());

        var tally = await _service.GetTallyAsync(
            ItemId, Guid.NewGuid(), false, CancellationToken.None);

        tally.Groups[0].Count.Should().Be(8);
        tally.Groups[0].ProposerNames.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetTally_TrimsSourcePathForMembersAndKeepsItForAdmins()
    {
        var sourceId = Guid.NewGuid();
        var item = GivenItem();
        item.MediaSourceId = sourceId;
        item.SourcePath = "D:/Users/maria.carmen/Fotos/Verano 98/Selva de Oza/img.jpg";

        _sources.GetByIdAsync(sourceId, Arg.Any<CancellationToken>())
            .Returns(new MediaSource { Id = sourceId, ContributorName = "Manolo García" });
        _proposals.GetYearsForSourceAsync(sourceId, Arg.Any<CancellationToken>()).Returns([1998]);
        GivenProposals();

        var asMember = await _service.GetTallyAsync(ItemId, Guid.NewGuid(), false, CancellationToken.None);
        var asAdmin = await _service.GetTallyAsync(ItemId, Guid.NewGuid(), true, CancellationToken.None);

        asMember.SourceHint!.SourcePathDisplay
            .Should().Be(".../Verano 98/Selva de Oza/img.jpg")
            .And.NotContain("maria.carmen");

        asAdmin.SourceHint!.SourcePathDisplay.Should().Be(item.SourcePath);
    }
}
