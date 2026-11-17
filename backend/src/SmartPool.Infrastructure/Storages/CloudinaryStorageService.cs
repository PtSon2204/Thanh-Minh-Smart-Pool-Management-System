using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Storages;

public class CloudinaryStorageService : IStorageService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryStorageService(Cloudinary cloudinary)
    {
        _cloudinary = cloudinary;
    }

    public async Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var uri = new Uri(fileUrl);
            var path = uri.AbsolutePath;
            var fileName = Path.GetFileNameWithoutExtension(path);
            var publicId = $"smartpool/avatars/{fileName}"; 

            var deletionParams = new DeletionParams(publicId);
            await _cloudinary.DestroyAsync(deletionParams);
        }
        catch (Exception)
        {
            // Ignore deletion errors for now
        }
    }

    public Task<Stream> DownloadAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Download should be done directly via HTTP URL.");
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var rawName = Path.GetFileNameWithoutExtension(fileName);
        var cleanName = System.Text.RegularExpressions.Regex.Replace(rawName, @"[^a-zA-Z0-9_\-]", "_");

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(fileName, fileStream),
            Folder = "smartpool/avatars",
            PublicId = $"{Guid.NewGuid():N}_{cleanName}"
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams);

        if (uploadResult.Error != null)
        {
            throw new Exception(uploadResult.Error.Message);
        }

        return uploadResult.SecureUrl.AbsoluteUri;
    }
}
