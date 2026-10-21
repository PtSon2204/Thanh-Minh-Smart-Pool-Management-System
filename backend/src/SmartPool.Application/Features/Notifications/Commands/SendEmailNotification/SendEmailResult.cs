using MediatR;
using System.Collections.Generic;

namespace SmartPool.Application.Features.Notifications.Commands.SendEmailNotification;

public sealed record SendEmailResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Recipient { get; init; }
    public string? Subject { get; init; }
    public string? TemplateType { get; init; }
    public DateTime SentAt { get; init; } = DateTime.UtcNow;
    public string? HtmlBody { get; init; }
}
