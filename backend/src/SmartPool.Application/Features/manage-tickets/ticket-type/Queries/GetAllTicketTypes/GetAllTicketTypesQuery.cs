using MediatR;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Queries.GetAllTicketTypes
{
    /// <summary>
    /// Query lấy danh sách tất cả loại vé (chưa bị xóa mềm).
    /// </summary>
    public class GetAllTicketTypesQuery : IRequest<List<GetAllTicketTypesResponse>>
    {
    }
}
