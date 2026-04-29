using System;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GammaRay.Core.Monitoring.Converters;

public sealed class IPEndPointConverter : JsonConverter<IPEndPoint>
{
	public override IPEndPoint? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var s = reader.GetString();
		if (s is null)
			return null;
		return IPEndPoint.Parse(s);
	}

	public override void Write(Utf8JsonWriter writer, IPEndPoint value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value.ToString());
	}
}
