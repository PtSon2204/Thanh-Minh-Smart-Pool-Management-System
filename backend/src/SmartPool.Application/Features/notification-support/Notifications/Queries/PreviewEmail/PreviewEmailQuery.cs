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

public sealed class PreviewEmailHandler : IRequestHandler<PreviewEmailQuery, string>
{
    public Task<string> Handle(PreviewEmailQuery request, CancellationToken cancellationToken)
    {
        var html = EmailTemplateBuilder.BuildHtml(
            request.TemplateType,
            request.RecipientName ?? "Nguyễn Văn A",
            request.Subject,
            request.Content,
            request.Metadata);

        return Task.FromResult(html);
    }
}
