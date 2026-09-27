using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Services
{
    /// <summary>
    /// Gửi email qua SMTP bằng MailKit.
    /// Cấu hình trong .env: EmailSettings__SmtpHost, EmailSettings__Username...
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
            => SendAsync([to], subject, htmlBody, cancellationToken);

        public async Task SendAsync(IEnumerable<string> recipients, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                _configuration["EmailSettings:FromName"],
                _configuration["EmailSettings:FromEmail"]));

            foreach (var recipient in recipients)
                message.To.Add(MailboxAddress.Parse(recipient));

            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            await client.ConnectAsync(
                _configuration["EmailSettings:SmtpHost"],
                int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587"),
                SecureSocketOptions.StartTls,
                cancellationToken);

            await client.AuthenticateAsync(
                _configuration["EmailSettings:Username"],
                _configuration["EmailSettings:Password"],
                cancellationToken);

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
    }
}
