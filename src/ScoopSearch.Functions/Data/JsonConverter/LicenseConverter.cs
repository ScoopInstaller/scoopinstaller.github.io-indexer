using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ScoopSearch.Functions.Data.JsonConverter
{
    internal class LicenseConverter : Newtonsoft.Json.JsonConverter
    {
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotImplementedException();

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                return reader.Value;
            }

            JObject jObject = JObject.Load(reader);
            if (jObject.TryGetValue("identifier", out JToken identifier))
            {
                return identifier.ToString();
            }
            else if (jObject.TryGetValue("url", out JToken value))
            {
                return value.ToString();
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string);
        }
    }
}
