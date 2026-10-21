using MediatR;
using System.Collections.Generic;

namespace SmartPool.Application.Features.Notifications.Commands.SendEmailNotification;

public sealed record SendEmailNotificationCommand : IRequest<SendEmailResult>
{
    public string ToEmail { get; init; } = string.Empty;
    public string? RecipientName { get; init; }
    public string Subject { get; init; } = string.Empty;
    /// <summary>
    /// Các loại template: ticket_confirmation, ticket_reminder, announcement, custom
    /// </summary>
    public string TemplateType { get; init; } = "ticket_confirmation";
    public string? Content { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}
