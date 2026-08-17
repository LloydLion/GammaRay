using System.Diagnostics.CodeAnalysis;

namespace GammaRay.Core.Settings.Binding.ValueParsers;

public class StringSettingsTreeValueParser : ISettingsTreeValueParser
{
	public bool CanParse(Type type, SettingsTreeAggregateBinder aggregateBinder) => typeof(string) == type;

	public SettingsTreeValueParseResult TryParse(Type type, ReadOnlySpan<char> value, SettingsTreeAggregateBinder aggregateBinder)
	{
		return SettingsTreeValueParseResult.Success(new string(value));
	}
}
