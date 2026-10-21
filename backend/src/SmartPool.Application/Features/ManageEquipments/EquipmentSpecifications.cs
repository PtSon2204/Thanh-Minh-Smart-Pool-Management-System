using System.Text;
using System.Text.Json;

namespace SmartPool.Application.Features.ManageEquipments;

public static class EquipmentSpecifications
{
    public static string? ValidationError(JsonElement? specifications) => specifications.HasValue
        ? ValidateValue(specifications.Value, string.Empty) : null;

    private static string? ValidateValue(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in value.EnumerateObject())
            {
                var name = property.Name.Trim().Normalize(NormalizationForm.FormC);
                if (string.IsNullOrEmpty(name)) return "Tên thông số thiết bị không được để trống.";
                if (name.Equals("_camera", StringComparison.OrdinalIgnoreCase))
                    return "Tên thông số này dành riêng cho hệ thống. Vui lòng chọn tên khác.";
                if (!names.Add(name)) return $"Tên thông số “{name}” bị trùng.";
                var field = string.IsNullOrEmpty(path) ? name : $"{path} / {name}";
                if (property.Value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(property.Value.GetString()))
                    return $"Nhập giá trị cho thông số “{field}”.";
                var error = ValidateValue(property.Value, field);
                if (error != null) return error;
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                var field = $"{path} / Mục {++index}";
                if (item.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(item.GetString()))
                    return $"Nhập giá trị cho thông số “{field}”.";
                var error = ValidateValue(item, field);
                if (error != null) return error;
            }
        }
        return null;
    }
}
