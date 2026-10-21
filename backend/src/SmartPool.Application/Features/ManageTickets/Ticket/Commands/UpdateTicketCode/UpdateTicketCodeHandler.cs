using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.UpdateTicketCode;

public class UpdateTicketCodeHandler : IRequestHandler<UpdateTicketCodeCommand, UpdateTicketCodeResponse>
{
    private readonly IRepository<Domain.Entities.Ticket> _ticketRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTicketCodeHandler(
        IRepository<Domain.Entities.Ticket> ticketRepo,
        IUnitOfWork unitOfWork)
    {
        _ticketRepo = ticketRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<UpdateTicketCodeResponse> Handle(UpdateTicketCodeCommand request, CancellationToken cancellationToken)
    {
        // 1. Tìm vé theo ID
        var ticket = await _ticketRepo.GetByIdAsync(request.TicketId, cancellationToken);
        if (ticket == null)
            return Fail("Không tìm thấy vé.");

        // 2. Kiểm tra quyền sở hữu
        if (ticket.UserId != request.UserId)
            return Fail("Bạn không có quyền chỉnh sửa vé này.");

        // 3. Chỉ cho phép sửa khi vé đang ACTIVE
        if (ticket.Status != "ACTIVE")
            return Fail("Chỉ có thể chỉnh sửa mã khi vé đang ở trạng thái ACTIVE.");

        // 4. Kiểm tra mã mới có trùng với vé khác không
        var duplicate = await _ticketRepo.FirstOrDefaultAsync(
            t => t.QrCode == request.Code && t.Id != request.TicketId,
            cancellationToken);

        if (duplicate != null)
            return Fail("Mã này đã được dùng bởi vé khác. Vui lòng chọn mã khác.");

        // 5. Cập nhật mã
        ticket.QrCode = request.Code;
        ticket.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateTicketCodeResponse
        {
            Success = true,
            Message = "Cập nhật mã vé thành công.",
            NewCode = ticket.QrCode
        };
    }

    private static UpdateTicketCodeResponse Fail(string message)
        => new() { Success = false, Message = message };
}
