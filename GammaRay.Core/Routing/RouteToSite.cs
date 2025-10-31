namespace GammaRay.Core.Routing;

public readonly record struct RouteToSite(string ConfigurationName, DateTime ValidUntil)
{
	public bool IsValid => DateTime.UtcNow >= ValidUntil;
}
