using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.Camps;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

public class AccommodationAssignmentsServiceTests
{
    private readonly IAccommodationAssignmentsRepository _repository;
    private readonly AccommodationAssignmentsService _sut;

    private static readonly Guid EditionId = Guid.NewGuid();
    private static readonly Guid ProposalId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static readonly ProposalAssignmentStateResponse EmptyState = new(
        ProposalId, [], [], [], [], []);

    public AccommodationAssignmentsServiceTests()
    {
        _repository = Substitute.For<IAccommodationAssignmentsRepository>();
        _repository.ProposalBelongsToEditionAsync(ProposalId, EditionId, Arg.Any<CancellationToken>())
            .Returns(true);
        _repository.GetAssignmentStateAsync(EditionId, ProposalId, Arg.Any<CancellationToken>())
            .Returns(EmptyState);
        _sut = new AccommodationAssignmentsService(_repository);
    }

    [Fact]
    public async Task BulkReplace_WithValidAssignments_Succeeds()
    {
        var request = new BulkAssignRequest([new AssignmentEntry(Guid.NewGuid(), Guid.NewGuid())]);

        var result = await _sut.BulkReplaceAsync(EditionId, ProposalId, request, UserId);

        result.Should().Be(EmptyState);
        await _repository.Received(1).BulkReplaceAsync(
            ProposalId, EditionId, request.Assignments, UserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkReplace_WithRegistrationNotInEdition_ThrowsBusinessRuleException()
    {
        var request = new BulkAssignRequest([new AssignmentEntry(Guid.NewGuid(), Guid.NewGuid())]);
        _repository.BulkReplaceAsync(ProposalId, EditionId, request.Assignments, UserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BusinessRuleException("Algunas inscripciones no pertenecen a esta edición del campamento."));

        var act = () => _sut.BulkReplaceAsync(EditionId, ProposalId, request, UserId);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*inscripciones*");
    }

    [Fact]
    public async Task BulkReplace_WithAccommodationNotInEdition_ThrowsBusinessRuleException()
    {
        var request = new BulkAssignRequest([new AssignmentEntry(Guid.NewGuid(), Guid.NewGuid())]);
        _repository.BulkReplaceAsync(ProposalId, EditionId, request.Assignments, UserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BusinessRuleException("Alguno de los alojamientos no pertenece a esta edición del campamento."));

        var act = () => _sut.BulkReplaceAsync(EditionId, ProposalId, request, UserId);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*alojamientos*");
    }

    [Fact]
    public async Task BulkReplace_WithCapacityExceeded_ThrowsBusinessRuleException()
    {
        var request = new BulkAssignRequest([new AssignmentEntry(Guid.NewGuid(), Guid.NewGuid())]);
        _repository.BulkReplaceAsync(ProposalId, EditionId, request.Assignments, UserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BusinessRuleException("El alojamiento 'Cabaña A' no tiene capacidad para 6 personas (máximo: 4)."));

        var act = () => _sut.BulkReplaceAsync(EditionId, ProposalId, request, UserId);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*capacidad*");
    }

    [Fact]
    public async Task BulkReplace_WithByFamilyCapacityExceeded_ThrowsBusinessRuleException()
    {
        var request = new BulkAssignRequest([new AssignmentEntry(Guid.NewGuid(), Guid.NewGuid())]);
        _repository.BulkReplaceAsync(ProposalId, EditionId, request.Assignments, UserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BusinessRuleException("El alojamiento 'Caravana 1' no tiene capacidad para 3 familias (máximo: 2)."));

        var act = () => _sut.BulkReplaceAsync(EditionId, ProposalId, request, UserId);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*familias*");
    }

    [Fact]
    public async Task AssignFamily_WhenProposalDoesNotBelongToEdition_ThrowsNotFoundException()
    {
        var otherProposalId = Guid.NewGuid();
        _repository.ProposalBelongsToEditionAsync(otherProposalId, EditionId, Arg.Any<CancellationToken>())
            .Returns(false);

        var act = () => _sut.AssignAsync(
            EditionId, otherProposalId, Guid.NewGuid(),
            new SingleAssignRequest(Guid.NewGuid()), UserId);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
