using GammaRay.Core.InternetAccess;
using GammaRay.Core.InternetAccess.Channels;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GammaRay.Core.Monitoring.Converters;

public sealed class NamedIAPChannelConverter(InternetAccessPointProvider? _internetAccessPointProvider = null) : JsonConverter<NamedIAPChannel>
{
	public override NamedIAPChannel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (_internetAccessPointProvider is null)
			throw new InvalidOperationException("Converter in Write only mode, provide provider dependency to Read");

		if (reader.TokenType != JsonTokenType.StartArray)
			throw new JsonException("Expected StartArray token");

		string? IAPName = null, channelName = null;

		while (reader.Read())
		{
			if (reader.TokenType == JsonTokenType.EndArray)
				break;
			if (reader.TokenType == JsonTokenType.String)
			{
				var value = reader.GetString();
				if (IAPName is null)
					IAPName = value;
				else
				{
					channelName = value;
					break;
				}
			}
		}

		if (IAPName is null || channelName is null)
			throw new JsonException("Missing required properties for NamedIAPChannel");

		var IAP = _internetAccessPointProvider.InternetAccessPoints[IAPName];
		var channel = IAP.Channels[channelName];
		return new NamedIAPChannel(IAP, channelName, channel);
	}

	public override void Write(Utf8JsonWriter writer, NamedIAPChannel value, JsonSerializerOptions options)
	{
		writer.WriteStartArray();
		writer.WriteStringValue(value.IAP.Name);
		writer.WriteStringValue(value.ChannelName);
		writer.WriteEndArray();
	}

	public override NamedIAPChannel ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
		Read(ref reader, typeToConvert, options);

	public override void WriteAsPropertyName(Utf8JsonWriter writer, NamedIAPChannel value, JsonSerializerOptions options) =>
		Write(writer, value, options);
}
