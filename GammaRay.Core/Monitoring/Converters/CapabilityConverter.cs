using GammaRay.Core.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GammaRay.Core.Monitoring.Converters;

public sealed class CapabilityConverter : JsonConverter<Capability>
{
    public override Capability? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject token");

        CapabilityClass? capabilityClass = null;
        Dictionary<string, string>? properties = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case "class":
                        capabilityClass = JsonSerializer.Deserialize<CapabilityClass>(ref reader, options);
                        break;
                    case "properties":
                        properties = JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options);
                        break;
                }
            }
        }

		if (capabilityClass is null)
			throw new JsonException("Missing required property: capabilityClass");
		if (properties is null)
			throw new JsonException("Missing required property: capabilityClass");

		return new Capability(capabilityClass, properties);
    }

    public override void Write(Utf8JsonWriter writer, Capability value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("class");
        JsonSerializer.Serialize(writer, value.Class, options);
        writer.WritePropertyName("properties");
        JsonSerializer.Serialize(writer, value.Properties, options);
        writer.WriteEndObject();
    }
}
