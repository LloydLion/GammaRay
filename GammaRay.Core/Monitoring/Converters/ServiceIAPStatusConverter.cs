using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using GammaRay.Core.Services.Probing;

namespace GammaRay.Core.Monitoring.Converters;

public sealed class ServiceIAPStatusConverter : JsonConverter<ServiceIAPStatus>
{
	public override ServiceIAPStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.Number)
			throw new JsonException("Expected Number token for ServiceIAPStatus");

		var ticks = reader.GetInt64();
		return new ServiceIAPStatus(new TimeSpan(ticks));
	}

	public override void Write(Utf8JsonWriter writer, ServiceIAPStatus value, JsonSerializerOptions options)
	{
		writer.WriteNumberValue(value.AverageProbeTime.Ticks);
	}
}
