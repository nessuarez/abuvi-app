using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.MediaItems;
using Abuvi.API.Features.Memories;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Memories;

public class MemoriesServiceTests
{
    private readonly IMemoriesRepository _repository;
    private readonly IMediaItemsRepository _mediaItemsRepository;
    private readonly ILogger<MemoriesService> _logger;
    private readonly MemoriesService _service;

    public MemoriesServiceTests()
    {
        _repository = Substitute.For<IMemoriesRepository>();
        _mediaItemsRepository = Substitute.For<IMediaItemsRepository>();
        _logger = Substitute.For<ILogger<MemoriesService>>();
        _service = new MemoriesService(_repository, _mediaItemsRepository, _logger);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_CreatesMemoryWithDefaultFlags()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateMemoryRequest("My Summer Memory", "Great times at camp", 1985, null);

        // Act
        var result = await _service.CreateAsync(userId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("My Summer Memory");
        result.Content.Should().Be("Great times at camp");
        result.IsApproved.Should().BeFalse();
        result.IsPublished.Should().BeFalse();
        result.AuthorUserId.Should().Be(userId);

        await _repository.Received(1).AddAsync(Arg.Any<Memory>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithYear_SetsYearCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateMemoryRequest("Title", "Content", 1990, null);

        // Act
        var result = await _service.CreateAsync(userId, request, CancellationToken.None);

        // Assert
        result.Year.Should().Be(1990);
    }

    [Fact]
    public async Task CreateAsync_CallsRepositoryAdd()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateMemoryRequest("Title", "Content", null, null);

        // Act
        await _service.CreateAsync(userId, request, CancellationToken.None);

        // Assert
        await _repository.Received(1).AddAsync(
            Arg.Is<Memory>(m => m.Title == "Title" && m.Content == "Content"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsMemoryResponse()
    {
        // Arrange
        var memoryId = Guid.NewGuid();
        var memory = CreateTestMemory(memoryId);

        _repository.GetByIdAsync(memoryId, Arg.Any<CancellationToken>())
            .Returns(memory);
        _mediaItemsRepository.GetByMemoryIdAsync(memoryId, Arg.Any<CancellationToken>())
            .Returns(new List<MediaItem>());

        // Act
        var result = await _service.GetByIdAsync(memoryId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(memoryId);
        result.Title.Should().Be("Test Memory");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var memoryId = Guid.NewGuid();
        _repository.GetByIdAsync(memoryId, Arg.Any<CancellationToken>())
            .Returns((Memory?)null);

        // Act & Assert
        await _service.Invoking(s => s.GetByIdAsync(memoryId, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetListAsync_WithApprovedTrue_DelegatesToRepository()
    {
        // Arrange
        _repository.GetListAsync(null, true, Arg.Any<CancellationToken>())
            .Returns(new List<Memory>());

        // Act
        var result = await _service.GetListAsync(null, true, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        await _repository.Received(1).GetListAsync(null, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApproveAsync_WithExistingId_SetsBothFlagsTrue()
    {
        // Arrange
        var memoryId = Guid.NewGuid();
        var memory = CreateTestMemory(memoryId);

        _repository.GetByIdAsync(memoryId, Arg.Any<CancellationToken>())
            .Returns(memory);

        // Act
        var result = await _service.ApproveAsync(memoryId, CancellationToken.None);

        // Assert
        result.IsApproved.Should().BeTrue();
        result.IsPublished.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(Arg.Any<Memory>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApproveAsync_WithNonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var memoryId = Guid.NewGuid();
        _repository.GetByIdAsync(memoryId, Arg.Any<CancellationToken>())
            .Returns((Memory?)null);

        // Act & Assert
        await _service.Invoking(s => s.ApproveAsync(memoryId, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RejectAsync_WithExistingId_SetsBothFlagsFalse()
    {
        // Arrange
        var memoryId = Guid.NewGuid();
        var memory = CreateTestMemory(memoryId);
        memory.IsApproved = true;
        memory.IsPublished = true;

        _repository.GetByIdAsync(memoryId, Arg.Any<CancellationToken>())
            .Returns(memory);

        // Act
        var result = await _service.RejectAsync(memoryId, CancellationToken.None);

        // Assert
        result.IsApproved.Should().BeFalse();
        result.IsPublished.Should().BeFalse();
        await _repository.Received(1).UpdateAsync(Arg.Any<Memory>(), Arg.Any<CancellationToken>());
    }

    // Helper methods
    private static Memory CreateTestMemory(Guid id) => new()
    {
        Id = id,
        AuthorUserId = Guid.NewGuid(),
        Title = "Test Memory",
        Content = "Test content",
        Year = 1990,
        IsApproved = false,
        IsPublished = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        Author = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            PasswordHash = "hash",
            Role = UserRole.Member,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }
    };
}
