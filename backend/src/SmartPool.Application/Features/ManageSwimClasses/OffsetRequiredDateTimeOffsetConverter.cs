using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageSwimClasses;

public sealed class OffsetRequiredDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Thời gian phải là chuỗi ISO 8601 có múi giờ.");

        var value = reader.GetString();
        var hasUtcDesignator = value?.EndsWith('Z') == true;
        var hasOffset = value is { Length: >= 6 } &&
            value[^6] is '+' or '-' && value[^3] == ':' &&
            char.IsDigit(value[^5]) && char.IsDigit(value[^4]) &&
            char.IsDigit(value[^2]) && char.IsDigit(value[^1]);

        if ((!hasUtcDesignator && !hasOffset) ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
            throw new JsonException("Thời gian phải có múi giờ, ví dụ 2026-11-01T08:00:00+07:00 hoặc 2026-11-01T01:00:00Z.");

        return parsed;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));
}
