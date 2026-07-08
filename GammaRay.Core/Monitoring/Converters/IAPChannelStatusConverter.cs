using GammaRay.Core.InternetAccess.Channels;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GammaRay.Core.Monitoring.Converters;

public sealed class IAPChannelStatusConverter : JsonConverter<IAPChannelStatus>
{
	public override IAPChannelStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
			throw new JsonException("Expected StartObject token");

		TimeSpan? averageAccessTime = null;
		TimeSpan? characteristicAccessTime = null;
		double? accessChance = null;
		bool? isAvailable = null;

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
					case "averageAccessTime":
						averageAccessTime = TimeSpan.FromTicks(reader.GetInt64());
						break;
					case "characteristicAccessTime":
						characteristicAccessTime = TimeSpan.FromTicks(reader.GetInt64());
						break;
					case "accessChance":
						accessChance = reader.GetDouble();
						break;
					case "isAvailable":
						isAvailable = reader.GetBoolean();
						break;
				}
			}
		}

		if (averageAccessTime is null || characteristicAccessTime is null || accessChance is null || isAvailable is null)
			throw new JsonException("Missing required properties for IAPChannelStatus");

		return new IAPChannelStatus(characteristicAccessTime.Value, averageAccessTime.Value, accessChance.Value, isAvailable.Value);
	}

	public override void Write(Utf8JsonWriter writer, IAPChannelStatus value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();
		writer.WritePropertyName("averageAccessTime");
		writer.WriteNumberValue(value.AverageAccessTime.Ticks);

		writer.WritePropertyName("characteristicAccessTime");
		writer.WriteNumberValue(value.CharacteristicAccessTime.Ticks);

		writer.WritePropertyName("accessChance");
		writer.WriteNumberValue(value.AccessChance);

		writer.WritePropertyName("isAvailable");
		writer.WriteBooleanValue(value.IsAvailable);
		writer.WriteEndObject();
	}
}
