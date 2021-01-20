using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyHome.Utils
{
    public class GenericJsonConverter : JsonConverter<object>
    {
        public override object Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
                return reader.GetBoolean();

            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetInt16(out short s))
                    return s;

                if (reader.TryGetInt32(out int i))
                    return i;

                if (reader.TryGetInt64(out long l))
                    return l;

                if (reader.TryGetDouble(out double d))
                    return d;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                if (reader.TryGetDateTime(out DateTime d))
                    return d;

                if (reader.TryGetGuid(out Guid g))
                    return g;

                return reader.GetString();
            }

            if (reader.TokenType == JsonTokenType.StartArray)
                return JsonSerializer.Deserialize<List<object>>(ref reader, options);

            if (reader.TokenType == JsonTokenType.StartObject)
                return JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options);

            throw new JsonException("Failed to read Json value");
        }

        public override void Write(Utf8JsonWriter writer, object obj, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, obj);
        }
    }
}
