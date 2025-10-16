namespace GammaRay.Core.Proxy;

public interface IProxyServerRouter
{
	public Task<ProxyRoutingResult> RouteConnectAsync(ProxyContext context, HttpEndPoint targetHost);

	public Task<ProxyRoutingResult> RouteHttpAsync(ProxyContext context, HttpEndPoint targetHost, HttpRequestHeader header);
}
