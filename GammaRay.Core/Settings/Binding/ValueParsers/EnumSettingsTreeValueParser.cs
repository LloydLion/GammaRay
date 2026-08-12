using System.Diagnostics.CodeAnalysis;

namespace GammaRay.Core.Settings.Binding.ValueParsers;

public class EnumSettingsTreeValueParser : ISettingsTreeValueParser
{
	public bool CanParse(Type type, SettingsTreeAggregateBinder aggregateBinder) => type.IsEnum;

	public bool TryParse(Type type, ReadOnlySpan<char> value, SettingsTreeAggregateBinder aggregateBinder, [NotNullWhen(true)] out object? result)
	{
		result = Enum.Parse(type, value, ignoreCase: true);
		return true;
	}
}
