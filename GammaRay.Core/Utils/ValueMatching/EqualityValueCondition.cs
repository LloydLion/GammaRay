namespace GammaRay.Core.Utils.ValueMatching;

public sealed class EqualityValueCondition<TValue> : ValueCondition<TValue>
{
	public EqualityValueCondition(TValue expectedValue, bool inverted, IEqualityComparer<TValue> equalityComparer)
	{
		ExpectedValue = expectedValue;
		Inverted = inverted;
		Comparer = equalityComparer;
	}


	public TValue ExpectedValue { get; }

	public bool Inverted { get; }

	public IEqualityComparer<TValue> Comparer { get; }


	public override bool IsMatch(TValue value)
	{
		return Comparer.Equals(ExpectedValue, value) == !Inverted;
	}
}
