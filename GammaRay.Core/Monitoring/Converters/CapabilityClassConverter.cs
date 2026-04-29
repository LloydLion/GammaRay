using GammaRay.Core.Services;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GammaRay.Core.Monitoring.Converters;

public sealed class CapabilityClassConverter(CapabilityClassProvider? _provider = null) : JsonConverter<CapabilityClass>
{
    public override CapabilityClass? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (_provider is null)
            throw new InvalidOperationException("Converter in Write only mode, provide provider dependency to Read");
        var name = reader.GetString();
        if (name is null)
            return null;
        return _provider.GetClassByName(name);
    }

    public override void Write(Utf8JsonWriter writer, CapabilityClass value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Name);
    }

	public override CapabilityClass ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
		Read(ref reader, typeToConvert, options) ?? throw new NullReferenceException();

	public override void WriteAsPropertyName(Utf8JsonWriter writer, CapabilityClass value, JsonSerializerOptions options) =>
		Write(writer, value, options);
}
