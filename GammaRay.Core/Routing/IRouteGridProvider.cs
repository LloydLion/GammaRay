namespace GammaRay.Core.Routing;

public interface IRouteGridProvider
{
	public string GetConfigurationQueueName(NetworkProfile profile, DomainCategory category);
}
