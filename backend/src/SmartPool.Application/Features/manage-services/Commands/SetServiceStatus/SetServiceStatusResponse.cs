namespace SmartPool.Application.Features.ManageServices.Commands.SetServiceStatus;

public sealed class SetServiceStatusResponse
{
    public Guid Id { get; set; }

    public bool IsActive { get; set; }

    public DateTime UpdatedAt { get; set; }
}
