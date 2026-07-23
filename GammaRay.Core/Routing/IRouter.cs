using GammaRay.Core.InternetAccess.Channels;

namespace GammaRay.Core.Routing;

public interface IRouter
{
	public NamedIAPChannel MakeRoutingDecision(RoutingRequest request);
}
