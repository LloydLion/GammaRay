namespace GammaRay.Core.Routing;

public interface IRoutingGridResolver
{
	public EndpointRoutingConfiguration GetConfiguration(NetworkProfile currentNetworkProfile, EndpointCategory endpointCategory);
}
