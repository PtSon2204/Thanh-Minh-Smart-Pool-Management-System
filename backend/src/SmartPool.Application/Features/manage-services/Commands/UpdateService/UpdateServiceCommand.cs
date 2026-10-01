using MediatR;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageServices.Commands.UpdateService
{
    public sealed class UpdateServiceCommand : IRequest<UpdateServiceResponse>
    {
        [JsonIgnore]
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
    }
}
