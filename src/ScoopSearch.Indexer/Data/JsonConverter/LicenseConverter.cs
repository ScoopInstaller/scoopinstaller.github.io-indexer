using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScoopSearch.Indexer.Data.JsonConverter;

internal class LicenseConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            if (document.RootElement.TryGetProperty("identifier", out var identifier))
            {
                return identifier.GetString();
            }

            if (document.RootElement.TryGetProperty("url", out var value))
            {
                return value.GetString();
            }
        }

        var licenses = new List<string?>();
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                licenses.Add(this.Read(ref reader, typeToConvert, options));
            }
        }

        return string.Join(", ", licenses.Where(_ => !string.IsNullOrEmpty(_)));
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}
