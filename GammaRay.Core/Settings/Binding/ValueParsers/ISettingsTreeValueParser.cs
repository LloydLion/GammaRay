using System.Diagnostics.CodeAnalysis;

namespace GammaRay.Core.Settings.Binding.ValueParsers;

public interface ISettingsTreeValueParser
{
	public bool CanParse(Type type, SettingsTreeAggregateBinder aggregateBinder);

	public SettingsTreeValueParseResult TryParse(Type type, ReadOnlySpan<char> value, SettingsTreeAggregateBinder aggregateBinder);
}
