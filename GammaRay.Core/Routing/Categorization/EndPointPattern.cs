using GammaRay.Core.Network;

namespace GammaRay.Core.Routing.Categorization;

public class EndPointPattern(IReadOnlyList<string> webHostParts)
{
	public IReadOnlyList<string> WebHostParts { get; } = webHostParts;

	public int Level => WebHostParts.Count;


	public bool IsMatch(WebEndPoint endPoint)
	{
		// TODO
		throw new NotImplementedException();
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
