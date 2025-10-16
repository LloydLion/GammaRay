using System.Net;

namespace GammaRay.Core.Proxy;

public abstract record ProxyRoutingResult();

public sealed record DirectProxyRoutingResult() : ProxyRoutingResult;

public sealed record UpstreamProxyRoutingResult(IPEndPoint UpstreamProxyServer) : ProxyRoutingResult;
