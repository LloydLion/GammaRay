using GammaRay.Core.Proxy;
using Microsoft.Extensions.Options;
using System.Net;
using HttpRequestHeader = GammaRay.Core.Proxy.HttpRequestHeader;


var proxy = new ProxyServer(Options.Create(new ProxyServer.Options
{
	ListenEndPoint = new IPEndPoint(IPAddress.Loopback, 9999)
}), new Router());

proxy.Run();


public class Router : IProxyServerRouter
{
	public Task<ProxyRoutingResult> RouteConnectAsync(ProxyContext context, HttpEndPoint targetHost)
	{
		if (targetHost.Host.EndsWith(".ru"))
			return Task.FromResult<ProxyRoutingResult>(new DirectProxyRoutingResult());
		else
			return Task.FromResult<ProxyRoutingResult>(new UpstreamProxyRoutingResult(new IPEndPoint(IPAddress.Loopback, 1080)));
	}

	public Task<ProxyRoutingResult> RouteHttpAsync(ProxyContext context, HttpEndPoint targetHost, HttpRequestHeader header)
	{
		if (targetHost.Host.EndsWith(".ru"))
			return Task.FromResult<ProxyRoutingResult>(new DirectProxyRoutingResult());
		else
			return Task.FromResult<ProxyRoutingResult>(new UpstreamProxyRoutingResult(new IPEndPoint(IPAddress.Loopback, 1080)));
	}
}
