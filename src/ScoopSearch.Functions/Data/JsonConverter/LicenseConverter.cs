using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScoopSearch.Functions.Data.JsonConverter
{
    internal class LicenseConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }

            using (var document = JsonDocument.ParseValue(ref reader))
            {
                if (document.RootElement.TryGetProperty("identifier", out var identifier))
                {
                    return identifier.GetString();
                }

                if (document.RootElement.TryGetProperty("url", out var value))
                {
                    return value.GetString();
                }

                throw new NotSupportedException();
            }
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) => throw new NotImplementedException();
    }
}
