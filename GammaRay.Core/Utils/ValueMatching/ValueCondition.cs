namespace GammaRay.Core.Utils.ValueMatching;

public abstract class ValueCondition<TValue>
{
	public abstract bool IsMatch(TValue value);
}
