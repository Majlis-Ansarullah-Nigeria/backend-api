using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManagementApi.Host.Json;

/// <summary>
/// Reads enums from either a number (2) or a name ("Dila", case-insensitive) and writes numbers,
/// so screens can send names while existing responses keep their numeric values.
/// </summary>
public sealed class LenientEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        (Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert).IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var enumType = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
        var converter = (JsonConverter)Activator.CreateInstance(typeof(LenientEnumConverter<>).MakeGenericType(enumType))!;
        return Nullable.GetUnderlyingType(typeToConvert) is null
            ? converter
            : (JsonConverter)Activator.CreateInstance(typeof(NullableConverter<>).MakeGenericType(enumType), converter)!;
    }

    private sealed class LenientEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var number))
            {
                return (T)Enum.ToObject(typeof(T), number);
            }

            if (reader.TokenType == JsonTokenType.String && reader.GetString() is { } text)
            {
                if (Enum.TryParse<T>(text, ignoreCase: true, out var value))
                {
                    return value;
                }
            }

            throw new JsonException($"Unknown value for {typeof(T).Name}.");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(Convert.ToInt32(value));
    }

    private sealed class NullableConverter<T> : JsonConverter<T?> where T : struct
    {
        private readonly JsonConverter<T> _inner;

        public NullableConverter(JsonConverter<T> inner) => _inner = inner;

        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Null ? null : _inner.Read(ref reader, typeof(T), options);

        public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
        {
            if (value is { } v) _inner.Write(writer, v, options);
            else writer.WriteNullValue();
        }
    }
}
