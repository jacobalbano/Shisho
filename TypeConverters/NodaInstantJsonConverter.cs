using Microsoft.Data.Sqlite;
using NodaTime;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shisho.TypeConverters;

public class NodaInstantJsonConverter : JsonConverter<Instant>
{
    public override Instant Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Instant.FromUnixTimeTicks(reader.GetInt64());
    }

    public override void Write(Utf8JsonWriter writer, Instant value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.ToUnixTimeTicks());
    }
}

