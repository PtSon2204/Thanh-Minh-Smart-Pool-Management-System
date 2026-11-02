using System.Net;

namespace SmartPool.Infrastructure.StaffOnboarding;

public static class StaffInvitationEmail
{
    public const string Subject = "Thư mời hoàn thiện hồ sơ nhân viên | Thành Minh Smart Pool";

    public static string Body(string link) => $"""
        <div lang="vi" style="font-family:Arial,sans-serif;font-size:14px;line-height:1.7;color:#1f2937">
          <p>Kính gửi Anh/Chị,</p>
          <p>Hệ thống Thành Minh Smart Pool trân trọng mời Anh/Chị hoàn thiện hồ sơ nhân viên để tiếp tục quy trình đăng ký và kích hoạt tài khoản.</p>
          <p>Vui lòng truy cập liên kết dưới đây để bổ sung thông tin:</p>
          <p><a href="{WebUtility.HtmlEncode(link)}"><strong>Hoàn thiện hồ sơ nhân viên</strong></a></p>
          <p><strong>Lưu ý:</strong> Liên kết có hiệu lực trong vòng 24 giờ kể từ khi email được gửi và chỉ được sử dụng để hoàn thiện hồ sơ.</p>
          <p>Trân trọng,<br><strong>Thành Minh Smart Pool</strong></p>
        </div>
        """;
}
