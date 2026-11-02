using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.StaffOnboarding;

public sealed record StaffStoredAsset(string AssetId, string Version);

public interface IStaffDocumentStorage
{
    Task<StaffStoredAsset> UploadAsync(StaffOnboardingDocument document, byte[] bytes, CancellationToken cancellationToken);
    Task<byte[]> ReadAsync(StaffOnboardingDocument document, CancellationToken cancellationToken);
    Task DeleteAsync(StaffOnboardingDocument document, CancellationToken cancellationToken);
    string AvatarUrl(StaffOnboardingDocument document);
}

public sealed class CloudinaryStaffDocumentStorage(Cloudinary cloudinary, HttpClient http) : IStaffDocumentStorage
{
    private static void RequirePrivateStorage(StaffOnboardingDocument document)
    {
        if (document.Purpose != "AVATAR" && document.DeliveryType != "authenticated")
            throw new StaffOnboardingException("INVALID_DOCUMENT_DELIVERY_TYPE", 400);
    }

    public async Task<StaffStoredAsset> UploadAsync(StaffOnboardingDocument document, byte[] bytes, CancellationToken cancellationToken)
    {
        RequirePrivateStorage(document);
        using var stream = new MemoryStream(bytes, false);
        var result = await cloudinary.UploadAsync(new ImageUploadParams { File = new FileDescription("image", stream),
            PublicId = document.PublicId, Type = document.DeliveryType, Overwrite = false,
            Format = document.ContentType == "image/png" ? "png" : "jpg" }, cancellationToken);
        if (result.Error is not null || result.PublicId != document.PublicId || result.Type != document.DeliveryType)
            throw new StaffOnboardingException("DOCUMENT_UPLOAD_FAILED", 503);
        return new(result.AssetId, result.Version);
    }

    public async Task<byte[]> ReadAsync(StaffOnboardingDocument document, CancellationToken cancellationToken)
    {
        RequirePrivateStorage(document);
        var url = cloudinary.DownloadPrivate(document.PublicId!, type: document.DeliveryType,
            format: document.ContentType == "image/png" ? "png" : "jpg", expiresAt: DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds());
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 5 * 1024 * 1024)
            throw new StaffOnboardingException("DOCUMENT_UNAVAILABLE", 503);
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) != 0)
        {
            if (output.Length + read > 5 * 1024 * 1024) throw new StaffOnboardingException("DOCUMENT_UNAVAILABLE", 503);
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    public async Task DeleteAsync(StaffOnboardingDocument document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await cloudinary.DestroyAsync(new DeletionParams(document.PublicId!)
            { Type = document.DeliveryType, ResourceType = ResourceType.Image, Invalidate = true });
        if (result.Error is not null || result.Result is not ("ok" or "not found"))
            throw new StaffOnboardingException("DOCUMENT_DELETE_FAILED", 503);
    }

    public string AvatarUrl(StaffOnboardingDocument document) => document.Purpose == "AVATAR" && document.DeliveryType == "upload"
        ? cloudinary.Api.UrlImgUp.Secure(true).Version(document.AssetVersion).BuildUrl(document.PublicId)
        : throw new StaffOnboardingException("INVALID_AVATAR", 400);
}
