using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.Profiles.Commands.UploadAvatar;

public sealed class UploadAvatarHandler : IRequestHandler<UploadAvatarCommand, IActionResult>
{
    private readonly IRepository<User> _users;
    private readonly IRepository<UserProfile> _profiles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStorageService _storageService;

    public UploadAvatarHandler(
        IRepository<User> users,
        IRepository<UserProfile> profiles,
        IUnitOfWork unitOfWork,
        IStorageService storageService)
    {
        _users = users;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
        _storageService = storageService;
    }

    public async Task<IActionResult> Handle(UploadAvatarCommand request, CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.Length == 0)
        {
            return new BadRequestObjectResult(new { message = "Vui lòng chọn ảnh hợp lệ." });
        }

        var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
        if (!allowedContentTypes.Contains(request.File.ContentType.ToLower()))
        {
            return new BadRequestObjectResult(new { message = "Chỉ chấp nhận định dạng JPG hoặc PNG." });
        }

        if (request.File.Length > 5 * 1024 * 1024)
        {
            return new BadRequestObjectResult(new { message = "Dung lượng ảnh không được vượt quá 5MB." });
        }

        var profile = await _profiles.FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);
        var user = await _users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            return new NotFoundObjectResult(new { message = "Tài khoản không tồn tại." });
        }

        // Upload to Cloudinary
        using var stream = request.File.OpenReadStream();
        var avatarUrl = await _storageService.UploadAsync(stream, request.File.FileName, request.File.ContentType, cancellationToken);

        if (profile == null)
        {
            profile = new UserProfile
            {
                UserId = request.UserId,
                FullName = user.Username ?? "User",
                AvatarUrl = avatarUrl,
                UpdatedAt = DateTime.UtcNow
            };
            await _profiles.AddAsync(profile, cancellationToken);
        }
        else
        {
            // Optional: delete old avatar from Cloudinary
            // if (!string.IsNullOrEmpty(profile.AvatarUrl)) await _storageService.DeleteAsync(profile.AvatarUrl);

            profile.AvatarUrl = avatarUrl;
            profile.UpdatedAt = DateTime.UtcNow;
            _profiles.Update(profile);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new OkObjectResult(new { message = "Cập nhật ảnh đại diện thành công.", avatarUrl });
    }
}
