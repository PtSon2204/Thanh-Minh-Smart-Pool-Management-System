using SmartPool.Application.Features.Notifications.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Features.Notifications.Services;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.Notifications.Commands.SendEmailNotification;

public sealed class SendEmailNotificationHandler : IRequestHandler<SendEmailNotificationCommand, SendEmailResult>
{
    private readonly IEmailService _emailService;
    private readonly IEmailHistoryStore _historyStore;
    private readonly IRepository<User> _userRepo;
    private readonly IConfiguration _config;
    private readonly ILogger<SendEmailNotificationHandler> _logger;

    public SendEmailNotificationHandler(
        IEmailService emailService,
        IEmailHistoryStore historyStore,
        IRepository<User> userRepo,
        IConfiguration config,
        ILogger<SendEmailNotificationHandler> logger)
    {
        _emailService = emailService;
        _historyStore = historyStore;
        _userRepo = userRepo;
        _config = config;
        _logger = logger;
    }

    public async Task<SendEmailResult> Handle(SendEmailNotificationCommand request, CancellationToken cancellationToken)
    {
        var targetAudience = (request.TargetAudience ?? "single").ToLowerInvariant();
        List<(string Email, string Name)> recipients = new();

        if (targetAudience == "single")
        {
            var trimmedEmail = request.ToEmail?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedEmail))
            {
                return new SendEmailResult
                {
                    Success = false,
                    Message = "Địa chỉ email người nhận không được để trống."
                };
            }

            // Kiểm tra email người nhận có tồn tại trong hệ thống tài khoản bể bơi không
            var existingUser = await _userRepo.FirstOrDefaultAsync(
                u => u.IsDeleted != true && u.Email != null && u.Email.ToLower() == trimmedEmail.ToLower(),
                cancellationToken);

            if (existingUser == null)
            {
                return new SendEmailResult
                {
                    Success = false,
                    Message = $"Email '{trimmedEmail}' không tồn tại trong hệ thống tài khoản bể bơi. Vui lòng kiểm tra lại."
                };
            }

            var recipientName = !string.IsNullOrWhiteSpace(request.RecipientName)
                ? request.RecipientName
                : (existingUser.UserProfile?.FullName ?? existingUser.Username ?? "Quý khách");

            recipients.Add((trimmedEmail, recipientName));
        }
        else
        {
            var users = await _userRepo.FindAsync(u => u.IsDeleted != true && !string.IsNullOrEmpty(u.Email), cancellationToken);

            if (targetAudience == "customers")
            {
                users = users.Where(u => u.Role != null && u.Role.Name.Equals("CUSTOMER", StringComparison.OrdinalIgnoreCase)).ToList();
            }
            else if (targetAudience == "staff")
            {
                users = users.Where(u => u.Role != null && !u.Role.Name.Equals("CUSTOMER", StringComparison.OrdinalIgnoreCase)).ToList();
            }

            recipients = users
                .Where(u => !string.IsNullOrWhiteSpace(u.Email))
                .Select(u => (Email: u.Email!.Trim(), Name: u.UserProfile?.FullName ?? u.Username))
                .DistinctBy(x => x.Email, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (recipients.Count == 0)
            {
                return new SendEmailResult
                {
                    Success = false,
                    Message = "Không tìm thấy người nhận nào phù hợp với nhóm đối tượng đã chọn."
                };
            }
        }

        var subject = string.IsNullOrWhiteSpace(request.Subject)
            ? "[Bể bơi Thanh Minh] Thông báo từ hệ thống"
            : request.Subject;

        var smtpUser = _config["EmailSettings:Username"];
        var isMock = string.IsNullOrWhiteSpace(smtpUser) || smtpUser.Contains("placeholder", StringComparison.OrdinalIgnoreCase);

        int successCount = 0;
        int failCount = 0;
        string? sampleHtml = null;

        foreach (var (toEmail, recipientName) in recipients)
        {
            var htmlBody = EmailTemplateBuilder.BuildHtml(
                request.TemplateType,
                recipientName,
                subject,
                request.Content,
                request.Metadata);

            sampleHtml ??= htmlBody;

            var historyItem = new EmailHistoryItem
            {
                Recipient = toEmail,
                RecipientName = recipientName,
                Subject = subject,
                TemplateType = request.TemplateType,
                SentAt = DateTime.UtcNow,
                HtmlBody = htmlBody,
            };

            if (isMock)
            {
                _logger.LogInformation("[DEV-SIMULATED-EMAIL] To: {To} | Subject: {Subject} | Template: {Template}",
                    toEmail, subject, request.TemplateType);

                historyItem.Status = "Thành công (Dev Mode)";
                await _historyStore.AddLogAsync(historyItem);
                successCount++;
            }
            else
            {
                try
                {
                    await _emailService.SendAsync(toEmail, subject, htmlBody, cancellationToken);
                    historyItem.Status = "Thành công";
                    await _historyStore.AddLogAsync(historyItem);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi gửi email tới {To}: {Msg}", toEmail, ex.Message);
                    historyItem.Status = "Thất bại";
                    historyItem.ErrorMessage = ex.Message;
                    await _historyStore.AddLogAsync(historyItem);
                    failCount++;
                }
            }
        }

        var audienceLabel = targetAudience switch
        {
            "all" => "Toàn bộ Khách hàng & Nhân viên",
            "customers" => "Toàn bộ Khách hàng",
            "staff" => "Toàn bộ Nhân viên",
            _ => recipients[0].Email
        };

        var isMulti = targetAudience != "single";
        var message = isMulti
            ? $"Đã gửi thông báo thành công tới {successCount}/{recipients.Count} người nhận ({audienceLabel}){(isMock ? " [Chế độ Dev]" : "")}."
            : (successCount > 0
                ? $"Đã gửi email thành công tới {recipients[0].Email}."
                : $"Gửi email thất bại.");

        return new SendEmailResult
        {
            Success = successCount > 0,
            Message = message,
            Recipient = isMulti ? audienceLabel : recipients[0].Email,
            Subject = subject,
            TemplateType = request.TemplateType,
            SentAt = DateTime.UtcNow,
            HtmlBody = sampleHtml,
            TotalRecipients = recipients.Count
        };
    }
}
