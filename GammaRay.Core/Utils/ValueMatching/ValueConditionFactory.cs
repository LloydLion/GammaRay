using System.Diagnostics;
using System.Numerics;
using System.Reflection;

namespace GammaRay.Core.Utils.ValueMatching;

public static class ValueConditionFactory
{
	public static ValueCondition<TValue> Parse<TValue>(string? value, Func<ReadOnlySpan<char>, TValue> parser)
	{
		if (string.IsNullOrWhiteSpace(value))
			return NoneValueCondition<TValue>.AlwaysTrue;

		var valueSpan = value.AsSpan();

		bool invert = false;
		if (valueSpan.StartsWith('~'))
		{
			invert = true;
			valueSpan = valueSpan[1..];
		}

		if (true)
		{
			var condition = GenericHelper<TValue>.ParseNumeric(valueSpan, parser);
			if (condition is not null)
				return condition;
		}

		var exceptValue = parser(valueSpan);
		return new EqualityValueCondition<TValue>(exceptValue, invert, EqualityComparer<TValue>.Default);
	}

	private static NumericValueCondition<TValue>? ParseNumeric<TValue>(ReadOnlySpan<char> value, Func<ReadOnlySpan<char>, TValue> parser)
		where TValue : notnull, INumber<TValue>
	{
		var dashIndex = value.IndexOf('-');
		if (dashIndex != -1)
		{
			var lowBoundSpan = value[..dashIndex];
			var highBoundSpan = value[(dashIndex + 1)..];

			var lowBound = parser(lowBoundSpan);
			var highBound = parser(highBoundSpan);
			return new NumericValueCondition<TValue>(
				(lowBound, true),
				(highBound, true),
				NumericMatchMethod.ExceptInRange
			);
		}

		if (value is ['>' or '<', ..])
		{
			var operation = value[0];
			value = value[1..];

			var equalityAllowed = false;
			if (value.StartsWith('='))
			{
				equalityAllowed = true;
				value = value[1..];
			}

			var boundValue = parser(value);

			return operation switch
			{
				'<' => new NumericValueCondition<TValue>(
						lowBound: null,
						highBound: (boundValue, equalityAllowed),
						matchMethod: NumericMatchMethod.ExceptInRange
					),
				'>' => new NumericValueCondition<TValue>(
						lowBound: (boundValue, equalityAllowed),
						highBound: null,
						matchMethod: NumericMatchMethod.ExceptInRange
					),
				_ => throw new UnreachableException(),
			};
		}

		return null;
	}

	private static class GenericHelper<TValue>
	{
		public static readonly Func<
			ReadOnlySpan<char>,
			Func<ReadOnlySpan<char>, TValue>,
			ValueCondition<TValue>?> ParseNumeric = CreateParseNumeric();

		private static Func<ReadOnlySpan<char>, Func<ReadOnlySpan<char>, TValue>, ValueCondition<TValue>?> CreateParseNumeric()
		{
			var isNumeric = typeof(TValue).GetInterfaces().Any(@interface =>
				@interface.GetGenericTypeDefinition() == typeof(INumber<>) && @interface.GetGenericArguments().SequenceEqual([typeof(TValue)])
			);

			if (isNumeric)
				return typeof(ValueConditionFactory)
					.GetMethod(nameof(ValueConditionFactory.ParseNumeric), BindingFlags.NonPublic | BindingFlags.Static)!
					.MakeGenericMethod(typeof(TValue)).CreateDelegate<Func<
						ReadOnlySpan<char>,
						Func<ReadOnlySpan<char>, TValue>,
						ValueCondition<TValue>?>
					>();
			return (_, _) => null;
		}
	}
}
