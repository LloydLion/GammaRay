namespace GammaRay.Core.Routing;

public interface IRoutePersistenceStorage
{
	public RouteToSite? TryGetRoute(Site site, NetworkProfile profile);

	public void SaveRoute(Site site, NetworkProfile profile, string optimalConfigurationName);
}
