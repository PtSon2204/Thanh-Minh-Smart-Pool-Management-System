using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SmartPool.Application.Features.Notifications.Models;

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
            "event_promo"         => BuildEventPromoBody(recipientName, content, meta),
            _                     => BuildCustomBody(recipientName, content, meta),
        };

        return WrapInLayout(subject, innerBody);
    }

    /// <summary>
    /// Tạo toàn bộ nội dung HTML email xác nhận đơn hàng vé bơi hoàn chỉnh (hỗ trợ cả đơn 1 vé và nhiều vé).
    /// </summary>
    public static string BuildOrderTicketConfirmationHtml(
        string recipientName,
        string subject,
        string orderRef,
        decimal totalAmount,
        decimal discountAmount,
        decimal finalAmount,
        DateTime paymentTimeUtc,
        IReadOnlyList<TicketEmailItemDto> tickets,
        string myTicketsUrl = "https://thanhminhpool.vn/the-cua-toi")
    {
        var localTime = paymentTimeUtc.AddHours(7); // Giờ Việt Nam (UTC+7)
        var timeFormatted = localTime.ToString("HH:mm - dd/MM/yyyy", CultureInfo.InvariantCulture);

        var sb = new StringBuilder();
        sb.Append($@"
      <p style=""margin-top:0;"">Xin chào <strong>{(string.IsNullOrWhiteSpace(recipientName) ? "Quý khách" : recipientName)}</strong>,</p>
      <p>Cảm ơn bạn đã mua vé bơi tại <strong>{BrandName}</strong>! Đơn hàng của bạn đã được thanh toán và kích hoạt thành công trên hệ thống.</p>
      
      <div class=""order-summary-box"">
        <div style=""display:flex; justify-content:space-between; align-items:center; margin-bottom:8px; border-bottom:1px solid #e2e8f0; padding-bottom:8px;"">
          <span style=""font-weight:700; color:#0f172a; font-size:15px;"">Mã đơn hàng: <span style=""color:#005f8e;"">{orderRef}</span></span>
          <span class=""status-badge"">Đã thanh toán</span>
        </div>
        <div style=""font-size:13px; color:#64748b;"">Thời gian thanh toán: <strong>{timeFormatted}</strong></div>
      </div>

      <div style=""margin: 24px 0 12px 0;"">
        <h3 style=""margin:0 0 12px 0; color:#0f172a; font-size:16px; display:flex; align-items:center; gap:6px;"">
          DANH SÁCH VÉ BƠI ({tickets.Count} vé)
        </h3>
        <p style=""font-size:13px; color:#64748b; margin:0 0 16px 0;"">Quý khách vui lòng xuất trình mã 6 số này tại quầy hoặc máy quét QR trước khi vào bể bơi.</p>
      </div>
");

        foreach (var ticket in tickets)
        {
            var isVeLuot = string.Equals(ticket.Category, "VE_LUOT", StringComparison.OrdinalIgnoreCase);
            var categoryBadge = isVeLuot ? "tag-teal" : "tag-blue";
            var categoryName = isVeLuot ? "Vé lượt" : "Vé tháng";

            sb.Append($@"
      <div class=""ticket-card"">
        <div style=""display:flex; justify-content:space-between; align-items:flex-start; margin-bottom:10px;"">
          <div>
            <div style=""font-weight:700; font-size:16px; color:#0f172a;"">{ticket.TicketTypeName}</div>
            <div style=""font-size:13px; color:#475569; margin-top:4px;"">{ticket.Details}</div>
          </div>
          <span class=""tag {categoryBadge}"">{categoryName}</span>
        </div>
        <div class=""ticket-code-wrapper"">
          <div style=""font-size:11px; color:#0f766e; font-weight:700; text-transform:uppercase; letter-spacing:1px; margin-bottom:8px;"">MÃ VÉ QUÉT CỔNG (6 SỐ)</div>
          <div style=""text-align:center; margin:0 0 10px 0;"">
            <img src=""https://api.qrserver.com/v1/create-qr-code/?size=180x180&data={ticket.TicketCode}""
                 alt=""Ma QR ve {ticket.TicketCode}""
                 width=""150"" height=""150""
                 style=""display:inline-block; border:1px solid #cbd5e1; border-radius:8px; padding:6px; background:#ffffff;"" />
          </div>
          <div class=""ticket-code-val"">{ticket.TicketCode}</div>
          <div style=""font-size:11px; color:#64748b; margin-top:4px;"">Có thể đọc mã này cho thu ngân hoặc quét QR trên web/app</div>
        </div>
      </div>
");
        }

        sb.Append($@"
      <div class=""pricing-card"">
        <div class=""info-row"">
          <span class=""info-label"">Tạm tính:</span>
          <span class=""info-value"">{totalAmount:N0} đ</span>
        </div>
");

        if (discountAmount > 0)
        {
            sb.Append($@"
        <div class=""info-row"">
          <span class=""info-label"">Giảm giá (Voucher):</span>
          <span class=""info-value"" style=""color:#ea580c;"">-{discountAmount:N0} đ</span>
        </div>
");
        }

        sb.Append($@"
        <div class=""info-row"" style=""border-top:2px solid #cbd5e1; padding-top:10px; margin-top:6px;"">
          <span class=""info-label"" style=""font-size:15px; font-weight:700; color:#0f172a;"">Tổng tiền đã thanh toán:</span>
          <span class=""info-value"" style=""color:#16a34a; font-size:18px; font-weight:800;"">{finalAmount:N0} đ</span>
        </div>
      </div>

      <div style=""text-align:center; margin:24px 0 20px 0;"">
        <a href=""{myTicketsUrl}"" target=""_blank""
           style=""display:inline-block; background:#005f8e; color:#ffffff; padding:12px 28px; font-size:14px; font-weight:700; text-decoration:none; border-radius:6px;"">
          Truy cap trang Ve cua toi tren Website
        </a>
      </div>
      <p style=""font-size:12px; color:#94a3b8; text-align:center; margin:4px 0 16px 0;"">
        Hoac truy cap: <a href=""{myTicketsUrl}"" style=""color:#005f8e;"">{myTicketsUrl}</a>
      </p>

      <div style=""background:#f8fafc; border:1px solid #e2e8f0; border-radius:8px; padding:14px 16px; margin-top:20px; font-size:13px; color:#475569; line-height:1.5;"">
        <strong style=""color:#0f172a;"">Lưu ý quan trọng khi vào bể:</strong>
        <ul style=""margin:6px 0 0 0; padding-left:18px;"">
          <li><strong>Bảo mật:</strong> Đối với vé nhiều lượt, mã vé sẽ tự động đổi mới sau mỗi lần quét vào bể để bảo vệ số lượt của bạn.</li>
          <li><strong>Quản lý vé:</strong> Bạn có thể mở mục <em>""Thẻ của tôi""</em> trên website để hiển thị mã QR trực tiếp bất cứ lúc nào.</li>
          <li><strong>Trang phục:</strong> Quý khách vui lòng mang đồ bơi đúng quy định và khởi động kỹ trước khi xuống nước.</li>
        </ul>
      </div>
");

        return WrapInLayout(subject, sb.ToString());
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
    .header {{ background: linear-gradient(135deg, {BrandColor} 0%, {AccentColor} 100%); padding: 30px 24px; text-align: center; color: #ffffff; }}
    .header h1 {{ margin: 0 0 6px 0; font-size: 20px; font-weight: 700; letter-spacing: 0.5px; text-transform: uppercase; }}
    .header p {{ margin: 0; font-size: 13px; opacity: 0.9; }}
    .content {{ padding: 26px 22px; font-size: 14px; line-height: 1.6; color: #334155; }}
    
    .order-summary-box {{ background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 14px 16px; margin: 16px 0 20px 0; }}
    .status-badge {{ background: #dcfce7; color: #15803d; font-size: 12px; font-weight: 700; padding: 3px 8px; border-radius: 6px; }}
    
    .ticket-card {{ background: #ffffff; border: 1px solid #cbd5e1; border-left: 4px solid {BrandColor}; border-radius: 8px; padding: 14px 16px; margin-bottom: 14px; box-shadow: 0 2px 4px rgba(0,0,0,0.02); }}
    .ticket-code-wrapper {{ background: #f0fdfa; border: 1px dashed #5eead4; border-radius: 8px; padding: 12px; text-align: center; margin-top: 10px; }}
    .ticket-code-val {{ display: inline-block; font-family: 'Consolas', 'Courier New', monospace; font-size: 24px; font-weight: 800; letter-spacing: 6px; color: #0d9488; }}
    
    .pricing-card {{ background: #f8fafc; border-radius: 8px; padding: 14px 16px; margin: 20px 0; border: 1px solid #e2e8f0; }}
    .info-row {{ display: flex; justify-content: space-between; margin-bottom: 8px; font-size: 14px; }}
    .info-row:last-child {{ margin-bottom: 0; }}
    .info-label {{ color: #64748b; font-weight: 500; }}
    .info-value {{ color: #0f172a; font-weight: 600; text-align: right; }}

    .footer {{ background: #0f172a; padding: 22px; text-align: center; color: #94a3b8; font-size: 12px; line-height: 1.6; }}
    .footer strong {{ color: #e2e8f0; }}
    .tag {{ display: inline-block; padding: 3px 8px; border-radius: 6px; font-size: 11px; font-weight: 700; }}
    .tag-blue {{ background: #e0f2fe; color: #0284c7; }}
    .tag-teal {{ background: #ccfbf1; color: #0f766e; }}
  </style>
</head>
<body>
  <div class=""container"">
    <div class=""header"">
      <h1>{BrandName}</h1>
      <p>Không gian bơi lội chuẩn thông minh - An toàn & Hiện đại</p>
    </div>
    <div class=""content"">
      {bodyContent}
    </div>
    <div class=""footer"">
      <strong>Hệ thống quản lý Bể bơi Thông minh Thanh Minh</strong><br />
      Địa chỉ: Khu thể thao Thanh Minh, TP. Hà Nội<br />
      Hotline: 0988.123.456 | Email: support@thanhminhpool.vn<br />
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
      
      <div style=""background:#f8fafc; border-left:4px solid {BrandColor}; border-radius:6px; padding:16px 20px; margin:20px 0;"">
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

      <div style=""text-align:center; margin:20px 0 15px 0; padding:16px; background:#f0fdf4; border:1px dashed #86efac; border-radius:8px;"">
        <div style=""font-size: 13px; color: #15803d; margin-bottom: 8px; font-weight: 500;"">MÃ QUÉT TẠI CỔNG VÀO (POS/TURNSTILE)</div>
        <div style=""display:inline-block; font-family:monospace; font-size:24px; font-weight:800; letter-spacing:6px; color:#16a34a; padding:6px 14px; background:#ffffff; border-radius:6px;"">{ticketCode}</div>
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

      <div style=""background:#f8fafc; border-left:4px solid #f59e0b; border-radius:6px; padding:16px 20px; margin:20px 0;"">
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
        <div style=""font-weight: 700; color: #1d4ed8; font-size: 16px; margin-bottom: 8px;"">{type}</div>
        <div style=""white-space: pre-wrap; color: #334155;"">{(string.IsNullOrWhiteSpace(content) ? "Ban quản lý Bể bơi Thanh Minh xin trân trọng thông báo đến Quý khách hàng các nội dung quan trọng về lịch hoạt động và chất lượng dịch vụ." : content)}</div>
      </div>

      <p>Mọi thắc mắc hoặc cần hỗ trợ thêm, Quý khách vui lòng liên hệ hotline ban quản lý để được giải đáp nhanh nhất.</p>
";
    }

    private static string BuildEventPromoBody(string recipientName, string? content, Dictionary<string, string> meta)
    {
        meta.TryGetValue("promoTitle", out var title);
        title = string.IsNullOrWhiteSpace(title) ? "SỰ KIỆN & ƯU ĐÃI ĐẶC BIỆT" : title;

        return $@"
      <p>Kính gửi <strong>{(string.IsNullOrWhiteSpace(recipientName) ? "Quý hội viên" : recipientName)}</strong>,</p>
      
      <div style=""background: #fdf2f8; border: 1px solid #fbcfe8; border-radius: 8px; padding: 18px; margin: 18px 0;"">
        <div style=""font-weight: 700; color: #db2777; font-size: 16px; margin-bottom: 8px;"">🎉 {title}</div>
        <div style=""white-space: pre-wrap; color: #334155;"">{(string.IsNullOrWhiteSpace(content) ? "Ban quản lý trân trọng gửi tới Quý khách thông tin sự kiện & ưu đãi hấp dẫn tại bể bơi." : content)}</div>
      </div>

      <p>Kính chúc Quý khách có những phút giây bơi lội thư giãn và tràn đầy năng lượng!</p>
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
