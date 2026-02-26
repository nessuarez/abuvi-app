using Abuvi.API.Common.Models;
using FluentValidation;

namespace Abuvi.API.Features.BlobStorage;

public static class BlobStorageEndpoints
{
    public static WebApplication MapBlobStorageEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/blobs")
            .WithTags("BlobStorage");

        group.MapPost("/upload", UploadAsync)
            .RequireAuthorization()
            .DisableAntiforgery() // Required for multipart/form-data in Minimal APIs
            .WithName("UploadBlob")
            .WithSummary("Upload a file to blob storage");

        group.MapDelete("/", DeleteAsync)
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .WithName("DeleteBlobs")
            .WithSummary("Delete one or more blobs (Admin only)");

        group.MapGet("/stats", GetStatsAsync)
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .WithName("GetBlobStats")
            .WithSummary("Get storage usage statistics (Admin only)");

        return app;
    }

    private static async Task<IResult> UploadAsync(
        IFormFile file,
        [Microsoft.AspNetCore.Mvc.FromForm] string folder,
        [Microsoft.AspNetCore.Mvc.FromForm] Guid? contextId,
        [Microsoft.AspNetCore.Mvc.FromForm] bool generateThumbnail,
        IBlobStorageService blobService,
        IValidator<UploadBlobRequest> validator,
        CancellationToken ct)
    {
        var request = new UploadBlobRequest
        {
            File = file,
            Folder = folder,
            ContextId = contextId,
            GenerateThumbnail = generateThumbnail
        };

        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .Select(e => new ValidationError(e.PropertyName, e.ErrorMessage))
                .ToList();
            return TypedResults.BadRequest(
                ApiResponse<BlobUploadResult>.ValidationFail("Datos no válidos", errors));
        }

        await using var stream = file.OpenReadStream();
        var result = await blobService.UploadAsync(
            stream,
            file.FileName,
            file.ContentType,
            folder,
            contextId,
            generateThumbnail,
            ct);

        return TypedResults.Ok(ApiResponse<BlobUploadResult>.Ok(result));
    }

    private static async Task<IResult> DeleteAsync(
        DeleteBlobsRequest body,
        IBlobStorageService blobService,
        CancellationToken ct)
    {
        await blobService.DeleteManyAsync(body.BlobKeys, ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetStatsAsync(
        IBlobStorageService blobService,
        CancellationToken ct)
    {
        var stats = await blobService.GetStatsAsync(ct);
        return TypedResults.Ok(ApiResponse<BlobStorageStats>.Ok(stats));
    }
}
