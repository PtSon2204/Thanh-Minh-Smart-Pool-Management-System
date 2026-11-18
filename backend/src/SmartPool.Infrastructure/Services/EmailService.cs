using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using SmartPool.Application.Interfaces.Services;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SmartPool.Infrastructure.Services
{
    /// <summary>
    /// Gửi email qua Brevo HTTP API (ưu tiên khi có EmailSettings__BrevoApiKey, tránh bị Render Free chặn cổng SMTP)
    /// hoặc fallback sang MailKit SMTP cho môi trường local/VPS.
    /// </summary>
    public class EmailService : IEmailService
    {
        private const string BrevoEndpoint = "https://api.brevo.com/v3/smtp/email";

        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public EmailService(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
            => SendAsync([to], subject, htmlBody, cancellationToken);

        public async Task SendAsync(IEnumerable<string> recipients, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            var recipientList = recipients
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (recipientList.Count == 0)
                return;

            var fromName = _configuration["EmailSettings:FromName"]?.Trim()
                           ?? _configuration["EmailSettings:From"]?.Trim()
                           ?? "Thanh Minh Smart Pool";
            var fromEmail = _configuration["EmailSettings:FromEmail"]?.Trim() ?? string.Empty;

            var brevoApiKey = _configuration["EmailSettings:BrevoApiKey"]?.Trim();
            if (!string.IsNullOrWhiteSpace(brevoApiKey))
            {
                await SendViaBrevoAsync(brevoApiKey, fromName, fromEmail, recipientList, subject, htmlBody, cancellationToken);
                return;
            }

            await SendViaSmtpAsync(fromName, fromEmail, recipientList, subject, htmlBody, cancellationToken);
        }

        private async Task SendViaBrevoAsync(
            string apiKey,
            string fromName,
            string fromEmail,
            IReadOnlyList<string> recipients,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(fromEmail))
                throw new InvalidOperationException("EmailSettings:FromEmail is required when using Brevo API.");

            using var request = new HttpRequestMessage(HttpMethod.Post, BrevoEndpoint);
            request.Headers.Add("api-key", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = JsonContent.Create(new
            {
                sender = new { name = fromName, email = fromEmail },
                to = recipients.Select(email => new { email }).ToArray(),
                subject,
                htmlContent = htmlBody,
            });

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"Brevo email API failed with status {(int)response.StatusCode}: {body}");
            }
        }

        private async Task SendViaSmtpAsync(
            string fromName,
            string fromEmail,
            IReadOnlyList<string> recipients,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));

            foreach (var recipient in recipients)
                message.To.Add(MailboxAddress.Parse(recipient));

            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient { Timeout = 15_000 };
            var smtpHost = _configuration["EmailSettings:SmtpHost"]?.Trim() ?? "localhost";
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"]?.Trim() ?? "587");
            await client.ConnectAsync(
                smtpHost,
                smtpPort,
                SecureSocketOptions.StartTls,
                cancellationToken);

            var username = _configuration["EmailSettings:Username"]?.Trim() ?? string.Empty;
            var password = _configuration["EmailSettings:Password"]?.Trim() ?? string.Empty;
            await client.AuthenticateAsync(
                username,
                password,
                cancellationToken);

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
    }
}
