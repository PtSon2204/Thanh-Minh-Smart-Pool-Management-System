using System;
using System.Collections.Generic;
using System.Text;

namespace SmartPool.Application.Features.Notifications.Services;

public static class EmailTemplateBuilder
{
    private const string BrandColor = "#005f8e";
    private const string AccentColor = "#0ea5e9";
    private const string BrandName = "BỂ BƠI THÔNG MINH THANH MINH";

    public static string BuildHtml(string templateType, string recipientName, string subject, string? content, Dictionary<string, string>? meta)
    {
        meta ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var innerBody = templateType?.ToLowerInvariant() switch
        {
            "ticket_confirmation" => BuildTicketConfirmationBody(recipientName, meta),
            "ticket_reminder"     => BuildTicketReminderBody(recipientName, meta),
            "announcement"        => BuildAnnouncementBody(recipientName, content, meta),
            _                     => BuildCustomBody(recipientName, content, meta),
        };

        return WrapInLayout(subject, innerBody);
    }

    private static string WrapInLayout(string subject, string bodyContent)
    {
        return $@"<!DOCTYPE html>
<html lang=""vi"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>{subject}</title>
  <style>
    body {{ margin: 0; padding: 0; background-color: #f1f5f9; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; }}
    .container {{ max-width: 600px; margin: 30px auto; background: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0, 0, 0, 0.06); }}
    .header {{ background: linear-gradient(135deg, {BrandColor} 0%, {AccentColor} 100%); padding: 32px 24px; text-align: center; color: #ffffff; }}
    .header h1 {{ margin: 0 0 6px 0; font-size: 22px; font-weight: 700; letter-spacing: 0.5px; text-transform: uppercase; }}
    .header p {{ margin: 0; font-size: 13px; opacity: 0.9; }}
    .content {{ padding: 30px 24px; font-size: 15px; line-height: 1.6; color: #334155; }}
    .info-card {{ background: #f8fafc; border-left: 4px solid {BrandColor}; border-radius: 6px; padding: 16px 20px; margin: 20px 0; }}
    .info-row {{ display: flex; justify-content: space-between; margin-bottom: 8px; font-size: 14px; border-bottom: 1px dashed #e2e8f0; padding-bottom: 6px; }}
    .info-row:last-child {{ border-bottom: none; margin-bottom: 0; padding-bottom: 0; }}
    .info-label {{ color: #64748b; font-weight: 500; }}
    .info-value {{ color: #0f172a; font-weight: 600; text-align: right; }}
    .qr-box {{ text-align: center; margin: 25px 0 15px 0; padding: 16px; background: #f0fdf4; border: 1px dashed #86efac; border-radius: 8px; }}
    .qr-code {{ display: inline-block; padding: 10px 18px; background: #16a34a; color: #ffffff; font-weight: 700; font-size: 16px; letter-spacing: 1.5px; border-radius: 6px; }}
    .footer {{ background: #0f172a; padding: 24px; text-align: center; color: #94a3b8; font-size: 12px; line-height: 1.6; }}
    .footer strong {{ color: #e2e8f0; }}
    .tag {{ display: inline-block; padding: 4px 10px; border-radius: 999px; font-size: 12px; font-weight: 600; }}
    .tag-blue {{ background: #e0f2fe; color: #0284c7; }}
  </style>
</head>
<body>
  <div class=""container"">
    <div class=""header"">
      <h1>🏊‍♂️ {BrandName}</h1>
      <p>Không gian bơi lội chuẩn thông minh - An toàn & Hiện đại</p>
    </div>
    <div class=""content"">
      {bodyContent}
    </div>
    <div class=""footer"">
      <strong>Hệ thống quản lý Bể bơi Thông minh Thanh Minh</strong><br />
      📍 Địa chỉ: Khu thể thao Thanh Minh, TP. Hà Nội<br />
      📞 Hotline: 0988.123.456 | ✉️ Email: support@thanhminhpool.vn<br />
      <span style=""font-size: 11px; opacity: 0.7; margin-top: 8px; display: inline-block;"">Email này được gửi tự động từ hệ thống. Vui lòng không trả lời trực tiếp.</span>
    </div>
  </div>
</body>
</html>";
    }

    private static string BuildTicketConfirmationBody(string recipientName, Dictionary<string, string> meta)
    {
        meta.TryGetValue("ticketCode", out var ticketCode);
        meta.TryGetValue("ticketTypeName", out var ticketTypeName);
        meta.TryGetValue("amount", out var amount);
        meta.TryGetValue("usageDate", out var usageDate);
        meta.TryGetValue("quantity", out var quantity);

        ticketCode = string.IsNullOrWhiteSpace(ticketCode) ? "TM-" + DateTime.Now.ToString("yyyyMMdd-HHmm") : ticketCode;
        ticketTypeName = string.IsNullOrWhiteSpace(ticketTypeName) ? "Vé bơi ngày tiêu chuẩn" : ticketTypeName;
        usageDate = string.IsNullOrWhiteSpace(usageDate) ? DateTime.Now.ToString("dd/MM/yyyy") : usageDate;
        quantity = string.IsNullOrWhiteSpace(quantity) ? "1" : quantity;
        amount = string.IsNullOrWhiteSpace(amount) ? "50,000 VND" : amount;

        return $@"
      <p>Xin chào <strong>{(string.IsNullOrWhiteSpace(recipientName) ? "Quý khách" : recipientName)}</strong>,</p>
      <p>Cảm ơn bạn đã đặt vé tại <strong>{BrandName}</strong>! Đơn vé của bạn đã được thanh toán và kích hoạt thành công trên hệ thống.</p>
      
      <div class=""info-card"">
        <div class=""info-row"">
          <span class=""info-label"">Mã vé (Code):</span>
          <span class=""info-value tag tag-blue"">{ticketCode}</span>
        </div>
        <div class=""info-row"">
          <span class=""info-label"">Loại vé:</span>
          <span class=""info-value"">{ticketTypeName}</span>
        </div>
        <div class=""info-row"">
          <span class=""info-label"">Số lượng:</span>
          <span class=""info-value"">{quantity} vé</span>
        </div>
        <div class=""info-row"">
          <span class=""info-label"">Ngày áp dụng:</span>
          <span class=""info-value"">{usageDate}</span>
        </div>
        <div class=""info-row"">
          <span class=""info-label"">Tổng tiền thanh toán:</span>
          <span class=""info-value"" style=""color: #16a34a; font-size: 16px;"">{amount}</span>
        </div>
      </div>

      <div class=""qr-box"">
        <div style=""font-size: 13px; color: #15803d; margin-bottom: 8px; font-weight: 500;"">MÃ QUÉT TẠI CỔNG VÀO (POS/TURNSTILE)</div>
        <div class=""qr-code"">{ticketCode}</div>
        <div style=""font-size: 12px; color: #64748b; margin-top: 8px;"">Vui lòng xuất trình mã này tại quầy hoặc máy quét QR trước khi vào bể bơi.</div>
      </div>

      <p style=""font-size: 13px; color: #64748b; margin-top: 20px;"">
        <strong>Lưu ý khi bơi:</strong><br />
        • Mang theo đồ bơi đúng quy định và khởi động kỹ trước khi xuống nước.<br />
        • Tuân thủ hiệu lệnh của nhân viên cứu hộ tại bể.
      </p>
";
    }

    private static string BuildTicketReminderBody(string recipientName, Dictionary<string, string> meta)
    {
        meta.TryGetValue("ticketCode", out var ticketCode);
        meta.TryGetValue("expiryDate", out var expiryDate);
        meta.TryGetValue("daysRemaining", out var daysRemaining);

        ticketCode = string.IsNullOrWhiteSpace(ticketCode) ? "VBT-2026-X" : ticketCode;
        expiryDate = string.IsNullOrWhiteSpace(expiryDate) ? DateTime.Now.AddDays(3).ToString("dd/MM/yyyy") : expiryDate;
        daysRemaining = string.IsNullOrWhiteSpace(daysRemaining) ? "3" : daysRemaining;

        return $@"
      <p>Xin chào <strong>{(string.IsNullOrWhiteSpace(recipientName) ? "Quý hội viên" : recipientName)}</strong>,</p>
      <p>Hệ thống xin thông báo gói <strong>Vé bơi tháng</strong> của bạn sắp đến ngày kết thúc hiệu lực.</p>

      <div class=""info-card"" style=""border-left-color: #f59e0b;"">
        <div class=""info-row"">
          <span class=""info-label"">Mã thẻ hội viên:</span>
          <span class=""info-value"">{ticketCode}</span>
        </div>
        <div class=""info-row"">
          <span class=""info-label"">Thời hạn còn lại:</span>
          <span class=""info-value"" style=""color: #d97706; font-weight: 700;"">{daysRemaining} ngày</span>
        </div>
        <div class=""info-row"">
          <span class=""info-label"">Ngày hết hạn:</span>
          <span class=""info-value"">{expiryDate}</span>
        </div>
      </div>

      <p>Để không làm gián đoạn lịch tập luyện bơi lội, quý khách vui lòng đến quầy lễ tân hoặc đăng nhập vào hệ thống để gia hạn vé tháng sớm nhất và nhận các ưu đãi hấp dẫn.</p>
";
    }

    private static string BuildAnnouncementBody(string recipientName, string? content, Dictionary<string, string> meta)
    {
        meta.TryGetValue("announcementType", out var type);
        type = string.IsNullOrWhiteSpace(type) ? "Thông báo chung" : type;

        return $@"
      <p>Kính gửi <strong>{(string.IsNullOrWhiteSpace(recipientName) ? "Quý khách hàng" : recipientName)}</strong>,</p>
      
      <div style=""background: #eff6ff; border: 1px solid #bfdbfe; border-radius: 8px; padding: 18px; margin: 18px 0;"">
        <div style=""font-weight: 700; color: #1d4ed8; font-size: 16px; margin-bottom: 8px;"">📢 {type}</div>
        <div style=""white-space: pre-wrap; color: #334155;"">{(string.IsNullOrWhiteSpace(content) ? "Ban quản lý Bể bơi Thanh Minh xin trân trọng thông báo đến Quý khách hàng các nội dung quan trọng về lịch hoạt động và chất lượng dịch vụ." : content)}</div>
      </div>

      <p>Mọi thắc mắc hoặc cần hỗ trợ thêm, Quý khách vui lòng liên hệ hotline ban quản lý để được giải đáp nhanh nhất.</p>
";
    }

    private static string BuildCustomBody(string recipientName, string? content, Dictionary<string, string> meta)
    {
        return $@"
      <p>Kính gửi <strong>{(string.IsNullOrWhiteSpace(recipientName) ? "Quý khách hàng" : recipientName)}</strong>,</p>
      <div style=""margin: 20px 0; white-space: pre-wrap;"">{content}</div>
";
    }
}
