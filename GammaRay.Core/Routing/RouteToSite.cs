namespace GammaRay.Core.Routing;

public readonly record struct RouteToSite(string[] ConfigurationsNames, DateTime ValidUntil)
{
	public bool IsValid => DateTime.UtcNow <= ValidUntil;
}
