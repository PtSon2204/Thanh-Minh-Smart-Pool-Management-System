using FluentValidation;
using FluentValidation.Results;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateClassProgressNote;

public sealed class UpdateClassProgressNoteHandler(ISwimClassOperations operations, ICurrentUser currentUser)
    : IRequestHandler<UpdateClassProgressNoteCommand, ClassAttendanceResponse>
{
    public Task<ClassAttendanceResponse> Handle(UpdateClassProgressNoteCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } coachId || !currentUser.IsInRole("COACH") || currentUser.IsInRole("ADMIN"))
            throw new UnauthorizedAccessException("Chỉ Coach phụ trách buổi được ghi nhận xét.");
        if (request.ClassId == Guid.Empty || request.SessionId == Guid.Empty || request.RegistrationId == Guid.Empty)
            throw new ValidationException([new ValidationFailure("RegistrationId", "Thông tin buổi học hoặc học viên không hợp lệ.")]);
        if (request.ExpectedUpdatedAt is null)
            throw new ValidationException([new ValidationFailure("ExpectedUpdatedAt", "Cần phiên bản điểm danh để cập nhật.")]);
        var progressNote = request.ProgressNote?.Trim();
        if (string.IsNullOrEmpty(progressNote))
            throw new ValidationException([new ValidationFailure("ProgressNote", "Vui lòng nhập nhận xét.")]);
        if (progressNote.Length > 2000)
            throw new ValidationException([new ValidationFailure("ProgressNote", "Nhận xét tối đa 2000 ký tự.")]);

        request.ProgressNote = progressNote;
        return operations.UpdateClassProgressNoteAsync(request, coachId, cancellationToken);
    }
}
