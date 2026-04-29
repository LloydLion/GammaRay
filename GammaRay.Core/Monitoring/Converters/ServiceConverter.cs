using GammaRay.Core.Network;
using GammaRay.Core.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GammaRay.Core.Monitoring.Converters;

public sealed class ServiceConverter : JsonConverter<Service>
{
    public override Service? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject token");

        WebEndPoint? endPoint = null;
        Capability? capability = null;

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
                    case "endPoint":
                        endPoint = JsonSerializer.Deserialize<WebEndPoint>(ref reader, options);
                        break;
                    case "capability":
                        capability = JsonSerializer.Deserialize<Capability>(ref reader, options);
                        break;
                }
            }
        }

        if (endPoint is null || capability is null)
            throw new JsonException("Missing required properties: endPoint, capability");

        return new Service(endPoint.Value, capability);
    }

    public override void Write(Utf8JsonWriter writer, Service value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("endPoint");
        JsonSerializer.Serialize(writer, value.EndPoint, options);
        writer.WritePropertyName("capability");
        JsonSerializer.Serialize(writer, value.Capability, options);
        writer.WriteEndObject();
    }
}
