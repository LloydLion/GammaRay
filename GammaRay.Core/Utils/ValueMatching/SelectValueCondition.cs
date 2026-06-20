namespace GammaRay.Core.Utils.ValueMatching;

public sealed class SelectValueCondition<TIn, TOut>(ValueCondition<TIn> _original, Func<TOut, TIn> _selector) : ValueCondition<TOut>
{
	public override bool IsMatch(TOut value) => _original.IsMatch(_selector(value));
}

public static class SelectValueCondition
{
	public static SelectValueCondition<TIn, TOut> Create<TIn, TOut>(ValueCondition<TIn> _original, Func<TOut, TIn> _selector) => new(_original, _selector);

	public static SelectValueCondition<TIn, TOut> Select<TIn, TOut>(this ValueCondition<TIn> _original, Func<TOut, TIn> _selector) => new(_original, _selector);
}
