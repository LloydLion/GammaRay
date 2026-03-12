namespace GammaRay.Core.Utils.ValueMatching;

public sealed class NoneValueCondition<TValue> : ValueCondition<TValue>
{
	private readonly bool _value;


	public static NoneValueCondition<TValue> AlwaysTrue { get; } = new NoneValueCondition<TValue>(true);

	public static NoneValueCondition<TValue> AlwaysFalse { get; } = new NoneValueCondition<TValue>(false);


	private NoneValueCondition(bool value)
	{
		_value = value;
	}


	public override bool IsMatch(TValue value) => _value;
}
