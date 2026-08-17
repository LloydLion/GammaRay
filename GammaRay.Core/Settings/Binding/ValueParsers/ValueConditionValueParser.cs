using GammaRay.Core.Settings.Tree;
using GammaRay.Core.Utils.ValueMatching;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
namespace GammaRay.Core.Settings.Binding.ValueParsers;

public sealed class ValueConditionValueParser : ISettingsTreeValueParser
{
	public SettingsTreeValueParseResult TryParse(Type type, ReadOnlySpan<char> value, SettingsTreeAggregateBinder aggregateBinder)
	{
		var targetType = type.GetGenericArguments()[0];
		var parser = aggregateBinder.Parsers.First(s => s.CanParse(targetType, aggregateBinder));

		var method = GetType().GetMethod(nameof(TryParseGeneric), BindingFlags.Static | BindingFlags.NonPublic)!;

		return (SettingsTreeValueParseResult)method.MakeGenericMethod(targetType).Invoke(null, [new string(value), aggregateBinder, parser])!;
	}

	public bool CanParse(Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		return
			type.IsGenericType &&
			type.GetGenericTypeDefinition() == typeof(ValueCondition<>) &&
			aggregateBinder.Parsers.Any(s => s.CanParse(type.GetGenericArguments()[0], aggregateBinder));
	}

	private static SettingsTreeValueParseResult? TryParseGeneric<TValue>(string data, SettingsTreeAggregateBinder aggregateBinder, ISettingsTreeValueParser parser)
	{
		try
		{
			return SettingsTreeValueParseResult.Success(ValueConditionFactory.Parse(data, (span) =>
			{
				var parseResult = parser.TryParse(typeof(TValue), span, aggregateBinder);
				if (parseResult.Try(out var value, out var errorMessage))
					return (TValue)value;
				throw new SignalException(errorMessage);
			}));
		}
		catch (SignalException ex)
		{
			return SettingsTreeValueParseResult.Failure($"Sub parser error: {ex.Message}");
		}
	}

	private class SignalException(string errorMessage) : Exception(errorMessage);
}
