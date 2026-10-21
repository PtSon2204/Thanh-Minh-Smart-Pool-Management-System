using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartPool.Application.Features.Notifications.Models;

public sealed class EmailHistoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Recipient { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string TemplateType { get; set; } = "ticket_confirmation";
    public string Status { get; set; } = "Thành công";
    public string? ErrorMessage { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public string HtmlBody { get; set; } = string.Empty;
}
