using System.Text.Json;
using System.Text.Json.Serialization;
using GammaRay.Core.Services.Probing;

namespace GammaRay.Core.Monitoring.Converters;

public sealed class ProbeResultConverter : JsonConverter<ProbeResult>
{
    public override ProbeResult? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject token");

        ProbeResult.CommunicationStatus? l7Status = null;
        ProbeResult.CommunicationStatus? l6Status = null;
        TimeSpan? probeDuration = null;
        string? failureComment = null;

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
                    case "l7Status":
                        l7Status = JsonSerializer.Deserialize<ProbeResult.CommunicationStatus>(ref reader, options);
                        break;
                    case "l6Status":
                        l6Status = JsonSerializer.Deserialize<ProbeResult.CommunicationStatus>(ref reader, options);
                        break;
                    case "probeDuration":
                        probeDuration = JsonSerializer.Deserialize<TimeSpan>(ref reader, options);
                        break;
                    case "failureComment":
                        failureComment = reader.GetString();
                        break;
                }
            }
        }

        if (l7Status is null)
            throw new JsonException("Missing required property: l7Status");
        if (probeDuration is null)
            throw new JsonException("Missing required property: probeDuration");

        var result = new ProbeResult(l7Status.Value, l6Status ?? ProbeResult.CommunicationStatus.Skipped, probeDuration.Value)
        {
            FailureComment = failureComment
        };

        return result;
    }

    public override void Write(Utf8JsonWriter writer, ProbeResult value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("l7Status");
        JsonSerializer.Serialize(writer, value.L7Status, options);

        writer.WritePropertyName("l6Status");
        JsonSerializer.Serialize(writer, value.L6Status, options);

        writer.WritePropertyName("probeDuration");
        JsonSerializer.Serialize(writer, value.ProbeDuration, options);

        if (value.FailureComment is not null)
        {
            writer.WritePropertyName("failureComment");
            writer.WriteStringValue(value.FailureComment);
        }

        writer.WriteEndObject();
    }
}
