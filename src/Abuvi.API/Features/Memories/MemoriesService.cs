using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.MediaItems;

namespace Abuvi.API.Features.Memories;

public class MemoriesService(
    IMemoriesRepository repository,
    IMediaItemsRepository mediaItemsRepository,
    ICampEditionsRepository campEditionsRepository,
    ILogger<MemoriesService> logger)
{
    public async Task<MemoryResponse> CreateAsync(
        Guid userId,
        CreateMemoryRequest request,
        CancellationToken ct)
    {
        // Same placement rules as MediaItem: an explicit edition wins, otherwise an
        // unambiguous year resolves one, otherwise the story stays unplaced. Writing a
        // memory without knowing the year is valid and must not be rejected.
        var editionId = request.CampEditionId;
        if (editionId is null && request.Year is { } year)
        {
            var candidates = await campEditionsRepository.GetByYearAsync(year, ct);
            if (candidates.Count == 1) editionId = candidates[0].Id;
        }

        var memory = new Memory
        {
            Id = Guid.NewGuid(),
            AuthorUserId = userId,
            Title = request.Title,
            Content = request.Content,
            Year = request.Year,
            CampLocationId = request.CampLocationId,
            CampEditionId = editionId,
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
        Guid? campEditionId,
        bool unplacedOnly,
        CancellationToken ct)
    {
        var memories = await repository.GetListAsync(year, approved, campEditionId, unplacedOnly, ct);
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
