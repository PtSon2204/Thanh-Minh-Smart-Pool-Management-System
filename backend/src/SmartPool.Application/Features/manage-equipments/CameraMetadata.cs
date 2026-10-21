using System.Text.Json;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageEquipments;

public static class CameraMetadata
{
    public static bool IsCamera(string? category) => string.Equals(category?.Trim(), "Camera", StringComparison.OrdinalIgnoreCase);

    public static bool ContainsConnectionCredentials(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
            return element.EnumerateObject().Any(property => property.Name.Equals("password", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Equals("credentials", StringComparison.OrdinalIgnoreCase) || property.Name.Equals("authorization", StringComparison.OrdinalIgnoreCase) ||
                ContainsConnectionCredentials(property.Value));
        if (element.ValueKind == JsonValueKind.Array) return element.EnumerateArray().Any(ContainsConnectionCredentials);
        return element.ValueKind == JsonValueKind.String && Uri.TryCreate(element.GetString(), UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.UserInfo);
    }

    public static string Pack(ICameraRegistration registration, string? specifications) => JsonSerializer.Serialize(new
    {
        _camera = new { version = 1, identity = registration.Identity, protectedConfiguration = registration.ProtectedConfiguration },
        specifications = specifications == null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(specifications)
    });

    // Never serialize the stored connection envelope through equipment DTOs.
    public static string? PublicSpecifications(string? metadata)
    {
        if (metadata == null) return null;
        using var document = JsonDocument.Parse(metadata);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("_camera", out _)) return metadata;
        return root.TryGetProperty("specifications", out var specs) && specs.ValueKind != JsonValueKind.Null ? specs.GetRawText() : null;
    }

    public static string? UpdateSpecifications(string? metadata, JsonElement? specifications)
    {
        var value = specifications is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } element
            ? element : (JsonElement?)null;
        if (metadata == null) return value?.GetRawText();
        using var document = JsonDocument.Parse(metadata);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("_camera", out var camera))
            return value?.GetRawText();
        return JsonSerializer.Serialize(new { _camera = camera, specifications = value });
    }

    public static bool TryGetProtectedConfiguration(string? metadata, out string configuration)
    {
        configuration = string.Empty;
        if (string.IsNullOrEmpty(metadata)) return false;
        try
        {
            using var document = JsonDocument.Parse(metadata);
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("_camera", out var camera) && camera.ValueKind == JsonValueKind.Object &&
                camera.TryGetProperty("protectedConfiguration", out var value) && value.ValueKind == JsonValueKind.String)
            {
                configuration = value.GetString()!;
                return true;
            }
        }
        catch (JsonException) { }
        return false;
    }
}
