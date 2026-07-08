using GammaRay.Core.Network.Profiles;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GammaRay.Core.Monitoring.Converters;

public sealed class NetworkProfileConverter(NetworkProfileProvider? _provider = null) : JsonConverter<NetworkProfile>
{
    public override NetworkProfile? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (_provider is null)
            throw new InvalidOperationException("Converter in Write only mode, provide provider dependency to Read");
        var name = reader.GetString();
        if (name is null)
            return null;
        return _provider.Profiles[name];
    }

    public override void Write(Utf8JsonWriter writer, NetworkProfile value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Name);
    }

	public override NetworkProfile ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
		Read(ref reader, typeToConvert, options) ?? throw new NullReferenceException();

	public override void WriteAsPropertyName(Utf8JsonWriter writer, NetworkProfile value, JsonSerializerOptions options) =>
		Write(writer, value, options);
}
