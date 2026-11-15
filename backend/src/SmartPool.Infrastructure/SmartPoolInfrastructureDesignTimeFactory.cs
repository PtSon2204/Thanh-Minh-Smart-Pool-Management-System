using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure;

/// <summary>
/// Design-time factory nằm trong Infrastructure để sinh migration chỉ build
/// Infrastructure + phụ thuộc (không cần build API, tránh xung đột khi dev server
/// đang giữ output của API). Chuỗi kết nối lấy từ biến môi trường
/// <c>ConnectionStrings__DefaultConnection</c>; khi sinh migration không cần kết nối
/// thật nên có thể đặt giá trị dummy nếu muốn.
/// </summary>
public sealed class SmartPoolInfrastructureDesignTimeFactory : IDesignTimeDbContextFactory<SmartPoolDbContext>
{
    public SmartPoolDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=smartpool;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<SmartPoolDbContext>()
            .UseNpgsql(connection, postgres => postgres.MigrationsAssembly(typeof(SmartPoolDbContext).Assembly.FullName))
            .Options;
        return new SmartPoolDbContext(options);
    }
}
