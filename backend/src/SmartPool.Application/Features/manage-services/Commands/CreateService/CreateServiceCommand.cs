using MediatR;

namespace SmartPool.Application.Features.ManageServices.Commands.CreateService
{
    public sealed class CreateServiceCommand : IRequest<CreateServiceResponse>
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int? StockQuantity { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
