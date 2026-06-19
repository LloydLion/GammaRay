namespace GammaRay.Core.Utils;

public static class SpanExtensions
{
	extension<T>(Span<T> span)
	{
		public Span<T> LimitLength(int limit)
		{
			if (span.Length > limit)
				return span[..limit];
			return span;
		}
	}
}
