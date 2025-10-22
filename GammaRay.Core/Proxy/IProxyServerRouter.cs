namespace GammaRay.Core.Proxy;

public interface IProxyServerRouter
{
	public Task<ProxyRoutingResult> RouteRequestAsync(ProxyRequestContext requestContext);
}
