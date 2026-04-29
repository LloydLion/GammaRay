using System.Text.Json;
using System.Text.Json.Serialization;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Routing.NetworkProfiles;
using GammaRay.Core.InternetAccess;

namespace GammaRay.Core.Monitoring.Converters;

public sealed class IAPChannelStatusConverter : JsonConverter<IAPChannelStatus>
{
	public override IAPChannelStatus? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
			throw new JsonException("Expected StartObject token");

		InternetAccessPoint? iap = null;
		IAPChannel? channel = null;
		NetworkProfile? network = null;
		TimeSpan? avg = null;

		while (reader.Read())
		{
			if (reader.TokenType == JsonTokenType.EndObject)
				break;
			if (reader.TokenType == JsonTokenType.PropertyName)
			{
				var prop = reader.GetString();
				reader.Read();
				switch (prop)
				{
					case "internetAccessPoint":
						iap = JsonSerializer.Deserialize<InternetAccessPoint>(ref reader, options);
						break;
					case "channel":
						channel = JsonSerializer.Deserialize<IAPChannel>(ref reader, options);
						break;
					case "network":
						network = JsonSerializer.Deserialize<NetworkProfile>(ref reader, options);
						break;
					case "averageAccessTime":
						avg = TimeSpan.FromTicks(reader.GetInt64());
						break;
				}
			}
		}

		if (iap is null || channel is null || network is null || avg is null)
			throw new JsonException("Missing required properties for IAPChannelStatus");

		return new IAPChannelStatus(iap, channel, network, avg.Value);
	}

	public override void Write(Utf8JsonWriter writer, IAPChannelStatus value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();
		writer.WritePropertyName("internetAccessPoint");
		JsonSerializer.Serialize(writer, value.InternetAccessPoint, options);
		writer.WritePropertyName("channel");
		JsonSerializer.Serialize(writer, value.Channel, options);
		writer.WritePropertyName("network");
		JsonSerializer.Serialize(writer, value.Network, options);
		writer.WritePropertyName("averageAccessTime");
		writer.WriteNumberValue(value.AverageAccessTime.Ticks);
		writer.WriteEndObject();
	}
}
