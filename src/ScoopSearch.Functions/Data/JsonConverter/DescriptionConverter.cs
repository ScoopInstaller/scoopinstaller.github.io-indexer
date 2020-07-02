using System;
using Newtonsoft.Json;

namespace ScoopSearch.Functions.Data.JsonConverter
{
    internal class DescriptionConverter : Newtonsoft.Json.JsonConverter
    {
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotImplementedException();

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                return reader.Value;
            }

            string description = string.Empty;
            if (reader.TokenType == JsonToken.StartArray)
            {
                while (reader.TokenType != JsonToken.EndArray)
                {
                    reader.Read();
                    description += string.IsNullOrEmpty((string)reader.Value) ? Environment.NewLine : reader.Value;
                }
            }

            return description;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string);
        }
    }
}
