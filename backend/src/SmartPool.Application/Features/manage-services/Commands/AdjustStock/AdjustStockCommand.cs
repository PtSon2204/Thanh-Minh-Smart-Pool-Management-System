using MediatR;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageServices.Commands.AdjustStock
{
    public sealed class AdjustStockCommand : IRequest<AdjustStockResponse>
    {
        [JsonIgnore]
        public Guid ServiceId { get; set; }
        public int Delta { get; set; }
        public string Note { get; set; } = string.Empty;
        [JsonIgnore]
        public Guid OperatorId { get; set; }
    }
}
