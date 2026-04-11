using GammaRay.Core.Inbound;
using GammaRay.Core.InternetAccess.Channels;

namespace GammaRay.Core.Routing;

public interface IRouter
{
	public IAPChannel MakeRoutingDecision(RequestContext context);
}
