using Abuvi.API.Features.Camps;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

/// <summary>
/// Unit tests for CampEditionsService - Proposal Workflow
/// Following TDD: Tests written FIRST before implementation
/// </summary>
public class CampEditionsServiceTests
{
    private readonly ICampEditionsRepository _repository;
    private readonly ICampsRepository _campsRepository;
    private readonly CampEditionsService _sut;

    public CampEditionsServiceTests()
    {
        _repository = Substitute.For<ICampEditionsRepository>();
        _campsRepository = Substitute.For<ICampsRepository>();
        _sut = new CampEditionsService(_repository, _campsRepository);
    }

    #region ProposeAsync Tests

    [Fact]
    public async Task ProposeAsync_WithValidData_CreatesProposedEdition()
    {
        // Arrange
        var camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = "Test Camp",
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            IsActive = true
        };

        _campsRepository.GetByIdAsync(camp.Id, Arg.Any<CancellationToken>())
            .Returns(camp);

        var request = new ProposeCampEditionRequest(
            CampId: camp.Id,
            Year: 2026,
            StartDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate: new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult: null,
            PricePerChild: null,
            PricePerBaby: null,
            UseCustomAgeRanges: false,
            CustomBabyMaxAge: null,
            CustomChildMinAge: null,
            CustomChildMaxAge: null,
            CustomAdultMinAge: null,
            MaxCapacity: 100,
            Notes: "Summer 2026 proposal"
        );

        _repository.CreateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<CampEdition>());

        // Act
        var result = await _sut.ProposeAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(CampEditionStatus.Proposed);
        result.CampId.Should().Be(camp.Id);
        result.Year.Should().Be(2026);
        result.PricePerAdult.Should().Be(180m); // Inherited from camp
        result.PricePerChild.Should().Be(120m);
        result.PricePerBaby.Should().Be(60m);

        await _repository.Received(1).CreateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProposeAsync_WithCustomPrices_UsesProvidedPrices()
    {
        // Arrange
        var camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = "Test Camp",
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            IsActive = true
        };

        _campsRepository.GetByIdAsync(camp.Id, Arg.Any<CancellationToken>())
            .Returns(camp);

        var request = new ProposeCampEditionRequest(
            CampId: camp.Id,
            Year: 2026,
            StartDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate: new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult: 200m, // Custom price
            PricePerChild: 140m,
            PricePerBaby: 70m,
            UseCustomAgeRanges: false,
            CustomBabyMaxAge: null,
            CustomChildMinAge: null,
            CustomChildMaxAge: null,
            CustomAdultMinAge: null,
            MaxCapacity: 100,
            Notes: null
        );

        _repository.CreateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<CampEdition>());

        // Act
        var result = await _sut.ProposeAsync(request);

        // Assert
        result.PricePerAdult.Should().Be(200m); // Custom price used
        result.PricePerChild.Should().Be(140m);
        result.PricePerBaby.Should().Be(70m);
    }

    [Fact]
    public async Task ProposeAsync_WithInactiveCamp_ThrowsException()
    {
        // Arrange
        var camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = "Inactive Camp",
            IsActive = false // Inactive
        };

        _campsRepository.GetByIdAsync(camp.Id, Arg.Any<CancellationToken>())
            .Returns(camp);

        var request = new ProposeCampEditionRequest(
            CampId: camp.Id,
            Year: 2026,
            StartDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate: new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult: null,
            PricePerChild: null,
            PricePerBaby: null,
            UseCustomAgeRanges: false,
            CustomBabyMaxAge: null,
            CustomChildMinAge: null,
            CustomChildMaxAge: null,
            CustomAdultMinAge: null,
            MaxCapacity: null,
            Notes: null
        );

        // Act & Assert
        var act = async () => await _sut.ProposeAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot propose edition for inactive camp*");
    }

    [Fact]
    public async Task ProposeAsync_WithEndDateBeforeStartDate_ThrowsException()
    {
        // Arrange
        var camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = "Test Camp",
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            IsActive = true
        };

        _campsRepository.GetByIdAsync(camp.Id, Arg.Any<CancellationToken>())
            .Returns(camp);

        var request = new ProposeCampEditionRequest(
            CampId: camp.Id,
            Year: 2026,
            StartDate: new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            EndDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), // Before start!
            PricePerAdult: null,
            PricePerChild: null,
            PricePerBaby: null,
            UseCustomAgeRanges: false,
            CustomBabyMaxAge: null,
            CustomChildMinAge: null,
            CustomChildMaxAge: null,
            CustomAdultMinAge: null,
            MaxCapacity: null,
            Notes: null
        );

        // Act & Assert
        var act = async () => await _sut.ProposeAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*End date must be after start date*");
    }

    #endregion

    #region GetProposedAsync Tests

    [Fact]
    public async Task GetProposedAsync_WithYear_ReturnsOnlyProposedEditions()
    {
        // Arrange
        var editions = new List<CampEdition>
        {
            new() { Id = Guid.NewGuid(), CampId = Guid.NewGuid(), Year = 2026, Status = CampEditionStatus.Proposed, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(10), PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m, Camp = new Camp { Name = "Camp 1", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m } },
            new() { Id = Guid.NewGuid(), CampId = Guid.NewGuid(), Year = 2026, Status = CampEditionStatus.Proposed, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(10), PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m, Camp = new Camp { Name = "Camp 2", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m } }
        };

        _repository.GetByStatusAndYearAsync(CampEditionStatus.Proposed, 2026, Arg.Any<CancellationToken>())
            .Returns(editions);

        // Act
        var result = await _sut.GetProposedAsync(2026);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(e => e.Status.Should().Be(CampEditionStatus.Proposed));
        result.Should().AllSatisfy(e => e.Year.Should().Be(2026));
    }

    #endregion

    #region PromoteToDraftAsync Tests

    [Fact]
    public async Task PromoteToDraftAsync_FromProposed_ChangesStatusToDraft()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        var edition = new CampEdition
        {
            Id = editionId,
            CampId = Guid.NewGuid(),
            Year = 2026,
            Status = CampEditionStatus.Proposed,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(10),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            Camp = new Camp { Name = "Test Camp", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m }
        };

        _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(edition);

        _repository.UpdateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<CampEdition>());

        // Act
        var result = await _sut.PromoteToDraftAsync(editionId);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(CampEditionStatus.Draft);

        await _repository.Received(1).UpdateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PromoteToDraftAsync_FromNonProposed_ThrowsException()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        var edition = new CampEdition
        {
            Id = editionId,
            CampId = Guid.NewGuid(),
            Year = 2026,
            Status = CampEditionStatus.Draft, // Already Draft
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(10),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m
        };

        _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(edition);

        // Act & Assert
        var act = async () => await _sut.PromoteToDraftAsync(editionId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only Proposed editions can be promoted to Draft*");
    }

    #endregion

    #region RejectProposalAsync Tests

    [Fact]
    public async Task RejectProposalAsync_MarksAsArchived()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        var edition = new CampEdition
        {
            Id = editionId,
            CampId = Guid.NewGuid(),
            Year = 2026,
            Status = CampEditionStatus.Proposed,
            IsArchived = false,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(10),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m
        };

        _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(edition);

        _repository.UpdateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<CampEdition>());

        // Act
        var result = await _sut.RejectProposalAsync(editionId);

        // Assert
        result.Should().BeTrue();

        await _repository.Received(1).UpdateAsync(
            Arg.Is<CampEdition>(e => e.IsArchived == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectProposalAsync_FromNonProposed_ThrowsException()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        var edition = new CampEdition
        {
            Id = editionId,
            CampId = Guid.NewGuid(),
            Year = 2026,
            Status = CampEditionStatus.Open, // Not Proposed
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(10),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m
        };

        _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(edition);

        // Act & Assert
        var act = async () => await _sut.RejectProposalAsync(editionId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only Proposed editions can be rejected*");
    }

    #endregion

    #region ProposeAsync — Duplicate Prevention Tests

    [Fact]
    public async Task ProposeAsync_WhenEditionAlreadyExists_ThrowsException()
    {
        // Arrange
        var camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = "Test Camp",
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            IsActive = true
        };

        _campsRepository.GetByIdAsync(camp.Id, Arg.Any<CancellationToken>())
            .Returns(camp);

        _repository.ExistsAsync(camp.Id, 2026, Arg.Any<CancellationToken>())
            .Returns(true); // Duplicate exists

        var request = new ProposeCampEditionRequest(
            CampId: camp.Id,
            Year: 2026,
            StartDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate: new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult: null,
            PricePerChild: null,
            PricePerBaby: null,
            UseCustomAgeRanges: false,
            CustomBabyMaxAge: null,
            CustomChildMinAge: null,
            CustomChildMaxAge: null,
            CustomAdultMinAge: null,
            MaxCapacity: null,
            Notes: null
        );

        // Act & Assert
        var act = async () => await _sut.ProposeAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Ya existe una edición para este campamento en el año 2026*");
    }

    [Fact]
    public async Task ProposeAsync_WhenNoExistingEdition_ChecksExistsBeforeCreating()
    {
        // Arrange
        var camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = "Test Camp",
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            IsActive = true
        };

        _campsRepository.GetByIdAsync(camp.Id, Arg.Any<CancellationToken>())
            .Returns(camp);

        _repository.ExistsAsync(camp.Id, 2026, Arg.Any<CancellationToken>())
            .Returns(false); // No duplicate

        _repository.CreateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<CampEdition>());

        var request = new ProposeCampEditionRequest(
            CampId: camp.Id,
            Year: 2026,
            StartDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate: new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult: null,
            PricePerChild: null,
            PricePerBaby: null,
            UseCustomAgeRanges: false,
            CustomBabyMaxAge: null,
            CustomChildMinAge: null,
            CustomChildMaxAge: null,
            CustomAdultMinAge: null,
            MaxCapacity: null,
            Notes: null
        );

        // Act
        var result = await _sut.ProposeAsync(request);

        // Assert
        await _repository.Received(1).ExistsAsync(camp.Id, 2026, Arg.Any<CancellationToken>());
        await _repository.Received(1).CreateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>());
        result.Status.Should().Be(CampEditionStatus.Proposed);
    }

    #endregion

    #region ChangeStatusAsync Tests

    [Theory]
    [InlineData(CampEditionStatus.Draft, CampEditionStatus.Open)]
    [InlineData(CampEditionStatus.Open, CampEditionStatus.Closed)]
    [InlineData(CampEditionStatus.Closed, CampEditionStatus.Completed)]
    [InlineData(CampEditionStatus.Open, CampEditionStatus.Draft)]
    public async Task ChangeStatusAsync_WithValidTransition_UpdatesStatus(
        CampEditionStatus from, CampEditionStatus to)
    {
        // Arrange
        var editionId = Guid.NewGuid();
        // Use a future start date for Draft→Open, and a past end date for Closed→Completed
        // For Open→Draft, use a past start date (no date constraint applies on →Draft)
        var startDate = to == CampEditionStatus.Open
            ? DateTime.UtcNow.AddDays(1)
            : to == CampEditionStatus.Draft
                ? DateTime.UtcNow.AddDays(-30)
                : DateTime.UtcNow.AddDays(-30);
        var endDate = to == CampEditionStatus.Completed
            ? DateTime.UtcNow.AddDays(-1)
            : to == CampEditionStatus.Draft
                ? DateTime.UtcNow.AddDays(10)
                : DateTime.UtcNow.AddDays(10);

        var edition = new CampEdition
        {
            Id = editionId,
            CampId = Guid.NewGuid(),
            Year = 2026,
            Status = from,
            StartDate = startDate,
            EndDate = endDate,
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            Camp = new Camp { Name = "Test Camp", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m }
        };

        _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(edition);

        _repository.UpdateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<CampEdition>());

        // Act
        var result = await _sut.ChangeStatusAsync(editionId, to, force: false);

        // Assert
        result.Status.Should().Be(to);
        await _repository.Received(1).UpdateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(CampEditionStatus.Proposed, CampEditionStatus.Open)]
    [InlineData(CampEditionStatus.Proposed, CampEditionStatus.Closed)]
    [InlineData(CampEditionStatus.Draft, CampEditionStatus.Closed)]
    [InlineData(CampEditionStatus.Open, CampEditionStatus.Completed)]
    [InlineData(CampEditionStatus.Completed, CampEditionStatus.Draft)]
    public async Task ChangeStatusAsync_WithInvalidTransition_ThrowsException(
        CampEditionStatus from, CampEditionStatus to)
    {
        // Arrange
        var editionId = Guid.NewGuid();
        var edition = new CampEdition
        {
            Id = editionId,
            CampId = Guid.NewGuid(),
            Year = 2026,
            Status = from,
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(10),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m
        };

        _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(edition);

        // Act & Assert
        var act = async () => await _sut.ChangeStatusAsync(editionId, to, force: false);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*La transición de '{from}' a '{to}' no es válida*");
    }

    [Fact]
    public async Task ChangeStatusAsync_ToOpen_WithPastStartDate_ThrowsException()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        var edition = new CampEdition
        {
            Id = editionId,
            CampId = Guid.NewGuid(),
            Year = 2025,
            Status = CampEditionStatus.Draft,
            StartDate = DateTime.UtcNow.AddDays(-1), // Past start date
            EndDate = DateTime.UtcNow.AddDays(5),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m
        };

        _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(edition);

        // Act & Assert
        var act = async () => await _sut.ChangeStatusAsync(editionId, CampEditionStatus.Open, force: false);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No se puede abrir el registro*");
    }

    [Fact]
    public async Task ChangeStatusAsync_ToCompleted_WithFutureEndDate_ThrowsException()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        var edition = new CampEdition
        {
            Id = editionId,
            CampId = Guid.NewGuid(),
            Year = 2026,
            Status = CampEditionStatus.Closed,
            StartDate = DateTime.UtcNow.AddDays(-20),
            EndDate = DateTime.UtcNow.AddDays(5), // End date still in future
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m
        };

        _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(edition);

        // Act & Assert
        var act = async () => await _sut.ChangeStatusAsync(editionId, CampEditionStatus.Completed, force: false);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No se puede marcar como completada*");
    }

    [Fact]
    public async Task ChangeStatusAsync_WithNotFoundEdition_ThrowsException()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns((CampEdition?)null);

        // Act & Assert
        var act = async () => await _sut.ChangeStatusAsync(editionId, CampEditionStatus.Draft, force: false);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*La edición de campamento no fue encontrada*");
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenOpenToDraft_WithForceFalse_SetsStatusToDraft()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        var edition = new CampEdition
        {
            Id = editionId,
            CampId = Guid.NewGuid(),
            Year = 2026,
            Status = CampEditionStatus.Open,
            StartDate = DateTime.UtcNow.AddDays(-5), // Camp already started — still allowed for Open→Draft
            EndDate = DateTime.UtcNow.AddDays(5),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            Camp = new Camp { Name = "Test Camp", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m }
        };

        _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(edition);
        _repository.UpdateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<CampEdition>());

        // Act
        var result = await _sut.ChangeStatusAsync(editionId, CampEditionStatus.Draft, force: false);

        // Assert
        result.Status.Should().Be(CampEditionStatus.Draft);
        await _repository.Received(1).UpdateAsync(
            Arg.Is<CampEdition>(e => e.Status == CampEditionStatus.Draft),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenDraftToOpen_WithForceTrue_AndStartDateInPast_UpdatesStatus()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        var edition = new CampEdition
        {
            Id = editionId,
            CampId = Guid.NewGuid(),
            Year = 2026,
            Status = CampEditionStatus.Draft,
            StartDate = DateTime.UtcNow.AddDays(-3), // Past start date
            EndDate = DateTime.UtcNow.AddDays(5),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            Camp = new Camp { Name = "Test Camp", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m }
        };

        _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(edition);
        _repository.UpdateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<CampEdition>());

        // Act — force=true bypasses the startDate < today constraint
        var result = await _sut.ChangeStatusAsync(editionId, CampEditionStatus.Open, force: true);

        // Assert
        result.Status.Should().Be(CampEditionStatus.Open);
        await _repository.Received(1).UpdateAsync(
            Arg.Is<CampEdition>(e => e.Status == CampEditionStatus.Open),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenDraftToOpen_WithForceFalse_AndStartDateInPast_ThrowsException()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        var edition = new CampEdition
        {
            Id = editionId,
            CampId = Guid.NewGuid(),
            Year = 2026,
            Status = CampEditionStatus.Draft,
            StartDate = DateTime.UtcNow.AddDays(-3), // Past start date
            EndDate = DateTime.UtcNow.AddDays(5),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m
        };

        _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(edition);

        // Act & Assert — force=false keeps the date constraint active
        var act = async () => await _sut.ChangeStatusAsync(editionId, CampEditionStatus.Open, force: false);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No se puede abrir el registro*");
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenClosedToDraft_ThrowsInvalidTransitionException()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        var edition = new CampEdition
        {
            Id = editionId,
            CampId = Guid.NewGuid(),
            Year = 2026,
            Status = CampEditionStatus.Closed,
            StartDate = DateTime.UtcNow.AddDays(-10),
            EndDate = DateTime.UtcNow.AddDays(-1),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m
        };

        _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(edition);

        // Act & Assert — Closed → Draft is never valid (only Open → Draft is the new backward transition)
        var act = async () => await _sut.ChangeStatusAsync(editionId, CampEditionStatus.Draft, force: false);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*La transición de 'Closed' a 'Draft' no es válida*");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithDraftEdition_UpdatesAllFields()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        var edition = new CampEdition
        {
            Id = editionId,
            CampId = Guid.NewGuid(),
            Year = 2026,
            Status = CampEditionStatus.Draft,
            StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            Camp = new Camp { Name = "Test Camp", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m }
        };

        _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(edition);

        _repository.UpdateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<CampEdition>());

        var request = new UpdateCampEditionRequest(
            StartDate: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate: new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult: 200m,
            PricePerChild: 140m,
            PricePerBaby: 70m,
            UseCustomAgeRanges: false,
            CustomBabyMaxAge: null,
            CustomChildMinAge: null,
            CustomChildMaxAge: null,
            CustomAdultMinAge: null,
            MaxCapacity: 150,
            Notes: "Updated notes"
        );

        // Act
        var result = await _sut.UpdateAsync(editionId, request);

        // Assert
        result.StartDate.Should().Be(request.StartDate);
        result.EndDate.Should().Be(request.EndDate);
        result.PricePerAdult.Should().Be(200m);
        result.MaxCapacity.Should().Be(150);
        result.Notes.Should().Be("Updated notes");
        await _repository.Received(1).UpdateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WithOpenEdition_AllowsUpdatingNotesAndCapacity()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        var startDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
        var edition = new CampEdition
        {
            Id = editionId,
            CampId = Guid.NewGuid(),
            Year = 2026,
            Status = CampEditionStatus.Open,
            StartDate = startDate,
            EndDate = endDate,
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            MaxCapacity = 100,
            Camp = new Camp { Name = "Test Camp", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m }
        };

        _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(edition);

        _repository.UpdateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<CampEdition>());

        // Same dates and prices, different capacity and notes
        var request = new UpdateCampEditionRequest(
            StartDate: startDate,
            EndDate: endDate,
            PricePerAdult: 180m,
            PricePerChild: 120m,
            PricePerBaby: 60m,
            UseCustomAgeRanges: false,
            CustomBabyMaxAge: null,
            CustomChildMinAge: null,
            CustomChildMaxAge: null,
            CustomAdultMinAge: null,
            MaxCapacity: 200,
            Notes: "New note"
        );

        // Act
        var result = await _sut.UpdateAsync(editionId, request);

        // Assert
        result.MaxCapacity.Should().Be(200);
        result.Notes.Should().Be("New note");
    }

    [Fact]
    public async Task UpdateAsync_WithOpenEdition_ChangingDates_ThrowsException()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        var edition = new CampEdition
        {
            Id = editionId,
            CampId = Guid.NewGuid(),
            Year = 2026,
            Status = CampEditionStatus.Open,
            StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            Camp = new Camp { Name = "Test Camp", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m }
        };

        _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(edition);

        // Different start date
        var request = new UpdateCampEditionRequest(
            StartDate: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate: new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult: 180m,
            PricePerChild: 120m,
            PricePerBaby: 60m,
            UseCustomAgeRanges: false,
            CustomBabyMaxAge: null,
            CustomChildMinAge: null,
            CustomChildMaxAge: null,
            CustomAdultMinAge: null,
            MaxCapacity: null,
            Notes: null
        );

        // Act & Assert
        var act = async () => await _sut.UpdateAsync(editionId, request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No se pueden modificar las fechas ni los precios*");
    }

    [Theory]
    [InlineData(CampEditionStatus.Closed)]
    [InlineData(CampEditionStatus.Completed)]
    public async Task UpdateAsync_WithClosedOrCompletedEdition_ThrowsException(CampEditionStatus status)
    {
        // Arrange
        var editionId = Guid.NewGuid();
        var edition = new CampEdition
        {
            Id = editionId,
            CampId = Guid.NewGuid(),
            Year = 2026,
            Status = status,
            StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            Camp = new Camp { Name = "Test Camp", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m }
        };

        _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(edition);

        var request = new UpdateCampEditionRequest(
            StartDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate: new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult: 180m,
            PricePerChild: 120m,
            PricePerBaby: 60m,
            UseCustomAgeRanges: false,
            CustomBabyMaxAge: null,
            CustomChildMinAge: null,
            CustomChildMaxAge: null,
            CustomAdultMinAge: null,
            MaxCapacity: null,
            Notes: null
        );

        // Act & Assert
        var act = async () => await _sut.UpdateAsync(editionId, request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No se puede modificar una edición cerrada o completada*");
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithExistingEdition_ReturnsResponse()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        var edition = new CampEdition
        {
            Id = editionId,
            CampId = Guid.NewGuid(),
            Year = 2026,
            Status = CampEditionStatus.Draft,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(10),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            Camp = new Camp { Name = "Test Camp", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m }
        };

        _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(edition);

        // Act
        var result = await _sut.GetByIdAsync(editionId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(editionId);
        result.CampName.Should().Be("Test Camp");
        result.Status.Should().Be(CampEditionStatus.Draft);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentEdition_ReturnsNull()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns((CampEdition?)null);

        // Act
        var result = await _sut.GetByIdAsync(editionId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_WithNoFilters_ReturnsAllEditions()
    {
        // Arrange
        var editions = new List<CampEdition>
        {
            new() { Id = Guid.NewGuid(), CampId = Guid.NewGuid(), Year = 2026, Status = CampEditionStatus.Draft, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(10), PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m, Camp = new Camp { Name = "Camp A", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m } },
            new() { Id = Guid.NewGuid(), CampId = Guid.NewGuid(), Year = 2025, Status = CampEditionStatus.Completed, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(10), PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m, Camp = new Camp { Name = "Camp B", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m } }
        };

        _repository.GetAllAsync(null, null, null, Arg.Any<CancellationToken>())
            .Returns(editions);

        // Act
        var result = await _sut.GetAllAsync(null, null, null);

        // Assert
        result.Should().HaveCount(2);
        await _repository.Received(1).GetAllAsync(null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllAsync_WithYearFilter_PassesFilterToRepository()
    {
        // Arrange
        _repository.GetAllAsync(2026, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<CampEdition>());

        // Act
        await _sut.GetAllAsync(2026, null, null);

        // Assert
        await _repository.Received(1).GetAllAsync(2026, null, null, Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetActiveEditionAsync Tests

    [Fact]
    public async Task GetActiveEditionAsync_WithOpenEditionForYear_ReturnsActiveEdition()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var edition = new CampEdition
        {
            Id = Guid.NewGuid(),
            CampId = campId,
            Year = 2026,
            Status = CampEditionStatus.Open,
            StartDate = DateTime.UtcNow.AddDays(-5),
            EndDate = DateTime.UtcNow.AddDays(5),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            MaxCapacity = 100,
            Camp = new Camp { Id = campId, Name = "Summer Camp", Location = "Madrid", FormattedAddress = "Calle 1, Madrid", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m }
        };

        _repository.GetByStatusAndYearAsync(CampEditionStatus.Open, 2026, Arg.Any<CancellationToken>())
            .Returns(new List<CampEdition> { edition });

        // Act
        var result = await _sut.GetActiveEditionAsync(2026);

        // Assert
        result.Should().NotBeNull();
        result!.CampId.Should().Be(campId);
        result.CampName.Should().Be("Summer Camp");
        result.CampLocation.Should().Be("Madrid");
        result.Status.Should().Be(CampEditionStatus.Open);
        result.RegistrationCount.Should().Be(0); // Always 0 placeholder
    }

    [Fact]
    public async Task GetActiveEditionAsync_WithNoOpenEdition_ReturnsNull()
    {
        // Arrange
        _repository.GetByStatusAndYearAsync(CampEditionStatus.Open, 2026, Arg.Any<CancellationToken>())
            .Returns(new List<CampEdition>());

        // Act
        var result = await _sut.GetActiveEditionAsync(2026);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveEditionAsync_WithNullYear_UsesCurrentYear()
    {
        // Arrange
        var currentYear = DateTime.UtcNow.Year;
        _repository.GetByStatusAndYearAsync(CampEditionStatus.Open, currentYear, Arg.Any<CancellationToken>())
            .Returns(new List<CampEdition>());

        // Act
        await _sut.GetActiveEditionAsync(null);

        // Assert
        await _repository.Received(1).GetByStatusAndYearAsync(
            CampEditionStatus.Open,
            currentYear,
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region AccommodationCapacity Template Auto-update Tests

    [Fact]
    public async Task ProposeAsync_WithAccommodationCapacity_UpdatesCampTemplate()
    {
        // Arrange
        var camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = "Test Camp",
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            IsActive = true
        };
        camp.SetAccommodationCapacity(new AccommodationCapacity { PrivateRoomsWithBathroom = 5 });

        _campsRepository.GetByIdAsync(camp.Id, Arg.Any<CancellationToken>()).Returns(camp);
        _repository.ExistsAsync(camp.Id, 2026, Arg.Any<CancellationToken>()).Returns(false);

        var newAccommodation = new AccommodationCapacity { PrivateRoomsWithBathroom = 10 };
        var request = new ProposeCampEditionRequest(
            CampId: camp.Id,
            Year: 2026,
            StartDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate: new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult: null,
            PricePerChild: null,
            PricePerBaby: null,
            UseCustomAgeRanges: false,
            CustomBabyMaxAge: null,
            CustomChildMinAge: null,
            CustomChildMaxAge: null,
            CustomAdultMinAge: null,
            MaxCapacity: null,
            Notes: null,
            AccommodationCapacity: newAccommodation
        );

        _repository.CreateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<CampEdition>());

        // Act
        await _sut.ProposeAsync(request);

        // Assert: camp template updated with new accommodation
        await _campsRepository.Received(1).UpdateAsync(
            Arg.Is<Camp>(c => c.GetAccommodationCapacity()!.PrivateRoomsWithBathroom == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProposeAsync_WithNullAccommodationCapacity_DoesNotUpdateCampTemplate()
    {
        // Arrange
        var camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = "Test Camp",
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            IsActive = true
        };

        _campsRepository.GetByIdAsync(camp.Id, Arg.Any<CancellationToken>()).Returns(camp);
        _repository.ExistsAsync(camp.Id, 2026, Arg.Any<CancellationToken>()).Returns(false);
        _repository.CreateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<CampEdition>());

        var request = new ProposeCampEditionRequest(
            CampId: camp.Id,
            Year: 2026,
            StartDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate: new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult: null,
            PricePerChild: null,
            PricePerBaby: null,
            UseCustomAgeRanges: false,
            CustomBabyMaxAge: null,
            CustomChildMinAge: null,
            CustomChildMaxAge: null,
            CustomAdultMinAge: null,
            MaxCapacity: null,
            Notes: null,
            AccommodationCapacity: null
        );

        // Act
        await _sut.ProposeAsync(request);

        // Assert: camp NOT updated when accommodation is null
        await _campsRepository.DidNotReceive().UpdateAsync(
            Arg.Any<Camp>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PromoteToDraftAsync_WithEditionAccommodation_UpdatesCampTemplate()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var camp = new Camp
        {
            Id = campId,
            Name = "Test Camp",
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            IsActive = true
        };

        var edition = new CampEdition
        {
            Id = Guid.NewGuid(),
            CampId = campId,
            Year = 2026,
            StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            Status = CampEditionStatus.Proposed,
            Camp = camp
        };
        edition.SetAccommodationCapacity(new AccommodationCapacity { PrivateRoomsWithBathroom = 7 });

        _repository.GetByIdAsync(edition.Id, Arg.Any<CancellationToken>()).Returns(edition);
        _repository.UpdateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<CampEdition>());
        _campsRepository.UpdateAsync(Arg.Any<Camp>(), Arg.Any<CancellationToken>())
            .Returns(camp);

        // Act
        await _sut.PromoteToDraftAsync(edition.Id);

        // Assert: camp template synced from edition accommodation
        await _campsRepository.Received(1).UpdateAsync(
            Arg.Is<Camp>(c => c.GetAccommodationCapacity()!.PrivateRoomsWithBathroom == 7),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PromoteToDraftAsync_WithoutEditionAccommodation_DoesNotUpdateCampTemplate()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var camp = new Camp
        {
            Id = campId,
            Name = "Test Camp",
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            IsActive = true
        };

        var edition = new CampEdition
        {
            Id = Guid.NewGuid(),
            CampId = campId,
            Year = 2026,
            StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            Status = CampEditionStatus.Proposed,
            Camp = camp,
            AccommodationCapacityJson = null
        };

        _repository.GetByIdAsync(edition.Id, Arg.Any<CancellationToken>()).Returns(edition);
        _repository.UpdateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<CampEdition>());

        // Act
        await _sut.PromoteToDraftAsync(edition.Id);

        // Assert: camp NOT updated when edition has no accommodation
        await _campsRepository.DidNotReceive().UpdateAsync(
            Arg.Any<Camp>(),
            Arg.Any<CancellationToken>());
    }

    #endregion
}
