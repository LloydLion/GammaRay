using GammaRay.Core.InternetAccess;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GammaRay.Core.Monitoring.Converters;

public sealed class InternetAccessPointConverter(InternetAccessPointProvider? _provider = null) : JsonConverter<InternetAccessPoint>
{
    public override InternetAccessPoint? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (_provider is null)
            throw new InvalidOperationException("Converter in Write only mode, provide provider dependency to Read");
        var name = reader.GetString();
        if (name is null)
            return null;
        return _provider.InternetAccessPoints[name];
    }

    public override void Write(Utf8JsonWriter writer, InternetAccessPoint value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Name);
	}

	public override InternetAccessPoint ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
		Read(ref reader, typeToConvert, options) ?? throw new NullReferenceException();

	public override void WriteAsPropertyName(Utf8JsonWriter writer, InternetAccessPoint value, JsonSerializerOptions options) =>
		Write(writer, value, options);
}
