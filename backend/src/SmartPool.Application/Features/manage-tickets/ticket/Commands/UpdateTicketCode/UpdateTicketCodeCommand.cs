using MediatR;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.UpdateTicketCode;

public class UpdateTicketCodeCommand : IRequest<UpdateTicketCodeResponse>
{
    /// <summary>ID của vé cần cập nhật mã — set bởi Controller từ JWT, không nhận từ body.</summary>
    [JsonIgnore]
    public Guid TicketId { get; set; }

    /// <summary>ID chủ vé — set bởi Controller từ JWT, không nhận từ body.</summary>
    [JsonIgnore]
    public Guid UserId { get; set; }

    /// <summary>Mã mới người dùng muốn đặt — đúng 6 chữ số.</summary>
    public string Code { get; set; } = string.Empty;
}
