using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.MediaItems;

namespace Abuvi.API.Features.Memories;

public class MemoriesService(
    IMemoriesRepository repository,
    IMediaItemsRepository mediaItemsRepository,
    ILogger<MemoriesService> logger)
{
    public async Task<MemoryResponse> CreateAsync(
        Guid userId,
        CreateMemoryRequest request,
        CancellationToken ct)
    {
        var memory = new Memory
        {
            Id = Guid.NewGuid(),
            AuthorUserId = userId,
            Title = request.Title,
            Content = request.Content,
            Year = request.Year,
            CampLocationId = request.CampLocationId,
            IsApproved = false,
            IsPublished = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(memory, ct);

        logger.LogInformation(
            "Memory {MemoryId} created by user {UserId}",
            memory.Id, userId);

        return memory.ToResponse();
    }

    public async Task<MemoryResponse> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var memory = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Memory), id);

        var mediaItems = await mediaItemsRepository.GetByMemoryIdAsync(id, ct);
        var mediaItemResponses = mediaItems.Select(m => m.ToResponse()).ToList();

        return memory.ToResponse(mediaItemResponses);
    }

    public async Task<IReadOnlyList<MemoryResponse>> GetListAsync(
        int? year,
        bool? approved,
        CancellationToken ct)
    {
        var memories = await repository.GetListAsync(year, approved, ct);
        return memories.Select(m => m.ToResponse()).ToList();
    }

    public async Task<MemoryResponse> ApproveAsync(Guid id, CancellationToken ct)
    {
        var memory = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Memory), id);

        memory.IsApproved = true;
        memory.IsPublished = true;
        memory.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(memory, ct);

        logger.LogInformation(
            "Memory {MemoryId} approved",
            id);

        return memory.ToResponse();
    }

    public async Task<MemoryResponse> RejectAsync(Guid id, CancellationToken ct)
    {
        var memory = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Memory), id);

        memory.IsApproved = false;
        memory.IsPublished = false;
        memory.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(memory, ct);

        logger.LogInformation(
            "Memory {MemoryId} rejected",
            id);

        return memory.ToResponse();
    }
}
