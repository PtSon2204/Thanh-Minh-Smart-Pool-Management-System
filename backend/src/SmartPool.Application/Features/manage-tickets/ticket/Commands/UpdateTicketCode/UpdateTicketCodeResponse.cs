namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.UpdateTicketCode;

public class UpdateTicketCodeResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    /// <summary>Mã mới đã được lưu.</summary>
    public string? NewCode { get; set; }
}
