using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScoopSearch.Functions.Data.JsonConverter
{
    internal class DescriptionConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }

            string description = string.Empty;
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                while (reader.TokenType != JsonTokenType.EndArray)
                {
                    var token = reader.GetString();
                    description += string.IsNullOrEmpty(token) ? Environment.NewLine : token;
                }
            }

            return description;
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) => throw new NotImplementedException();
    }
}
