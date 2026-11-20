using Microsoft.AspNetCore.Http;

namespace TechStore.Services;

public sealed class ImageService : IImageService
{
    public const long DefaultMaxFileSizeBytes = 5L * 1024 * 1024; // 5 MB
    private const string ProductUploadsWebPrefix = "/uploads/products/";

    private static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    private static readonly Dictionary<string, ImageFormat> ExtensionToFormat = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = ImageFormat.Jpeg,
        [".jpeg"] = ImageFormat.Jpeg,
        [".png"] = ImageFormat.Png,
        [".webp"] = ImageFormat.Webp
    };

    private static readonly Dictionary<string, ImageFormat> MimeToFormat = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ImageFormat.Jpeg,
        ["image/pjpeg"] = ImageFormat.Jpeg,
        ["image/png"] = ImageFormat.Png,
        ["image/webp"] = ImageFormat.Webp
    };

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ImageService> _logger;

    public ImageService(IWebHostEnvironment environment, ILogger<ImageService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public long MaxFileSizeBytes => DefaultMaxFileSizeBytes;
    public IReadOnlyCollection<string> AllowedExtensions => SupportedExtensions;

    public async Task<ImageUploadResult> SaveProductImageAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length <= 0)
        {
            return ImageUploadResult.Fail("Admin.ImageErrorEmpty");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return ImageUploadResult.Fail("Admin.ImageErrorTooLarge");
        }

        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;
        if (!ExtensionToFormat.TryGetValue(extension, out var expectedFormat))
        {
            return ImageUploadResult.Fail("Admin.ImageErrorInvalidExtension");
        }

        var contentType = (file.ContentType ?? string.Empty).Split(';', 2)[0].Trim();
        if (!MimeToFormat.TryGetValue(contentType, out var mimeFormat))
        {
            return ImageUploadResult.Fail("Admin.ImageErrorInvalidMime");
        }

        if (mimeFormat != expectedFormat)
        {
            return ImageUploadResult.Fail("Admin.ImageErrorMismatch");
        }

        // Read binary magic bytes to prevent forged extensions / polyglot payloads
        var header = new byte[12];
        int bytesRead;
        await using (var inspectStream = file.OpenReadStream())
        {
            bytesRead = await ReadExactlyUpToAsync(inspectStream, header, cancellationToken);
        }

        var detectedFormat = DetectFormatFromMagicBytes(header.AsSpan(0, bytesRead));
        if (detectedFormat == ImageFormat.Unknown)
        {
            return ImageUploadResult.Fail("Admin.ImageErrorInvalidSignature");
        }

        if (detectedFormat != expectedFormat)
        {
            return ImageUploadResult.Fail("Admin.ImageErrorMismatch");
        }

        var canonicalExtension = detectedFormat switch
        {
            ImageFormat.Jpeg => ".jpg",
            ImageFormat.Png => ".png",
            ImageFormat.Webp => ".webp",
            _ => throw new InvalidOperationException("Unsupported image format.")
        };

        var uploadsDir = GetUploadsDirectoryPath();
        Directory.CreateDirectory(uploadsDir);

        var safeFileName = $"{Guid.NewGuid():N}{canonicalExtension}";
        var fullTargetPath = Path.Combine(uploadsDir, safeFileName);

        await using (var source = file.OpenReadStream())
        await using (var destination = new FileStream(
            fullTargetPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true))
        {
            await source.CopyToAsync(destination, cancellationToken);
        }

        var relativeUrl = $"{ProductUploadsWebPrefix}{safeFileName}";
        _logger.LogInformation("Saved validated product image {RelativeUrl} ({Bytes} bytes).", relativeUrl, file.Length);
        return ImageUploadResult.Ok(relativeUrl);
    }

    public bool DeleteProductImage(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
        {
            return false;
        }

        var trimmed = relativeUrl.Trim();
        if (!trimmed.StartsWith(ProductUploadsWebPrefix, StringComparison.OrdinalIgnoreCase))
        {
            // Never delete seeded SVGs under /images/products/ or arbitrary paths
            return false;
        }

        var fileName = trimmed[ProductUploadsWebPrefix.Length..];
        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName.Contains('/') ||
            fileName.Contains('\\') ||
            fileName.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        var uploadsDir = Path.GetFullPath(GetUploadsDirectoryPath());
        var fullCandidatePath = Path.GetFullPath(Path.Combine(uploadsDir, fileName));

        var uploadsDirWithSeparator = uploadsDir.EndsWith(Path.DirectorySeparatorChar)
            ? uploadsDir
            : uploadsDir + Path.DirectorySeparatorChar;

        if (!fullCandidatePath.StartsWith(uploadsDirWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!File.Exists(fullCandidatePath))
        {
            return false;
        }

        try
        {
            File.Delete(fullCandidatePath);
            _logger.LogInformation("Deleted uploaded product image {RelativeUrl}.", trimmed);
            return true;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not delete uploaded product image {RelativeUrl}.", trimmed);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Permission denied deleting uploaded product image {RelativeUrl}.", trimmed);
            return false;
        }
    }

    private string GetUploadsDirectoryPath()
    {
        var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;

        return Path.Combine(webRoot, "uploads", "products");
    }

    private static async Task<int> ReadExactlyUpToAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private static ImageFormat DetectFormatFromMagicBytes(ReadOnlySpan<byte> header)
    {
        // JPEG: FF D8 FF
        if (header.Length >= 3 &&
            header[0] == 0xFF &&
            header[1] == 0xD8 &&
            header[2] == 0xFF)
        {
            return ImageFormat.Jpeg;
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (header.Length >= 8 &&
            header[0] == 0x89 &&
            header[1] == 0x50 &&
            header[2] == 0x4E &&
            header[3] == 0x47 &&
            header[4] == 0x0D &&
            header[5] == 0x0A &&
            header[6] == 0x1A &&
            header[7] == 0x0A)
        {
            return ImageFormat.Png;
        }

        // WebP: RIFF....WEBP
        if (header.Length >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
        {
            return ImageFormat.Webp;
        }

        return ImageFormat.Unknown;
    }

    private enum ImageFormat
    {
        Unknown = 0,
        Jpeg = 1,
        Png = 2,
        Webp = 3
    }
}
