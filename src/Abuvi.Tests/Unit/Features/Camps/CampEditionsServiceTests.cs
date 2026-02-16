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
}
