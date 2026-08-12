using GammaRay.Core.Settings.Tree;
using GammaRay.Core.Utils.ValueMatching;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
namespace GammaRay.Core.Settings.Binding.ValueParsers;

public sealed class ValueConditionValueParser : ISettingsTreeValueParser
{
	public bool TryParse(Type type, ReadOnlySpan<char> value, SettingsTreeAggregateBinder aggregateBinder, [NotNullWhen(true)] out object? result)
	{
		result = null;
		var targetType = type.GetGenericArguments()[0];
		var parser = aggregateBinder.Parsers.First(s => s.CanParse(targetType, aggregateBinder));

		var method = GetType().GetMethod(nameof(TryParseGeneric), BindingFlags.Static | BindingFlags.NonPublic)!;

		result = method.MakeGenericMethod(targetType).Invoke(null, [new string(value), aggregateBinder, parser]);
		return result is not null;
	}

	public bool CanParse(Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		return
			type.IsGenericType &&
			type.GetGenericTypeDefinition() == typeof(ValueCondition<>) &&
			aggregateBinder.Parsers.Any(s => s.CanParse(type.GetGenericArguments()[0], aggregateBinder));
	}

	private static ValueCondition<TValue>? TryParseGeneric<TValue>(string data, SettingsTreeAggregateBinder aggregateBinder, ISettingsTreeValueParser parser)
	{
		try
		{
			return ValueConditionFactory.Parse(data, (span) => {
				if (parser.TryParse(typeof(TValue), span, aggregateBinder, out var result) == false)
					throw new Exception("SINGAL");
				return (TValue)result;
			});
		}
		catch (Exception)
		{
			return null;
		}
	}
}
