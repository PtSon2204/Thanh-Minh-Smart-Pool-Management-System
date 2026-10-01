using MediatR;
using SmartPool.Application.Features.AccessControlPool.Contracts;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.AccessControlPool.Commands.ConfirmEntry
{
    public class ConfirmEntryCommand : IRequest<PoolAccessValidationResult<ConfirmEntryResponse>>
    {
        public string? Code { get; set; }
        public string? InputMode { get; set; }
        [JsonIgnore]
        public Guid OperatorId { get; set; }
    }
}
