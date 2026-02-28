namespace GammaRay.Core.Routing.Categorization;

public sealed class EndPointCategory(string name, IReadOnlyCollection<EndPointPattern> patterns)
{
	public string Name { get; } = name;

	public IReadOnlyCollection<EndPointPattern> Patterns { get; } = patterns;
}
