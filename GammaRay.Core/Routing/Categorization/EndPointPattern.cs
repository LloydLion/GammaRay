using GammaRay.Core.Network;

namespace GammaRay.Core.Routing.Categorization;

public class EndPointPattern(IReadOnlyList<string> webHostParts)
{
	public IReadOnlyList<string> WebHostParts { get; } = webHostParts;

	public int Level => WebHostParts.Count;


	public bool IsMatch(WebEndPoint endPoint)
	{
		var domain = endPoint.Host.Domain.AsSpan();
		var enumerator = domain.Split('.');

		/*
		 * Idea:
		 * find first equal part in pattern and domain
		 * then ensure that after first equal part all parts equal
		 */

		int patternIndex = 0;
		foreach (var partRange in enumerator)
		{
			var part = domain[partRange];

			// Domain too big for this pattern
			// Example: domain = some.big.domain.net.tld   pattern = domain.net
			if (patternIndex >= WebHostParts.Count)
				return false;

			var partsEqual = WebHostParts[patternIndex] == part;

			// If we are in begin of pattern and it is not matched -> cannot be matched at all
			// Example: domain = sub.domain.net.tld   pattern = domain.tld
			if (patternIndex != 0 && partsEqual == false)
				return false;

			// Begin or continuation of equality zone
			// in both cases advice patternIndex
			if (partsEqual)
				patternIndex++;
		}

		// If domain parts ended there are 2 cases: match or domain too small

		// Domain parts and patterns parts ends at same time
		// Example: domain = sub.domain.tld   pattern = domain.tld
		if (patternIndex == WebHostParts.Count)
			return true;

		// There are more pattern parts, but domain do not cover it
		// Example: domain = sub.domain.net   pattern = domain.net.tld
		return false;
	}

	public static EndPointPattern Parse(string rawPattern)
	{
		return new EndPointPattern(rawPattern.Trim().Split('.'));
	}

	public override string ToString()
	{
		return $"EndPointPattern {{{string.Join(".", WebHostParts)}}}";
	}
}
