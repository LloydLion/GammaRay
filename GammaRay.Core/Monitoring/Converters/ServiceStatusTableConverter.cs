using System.Text.Json;
using System.Text.Json.Serialization;
using GammaRay.Core.Services.Probing;
using GammaRay.Core.Services;
using GammaRay.Core.InternetAccess;

namespace GammaRay.Core.Monitoring.Converters;

public sealed class ServiceStatusTableConverter(InternetAccessPointProvider? _internetAccessPointProvider = null) : JsonConverter<ServiceStatusTable>
{
	public override ServiceStatusTable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (_internetAccessPointProvider is null)
			throw new InvalidOperationException("Converter in Write only mode, provide provider dependency to Read");

		if (reader.TokenType != JsonTokenType.StartObject)
			throw new JsonException("Expected StartObject token");

		Service? service = null;
		Dictionary<InternetAccessPoint, ServiceIAPStatus>? table = null;

		while (reader.Read())
		{
			if (reader.TokenType == JsonTokenType.EndObject)
				break;
			if (reader.TokenType == JsonTokenType.PropertyName)
			{
				var prop = reader.GetString() ?? throw new JsonException("Property name expected");
				reader.Read();
				switch (prop)
				{
					case "service":
						var svc = JsonSerializer.Deserialize<Service>(ref reader, options) ?? throw new JsonException("'service' property was null");
						service = svc;
						break;
					case "table":
						var raw = JsonSerializer.Deserialize<Dictionary<string, string>?>(ref reader, options) ?? throw new JsonException("'table' property was null");
						table = raw.ToDictionary(kv => _internetAccessPointProvider.InternetAccessPoints[kv.Key], kv => ServiceIAPStatus.Deserialize(kv.Value));
						break;
				}
			}
		}

		if (service is null || table is null)
			throw new JsonException("Missing required properties for ServiceStatusTable");

		return new ServiceStatusTable(service, table);
	}

	public override void Write(Utf8JsonWriter writer, ServiceStatusTable value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();
		writer.WritePropertyName("service");
		JsonSerializer.Serialize(writer, value.Service, options);
		writer.WritePropertyName("table");
		var dict = value.Table.ToDictionary(kv => kv.Key.Name, kv => kv.Value.Serialize());
		JsonSerializer.Serialize(writer, dict, options);
		writer.WriteEndObject();
	}
}
