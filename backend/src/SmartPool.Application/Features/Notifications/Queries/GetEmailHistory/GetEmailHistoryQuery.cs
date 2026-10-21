using SmartPool.Application.Features.Notifications.Models;
using System.Collections.Generic;
using MediatR;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.Notifications.Queries.GetEmailHistory;

public sealed record GetEmailHistoryQuery(int Limit = 50) : IRequest<IReadOnlyList<EmailHistoryItem>>;
