using System.Text.Json;
using System.Text.Json.Serialization;
using GammaRay.Core.Services.Probing;

namespace GammaRay.Core.Monitoring.Converters;

public sealed class ServiceIAPStatusConverter : JsonConverter<ServiceIAPStatus>
{
	public override ServiceIAPStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var serializedForm = reader.GetString();
		if (serializedForm is null)
			return ServiceIAPStatus.Blocked;
		return ServiceIAPStatus.Deserialize(serializedForm);
	}

	public override void Write(Utf8JsonWriter writer, ServiceIAPStatus value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value.Serialize());
	}
}
