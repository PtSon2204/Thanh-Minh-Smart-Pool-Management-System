using FluentValidation;
using FluentValidation.Results;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.SubmitClassAttendance;

public sealed class SubmitClassAttendanceHandler(
    ISwimClassOperations operations,
    ICurrentUser currentUser,
    IValidator<SubmitClassAttendanceCommand> validator,
    TimeProvider timeProvider)
    : IRequestHandler<SubmitClassAttendanceCommand, ClassAttendanceResponse>
{
    public async Task<ClassAttendanceResponse> Handle(SubmitClassAttendanceCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } customerUserId ||
            !currentUser.IsInRole("CUSTOMER") || currentUser.IsInRole("ADMIN"))
            throw new UnauthorizedAccessException("Chỉ Customer được nộp ảnh điểm danh của mình.");

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var submission = await operations.GetAttendanceSubmissionContextAsync(
            request.ClassId, request.SessionId, customerUserId, cancellationToken);
        ClassAttendanceSubmissionPolicy.EnsureAllowed(submission,
            timeProvider.GetUtcNow().UtcDateTime);

        await using var stream = request.Image!.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var format = DetectFormat(bytes);
        if (bytes.Length > SubmitClassAttendanceValidator.MaxImageBytes || format is null ||
            (format == "jpg" && request.Image.ContentType != "image/jpeg") ||
            (format == "png" && request.Image.ContentType != "image/png"))
            throw new ValidationException([new ValidationFailure("Image",
                "Nội dung ảnh không khớp định dạng JPG/PNG hoặc vượt quá 5 MB.")]);

        return await operations.SubmitAttendanceAsync(request.ClassId, request.SessionId,
            customerUserId, bytes, format, cancellationToken);
    }

    private static string? DetectFormat(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(
                new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "png";
        return bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8 &&
               bytes[^2] == 0xFF && bytes[^1] == 0xD9 ? "jpg" : null;
    }
}
