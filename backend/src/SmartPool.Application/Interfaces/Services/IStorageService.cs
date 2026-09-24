namespace SmartPool.Application.Interfaces.Services
{
    /// <summary>
    /// Upload / xóa file (ảnh, video camera, v.v.).
    /// Implement ở Infrastructure/Storage.
    /// </summary>
    public interface IStorageService
    {
        Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);
        Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default);
        Task<Stream> DownloadAsync(string fileUrl, CancellationToken cancellationToken = default);
    }
}
