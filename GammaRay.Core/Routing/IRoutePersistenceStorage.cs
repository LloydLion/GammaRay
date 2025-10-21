namespace GammaRay.Core.Routing;

public interface IRoutePersistenceStorage
{
	public string? TryGetRoute(Site site, NetworkProfile profile);

	public void SaveRoute(Site site, NetworkProfile profile, string optimalConfigurationName);
}
