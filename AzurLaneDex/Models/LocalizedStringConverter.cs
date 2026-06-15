using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzurLaneDex.Models
{
    public class LocalizedStringConverter : JsonConverter<LocalizedString>
    {
        public override LocalizedString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return new LocalizedString();

            if (reader.TokenType == JsonTokenType.String)
            {
                string value = reader.GetString();
                var result = new LocalizedString();
                result["zh-Hans"] = value ?? "";
                return result;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                return new LocalizedString();
            }

            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options);
                var result = new LocalizedString();
                if (dict != null)
                    foreach (var kv in dict)
                        result[kv.Key] = kv.Value;
                return result;
            }
            catch
            {
                reader.Skip();
                return new LocalizedString();
            }
        }

        public override void Write(Utf8JsonWriter writer, LocalizedString value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            if (value != null)
            {
                foreach (var kv in value)
                {
                    writer.WriteString(kv.Key, kv.Value);
                }
            }
            writer.WriteEndObject();
        }
    }
}