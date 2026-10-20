using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartPool.Infrastructure.Persistence.DbContext;
using System.Text.Json;

namespace SmartPool.Infrastructure.Seeding;

/// <summary>
/// Tự động đồng bộ permissions của từng Role vào DB khi app khởi động.
///
///
/// HƯỚNG DẪN KHI THÊM TÍNH NĂNG MỚI:
///    Bước 1 — Thêm module/action vào PermissionCatalog.cs (Application layer)
///    Bước 2 — Thêm permission vào đúng role bên dưới
///    Bước 3 — Dùng [RequirePermission("module", "action")] trong Controller
///    → App tự cập nhật DB khi khởi động, không cần làm gì thêm.
/// </summary>
public static class RolePermissionSeeder
{
    // ĐỊNH NGHĨA PERMISSION CHO TỪNG ROLE
    private static readonly Dictionary<string, Dictionary<string, string[]>> RolePermissions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ADMIN"] = new(StringComparer.Ordinal)
            {
                ["profiles"]         = ["read", "update"],
                ["users"]            = ["read", "create", "update"],
                ["staff"]            = ["read", "create", "update"],
                ["shifts"]           = ["read", "create", "update"],
                ["staff_schedule"]   = ["read", "create", "delete"],
                ["salaries"]         = ["read", "create", "update"],
                ["attendance"]       = ["read", "update"],
                ["ticket_lookup"]    = ["read"],
                ["pool_entry"]       = ["read", "create"],
                ["ticket_sales"]     = ["read", "create"],
                ["ticket_types"]     = ["read", "create", "update", "delete"],
                ["services"]         = ["read", "create", "update"],
                ["inventory"]        = ["read", "update"],
                ["rentals"]          = ["read", "create", "update"],
                ["vouchers"]         = ["read", "create", "update", "delete"],
                ["payments"]         = ["read"],
                ["reports"]          = ["read"],
                ["notifications"]    = ["read", "create", "update"],
            },

            ["LIFEGUARD"] = new(StringComparer.Ordinal)
            {
                ["profiles"]         = ["read", "update"],
                ["staff_schedule"]   = ["read"],
                ["attendance"]       = ["read", "update"],
            },

            ["SALES"] = new(StringComparer.Ordinal)
            {
                ["profiles"]         = ["read", "update"],
                ["staff_schedule"]   = ["read"],
                ["attendance"]       = ["read", "update"],
                ["ticket_lookup"]    = ["read"],
                ["pool_entry"]       = ["read", "create"],
                ["ticket_sales"]     = ["read", "create"],
                ["ticket_types"]     = ["read"],
                ["services"]         = ["read"],
                ["inventory"]        = ["read", "update"],
                ["rentals"]          = ["read", "create", "update"],
                ["vouchers"]         = ["read"],
            },

            ["CUSTOMER"] = new(StringComparer.Ordinal)
            {
                ["profiles"]         = ["read", "update"],
                ["customer_tickets"] = ["read", "create", "update"],
                ["ticket_types"]     = ["read"],
                ["vouchers"]         = ["read"],
            },
        };

    /// <summary>
    /// Chạy seeder: đọc roles từ DB, merge permission còn thiếu vào, lưu lại.
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db     = scope.ServiceProvider.GetRequiredService<SmartPoolDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SmartPoolDbContext>>();

        logger.LogInformation("[RolePermissionSeeder] Bắt đầu đồng bộ permissions...");

        var roles = await db.Roles
            .Where(r => r.IsDeleted != true)
            .ToListAsync(ct);

        int totalUpdated = 0;

        foreach (var (roleName, desiredModules) in RolePermissions)
        {
            var role = roles.FirstOrDefault(r =>
                string.Equals(r.Name.Trim(), roleName, StringComparison.OrdinalIgnoreCase));

            if (role is null)
            {
                logger.LogWarning("[RolePermissionSeeder] Role '{Role}' chưa có trong DB — bỏ qua.", roleName);
                continue;
            }

            // Parse JSON hiện tại từ DB
            var current = ParsePermissions(role.Permissions);

            // Merge: chỉ thêm action còn thiếu, giữ nguyên những gì đã có
            bool changed = false;
            if (roleName is "LIFEGUARD" or "SALES")
                changed = current.Remove("shifts");

            foreach (var (module, desiredActions) in desiredModules)
            {
                if (!current.TryGetValue(module, out var existingActions))
                {
                    current[module] = desiredActions.ToHashSet();
                    changed = true;
                }
                else
                {
                    foreach (var action in desiredActions)
                    {
                        if (existingActions.Add(action))
                            changed = true;
                    }
                }
            }

            if (changed)
            {
                // Serialize về JSONB format: { "module": ["action1", "action2"] }
                role.Permissions = SerializePermissions(current);
                role.UpdatedAt   = DateTime.UtcNow;
                totalUpdated++;
                logger.LogInformation("[RolePermissionSeeder] Role '{Role}' → đã cập nhật permissions.", roleName);
            }
        }

        if (totalUpdated > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("[RolePermissionSeeder] Hoàn thành — cập nhật {Count} role(s).", totalUpdated);
        }
        else
        {
            logger.LogInformation("[RolePermissionSeeder] Hoàn thành — không có thay đổi nào cần cập nhật.");
        }
    }

    /// <summary>Parse JSON string từ DB thành Dictionary.</summary>
    private static Dictionary<string, HashSet<string>> ParsePermissions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        try
        {
            var doc = JsonDocument.Parse(json);
            var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var actions = new HashSet<string>(StringComparer.Ordinal);
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in prop.Value.EnumerateArray())
                        if (item.GetString() is { } s)
                            actions.Add(s);
                }
                result[prop.Name] = actions;
            }
            return result;
        }
        catch
        {
            return new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        }
    }

    /// <summary>Serialize Dictionary thành JSON string để lưu vào DB.</summary>
    private static string SerializePermissions(Dictionary<string, HashSet<string>> permissions)
    {
        var ordered = permissions
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ToDictionary(kv => kv.Key, kv => kv.Value.OrderBy(a => a).ToArray());

        return JsonSerializer.Serialize(ordered);
    }
}
