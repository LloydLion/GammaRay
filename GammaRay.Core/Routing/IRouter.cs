using GammaRay.Core.Channels;
using GammaRay.Core.Inbound;

namespace GammaRay.Core.Routing;

public interface IRouter
{
	public IReadOnlyList<IAPChannel> MakeRoutingDecision(RequestContext context);
}
