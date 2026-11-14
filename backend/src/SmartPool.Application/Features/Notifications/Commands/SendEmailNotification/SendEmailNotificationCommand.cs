using MediatR;
using System.Collections.Generic;

namespace SmartPool.Application.Features.Notifications.Commands.SendEmailNotification;

public sealed record SendEmailNotificationCommand : IRequest<SendEmailResult>
{
    public string? ToEmail { get; init; }
    public string? RecipientName { get; init; }
    public string Subject { get; init; } = string.Empty;
    /// <summary>
    /// Các loại template: ticket_confirmation, ticket_reminder, announcement, custom
    /// </summary>
    public string TemplateType { get; init; } = "ticket_confirmation";
    /// <summary>
    /// Đối tượng nhận: "all" (toàn bộ khách hàng & nhân viên), "customers", "staff", "single" (mặc định)
    /// </summary>
    public string? TargetAudience { get; init; } = "single";
    public string? Content { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}
