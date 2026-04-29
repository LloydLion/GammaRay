using GammaRay.Core.Routing.Categorization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GammaRay.Core.Monitoring.Converters;

public sealed class EndPointCategoryConverter(EndPointCategoriesProvider? _endPointCategoriesProvider = null) : JsonConverter<EndPointCategory>
{
	public override EndPointCategory? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (_endPointCategoriesProvider is null)
			throw new InvalidOperationException("Converter in Write only mode, provide provider dependency to Read");
		var name = reader.GetString();
		if (name is null)
			return null;
		return _endPointCategoriesProvider.Categories[name];
	}

	public override void Write(Utf8JsonWriter writer, EndPointCategory value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value.Name);
	}

	public override EndPointCategory ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
		Read(ref reader, typeToConvert, options) ?? throw new NullReferenceException();

	public override void WriteAsPropertyName(Utf8JsonWriter writer, EndPointCategory value, JsonSerializerOptions options) =>
		Write(writer, value, options);
}
