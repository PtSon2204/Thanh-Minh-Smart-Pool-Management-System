using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Services;

public class EmailHistoryStore : IEmailHistoryStore
{
    private readonly ConcurrentBag<EmailHistoryItem> _items = new();

    public EmailHistoryStore()
    {
        // Seed initial history item for demonstration
        _items.Add(new EmailHistoryItem
        {
            Recipient = "khachhang.demo@gmail.com",
            RecipientName = "Nguyễn Văn Tuấn",
            Subject = "[Thanh Minh Pool] Xác nhận đặt vé bơi ngày #TM-20261005",
            TemplateType = "ticket_confirmation",
            Status = "Thành công",
            SentAt = DateTime.UtcNow.AddMinutes(-35),
            HtmlBody = "<p>Mẫu xác nhận vé bơi đã gửi cho khách hàng.</p>"
        });
    }

    public Task AddLogAsync(EmailHistoryItem item)
    {
        _items.Add(item);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EmailHistoryItem>> GetHistoryAsync(int limit = 50)
    {
        var list = _items.OrderByDescending(x => x.SentAt).Take(limit).ToList();
        return Task.FromResult<IReadOnlyList<EmailHistoryItem>>(list);
    }
}
