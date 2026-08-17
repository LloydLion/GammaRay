using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace GammaRay.Core.Settings.Binding.ValueParsers;

public class TryParseMethodBasedSettingsTreeValueParser : ISettingsTreeValueParser
{
	public bool CanParse(Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		return GetTryParseMethod(type) is not null;
	}

	public SettingsTreeValueParseResult TryParse(Type type, ReadOnlySpan<char> value, SettingsTreeAggregateBinder aggregateBinder)
	{
		var tryParseMethod = GetTryParseMethod(type) ?? throw new InvalidOperationException($"No TryParse method found for type {type.FullName}");

		var parameters = new object?[] { new string(value), null };
		var ok = tryParseMethod.Invoke(null, parameters);

		if (ok is true)
		{
			var result = parameters[1] ?? throw new NullReferenceException($"{type.FullName}.TryParse() return no result");
			return SettingsTreeValueParseResult.Success(result);
		}
		else return SettingsTreeValueParseResult.Failure($"Enable to parse value '{value}' as {type.Name}");
	}

	private static MethodInfo? GetTryParseMethod(Type type) => type.GetMethod("TryParse", 0, BindingFlags.Static | BindingFlags.Public, null, [typeof(string), type.MakeByRefType()], null);
}
