using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Features.Notifications.Services;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.Notifications.Commands.SendEmailNotification;

public sealed class SendEmailNotificationHandler : IRequestHandler<SendEmailNotificationCommand, SendEmailResult>
{
    private readonly IEmailService _emailService;
    private readonly IEmailHistoryStore _historyStore;
    private readonly IConfiguration _config;
    private readonly ILogger<SendEmailNotificationHandler> _logger;

    public SendEmailNotificationHandler(
        IEmailService emailService,
        IEmailHistoryStore historyStore,
        IConfiguration config,
        ILogger<SendEmailNotificationHandler> logger)
    {
        _emailService = emailService;
        _historyStore = historyStore;
        _config = config;
        _logger = logger;
    }

    public async Task<SendEmailResult> Handle(SendEmailNotificationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ToEmail))
        {
            return new SendEmailResult
            {
                Success = false,
                Message = "Địa chỉ email người nhận không được để trống."
            };
        }

        var subject = string.IsNullOrWhiteSpace(request.Subject)
            ? "[Bể bơi Thanh Minh] Thông báo từ hệ thống"
            : request.Subject;

        // Render HTML template
        var htmlBody = EmailTemplateBuilder.BuildHtml(
            request.TemplateType,
            request.RecipientName ?? "Quý khách",
            subject,
            request.Content,
            request.Metadata);

        var smtpUser = _config["EmailSettings:Username"];
        var isMock = string.IsNullOrWhiteSpace(smtpUser) || smtpUser.Contains("placeholder", StringComparison.OrdinalIgnoreCase);

        var historyItem = new EmailHistoryItem
        {
            Recipient = request.ToEmail,
            RecipientName = request.RecipientName,
            Subject = subject,
            TemplateType = request.TemplateType,
            SentAt = DateTime.UtcNow,
            HtmlBody = htmlBody,
        };

        if (isMock)
        {
            // Trong môi trường dev chưa cấu hình mật khẩu ứng dụng Gmail
            _logger.LogInformation("[DEV-SIMULATED-EMAIL] To: {To} | Subject: {Subject} | Template: {Template}",
                request.ToEmail, subject, request.TemplateType);

            historyItem.Status = "Thành công (Dev Mode)";
            await _historyStore.AddLogAsync(historyItem);

            return new SendEmailResult
            {
                Success = true,
                Message = $"Đã xử lý gửi email tự động tới {request.ToEmail} thành công (Chế độ Dev/Mô phỏng).",
                Recipient = request.ToEmail,
                Subject = subject,
                TemplateType = request.TemplateType,
                SentAt = historyItem.SentAt,
                HtmlBody = htmlBody
            };
        }

        try
        {
            await _emailService.SendAsync(request.ToEmail, subject, htmlBody, cancellationToken);
            historyItem.Status = "Thành công";
            await _historyStore.AddLogAsync(historyItem);

            return new SendEmailResult
            {
                Success = true,
                Message = $"Đã gửi email thành công tới {request.ToEmail}.",
                Recipient = request.ToEmail,
                Subject = subject,
                TemplateType = request.TemplateType,
                SentAt = historyItem.SentAt,
                HtmlBody = htmlBody
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi email tới {To}: {Msg}", request.ToEmail, ex.Message);
            historyItem.Status = "Thất bại";
            historyItem.ErrorMessage = ex.Message;
            await _historyStore.AddLogAsync(historyItem);

            return new SendEmailResult
            {
                Success = false,
                Message = $"Không thể gửi email qua SMTP: {ex.Message}",
                Recipient = request.ToEmail,
                Subject = subject,
                TemplateType = request.TemplateType,
                SentAt = historyItem.SentAt,
                HtmlBody = htmlBody
            };
        }
    }
}
