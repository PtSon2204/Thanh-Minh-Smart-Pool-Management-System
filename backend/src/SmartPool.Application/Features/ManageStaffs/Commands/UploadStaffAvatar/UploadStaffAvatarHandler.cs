using FluentValidation;
using FluentValidation.Results;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Commands.UploadStaffAvatar;

public sealed class UploadStaffAvatarHandler(IStaffOperations operations)
    : IRequestHandler<UploadStaffAvatarCommand, UploadStaffAvatarResponse>
{
    // Same file rules as the self-service avatar upload handler
    // (Profiles/Commands/UploadAvatar/UploadAvatarHandler.cs).
    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/jpg"];
    private const long MaxFileBytes = 5 * 1024 * 1024;

    public async Task<UploadStaffAvatarResponse> Handle(UploadStaffAvatarCommand request, CancellationToken cancellationToken)
    {
        var file = request.File;
        if (file is null || file.Length == 0)
            throw new ValidationException([new ValidationFailure("File", "Vui lòng chọn ảnh hợp lệ.")]);
        if (!AllowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            throw new ValidationException([new ValidationFailure("File", "Chỉ chấp nhận định dạng JPG hoặc PNG.")]);
        if (file.Length > MaxFileBytes)
            throw new ValidationException([new ValidationFailure("File", "Dung lượng ảnh không được vượt quá 5MB.")]);
        using var stream = file.OpenReadStream();
        var avatarUrl = await operations.SetStaffAvatarAsync(request.UserId, stream, file.FileName, file.ContentType, cancellationToken);
        return new UploadStaffAvatarResponse(avatarUrl);
    }
}
