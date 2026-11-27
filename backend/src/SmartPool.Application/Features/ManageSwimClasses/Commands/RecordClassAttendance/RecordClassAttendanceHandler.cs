using FluentValidation;
using FluentValidation.Results;
using MediatR;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.RecordClassAttendance;

public sealed class RecordClassAttendanceHandler(ISwimClassOperations operations, ICurrentUser currentUser)
    : IRequestHandler<RecordClassAttendanceCommand, ClassAttendanceResponse>
{
    private const int MaxImageBytes = 5 * 1024 * 1024;
    public async Task<ClassAttendanceResponse> Handle(RecordClassAttendanceCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } coachId || !currentUser.IsInRole("COACH") || currentUser.IsInRole("ADMIN"))
            throw new UnauthorizedAccessException("Chỉ Coach phụ trách buổi được điểm danh.");
        if (request.ClassId == Guid.Empty || request.SessionId == Guid.Empty || request.RegistrationId == Guid.Empty ||
            request.Status is not ("Verified" or "Absent"))
            throw new ValidationException([new ValidationFailure("Status", "Dữ liệu điểm danh không hợp lệ.")]);
        if (request.Image is { } file &&
            (file.Length is < 4 or > MaxImageBytes ||
             file.ContentType is not ("image/jpeg" or "image/png")))
            throw new ValidationException([new ValidationFailure("Image", "Chỉ nhận ảnh JPG/PNG tối đa 5 MB.")]);

        byte[]? bytes = null;
        string? format = null;
        if (request.Image is { } image)
        {
            await using var stream = image.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            bytes = buffer.ToArray();
            format = bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})
                ? "png" : bytes.Length >= 4 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[^2] == 0xff && bytes[^1] == 0xd9
                    ? "jpg" : null;
            if (format is null || (format == "png" && image.ContentType != "image/png") ||
                (format == "jpg" && image.ContentType != "image/jpeg"))
                throw new ValidationException([new ValidationFailure("Image", "Nội dung ảnh không khớp JPG/PNG.")]);
        }
        if (request.Status == "Absent" && bytes is not null)
            throw new ValidationException([new ValidationFailure("Image", "Điểm danh vắng không có ảnh.")]);
        return await operations.RecordClassAttendanceAsync(request, coachId, bytes, format, cancellationToken);
    }
}
