using System.Collections.Generic;
using MediatR;
using SmartPool.Application.Features.Notifications.Services;

namespace SmartPool.Application.Features.Notifications.Queries.PreviewEmail;

public sealed record PreviewEmailQuery : IRequest<string>
{
    public string TemplateType { get; init; } = "ticket_confirmation";
    public string? RecipientName { get; init; }
    public string Subject { get; init; } = "[Bể bơi Thanh Minh] Xác nhận đặt vé";
    public string? Content { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}
