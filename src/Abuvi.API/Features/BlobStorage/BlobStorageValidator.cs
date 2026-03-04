using FluentValidation;
using Microsoft.Extensions.Options;

namespace Abuvi.API.Features.BlobStorage;

public class UploadBlobRequestValidator : AbstractValidator<UploadBlobRequest>
{
    private static readonly string[] AllowedFolders =
        ["photos", "media-items", "camp-locations", "camp-photos"];

    public UploadBlobRequestValidator(IOptions<BlobStorageOptions> options)
    {
        var cfg = options.Value;

        RuleFor(x => x.File)
            .NotNull().WithMessage("El archivo es obligatorio");

        RuleFor(x => x.File)
            .Must(f => f.Length <= cfg.MaxFileSizeBytes)
            .WithMessage($"El archivo no puede superar {cfg.MaxFileSizeBytes / 1_048_576} MB")
            .When(x => x.File is not null);

        RuleFor(x => x.Folder)
            .NotEmpty().WithMessage("La carpeta es obligatoria")
            .Must(f => AllowedFolders.Contains(f))
            .WithMessage("La carpeta especificada no es válida");

        RuleFor(x => x.File)
            .Must((req, file) => IsExtensionAllowed(req.Folder, file, cfg))
            .WithMessage("El tipo de archivo no está permitido para esta carpeta")
            .When(x => x.File is not null && AllowedFolders.Contains(x.Folder));
    }

    private static bool IsExtensionAllowed(string folder, IFormFile file, BlobStorageOptions cfg)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        return folder switch
        {
            "media-items" => cfg.AllowedImageExtensions.Contains(ext)
                             || cfg.AllowedVideoExtensions.Contains(ext)
                             || cfg.AllowedAudioExtensions.Contains(ext)
                             || cfg.AllowedDocumentExtensions.Contains(ext),
            _ => cfg.AllowedImageExtensions.Contains(ext)
        };
    }
}
