namespace GammaRay.Core.Utils;

public static class EnumerableExtensions
{
	extension<TElement>(IEnumerable<TElement?> self)
	{
		public IEnumerable<TElement> WhereNotNull()
		{
			foreach (var element in self)
				if (element is not null)
					yield return element;
		}
	}
}
