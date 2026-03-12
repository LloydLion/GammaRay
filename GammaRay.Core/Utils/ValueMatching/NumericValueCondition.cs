using System.Diagnostics;
using System.Numerics;

namespace GammaRay.Core.Utils.ValueMatching;

public sealed class NumericValueCondition<TValue> : ValueCondition<TValue>
	where TValue : notnull, INumber<TValue>
{
	public NumericValueCondition(
		(TValue Value, bool EqualityAllowed)? lowBound,
		(TValue Value, bool EqualityAllowed)? highBound,
		NumericMatchMethod matchMethod
	)
	{
		LowBound = lowBound;
		HighBound = highBound;
		MatchMethod = matchMethod;
	}


	public (TValue Value, bool EqualityAllowed)? LowBound { get; }

	public (TValue Value, bool EqualityAllowed)? HighBound { get; }

	public NumericMatchMethod MatchMethod { get; }


	public override bool IsMatch(TValue value)
	{
		var lowBoundSatisfied = isBoundSatisfied(LowBound, (value, bound) => value > bound, value);
		var highBoundSatisfied = isBoundSatisfied(HighBound, (value, bound) => value < bound, value);
		
		var isInRange = lowBoundSatisfied && highBoundSatisfied;

		return MatchMethod switch
		{
			NumericMatchMethod.ExceptInRange => isInRange,
			NumericMatchMethod.ExceptOutOfRange => !isInRange,
			_ => throw new UnreachableException()
		};


		static bool isBoundSatisfied((TValue, bool)? bound, Func<TValue, TValue, bool> exclusiveCheck, TValue value)
		{
			if (bound is null)
				return true;
			var (boundValue, equalityAllowed) = bound.Value;

			if (equalityAllowed && value == boundValue)
				return true;

			if (exclusiveCheck(value, boundValue))
				return true;
			return false;
		}
	}
}
