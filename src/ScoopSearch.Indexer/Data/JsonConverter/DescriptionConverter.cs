using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScoopSearch.Indexer.Data.JsonConverter;

internal class DescriptionConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        var description = new List<string?>();
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                description.Add(JsonSerializer.Deserialize<string>(ref reader, options));
            }
        }

        return string.Join(" ", description.Select(_ => string.IsNullOrEmpty(_) ? Environment.NewLine : _));
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}
