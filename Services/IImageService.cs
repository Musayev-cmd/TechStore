using Microsoft.AspNetCore.Http;

namespace TechStore.Services;

public sealed class ImageUploadResult
{
    public bool Succeeded { get; init; }
    public string? RelativeUrl { get; init; }
    public string? ErrorResourceKey { get; init; }

    public static ImageUploadResult Ok(string relativeUrl) => new()
    {
        Succeeded = true,
        RelativeUrl = relativeUrl
    };

    public static ImageUploadResult Fail(string errorResourceKey) => new()
    {
        Succeeded = false,
        ErrorResourceKey = errorResourceKey
    };
}

public interface IImageService
{
    long MaxFileSizeBytes { get; }
    IReadOnlyCollection<string> AllowedExtensions { get; }
    Task<ImageUploadResult> SaveProductImageAsync(IFormFile file, CancellationToken cancellationToken = default);
    bool DeleteProductImage(string? relativeUrl);
}
