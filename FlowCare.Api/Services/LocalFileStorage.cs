using Microsoft.AspNetCore.StaticFiles;

namespace FlowCare.Api.Services;

public class LocalFileStorage : IFileStorage
{
    private readonly IWebHostEnvironment _env;

    public LocalFileStorage(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<(string relativePath, string contentType, long size)> SaveCustomerIdAsync(
        int customerProfileId,
        IFormFile file,
        CancellationToken ct = default)
    {
        // Root: <project>/storage/customer-ids/{customerId}/
        var root = Path.Combine(_env.ContentRootPath, "storage", "customer-ids", customerProfileId.ToString());
        Directory.CreateDirectory(root);

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(root, fileName);

        await using var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(fs, ct);

        var relativePath = Path.Combine("storage", "customer-ids", customerProfileId.ToString(), fileName)
            .Replace("\\", "/");

        return (relativePath, file.ContentType, file.Length);
    }

    public Task<(Stream stream, string contentType)> OpenReadAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_env.ContentRootPath, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("File not found", fullPath);

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(fullPath, out var contentType))
            contentType = "application/octet-stream";

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult((stream, contentType));
    }
}
