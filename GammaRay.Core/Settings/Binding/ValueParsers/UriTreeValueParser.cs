namespace GammaRay.Core.Settings.Binding.ValueParsers;

public class UriTreeValueParser : ISettingsTreeValueParser
{
	public bool CanParse(Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		return type == typeof(Uri);
	}

	public SettingsTreeValueParseResult TryParse(Type type, ReadOnlySpan<char> value, SettingsTreeAggregateBinder aggregateBinder)
	{
		if (Uri.TryCreate(new string(value), default, out var result))
			return SettingsTreeValueParseResult.Success(result);
		return SettingsTreeValueParseResult.Failure("URI must have valid format");
	}
}
