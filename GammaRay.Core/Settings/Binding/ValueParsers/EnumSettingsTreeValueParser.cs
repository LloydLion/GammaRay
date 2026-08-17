namespace GammaRay.Core.Settings.Binding.ValueParsers;

public class EnumSettingsTreeValueParser : ISettingsTreeValueParser
{
	public bool CanParse(Type type, SettingsTreeAggregateBinder aggregateBinder) => type.IsEnum;

	public SettingsTreeValueParseResult TryParse(Type type, ReadOnlySpan<char> value, SettingsTreeAggregateBinder aggregateBinder)
	{
		if (Enum.TryParse(type, value, ignoreCase: true, out var result))
			return SettingsTreeValueParseResult.Success(result);
		
		return SettingsTreeValueParseResult.Failure($"Invalid enum value: {value}, allowed: {string.Join(", ", Enum.GetNames(type))}");
	}
}
