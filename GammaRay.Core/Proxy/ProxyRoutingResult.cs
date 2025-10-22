namespace GammaRay.Core.Proxy;

public class ProxyRoutingResult(IEnumerable<NetClientConfiguration> clientConfigurations)
{
	public IEnumerable<NetClientConfiguration> ClientConfigurations { get; } = clientConfigurations;
}
