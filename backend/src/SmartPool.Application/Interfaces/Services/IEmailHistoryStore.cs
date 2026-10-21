using SmartPool.Application.Features.Notifications.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartPool.Application.Interfaces.Services;

public interface IEmailHistoryStore
{
    Task AddLogAsync(EmailHistoryItem item);
    Task<IReadOnlyList<EmailHistoryItem>> GetHistoryAsync(int limit = 50);
}
