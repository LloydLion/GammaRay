using System.Net;

namespace GammaRay.Core.Proxy;

public class ProxyInbound(string name)
{
	public string Name { get; } = name;

	public required EndPoint EndPoint { get; init; }

	public ProxyProtocol Protocol { get; init; } = ProxyProtocol.HTTP;


	public enum ProxyProtocol
	{
		HTTP
	}
}
