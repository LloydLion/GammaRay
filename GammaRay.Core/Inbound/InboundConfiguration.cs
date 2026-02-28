using System.Net;

namespace GammaRay.Core.Inbound;

public sealed class InboundConfiguration(string protocol, IPEndPoint endPoint)
{
	public string Protocol { get; } = protocol;

	public IPEndPoint EndPoint { get; } = endPoint;


	public override string ToString()
	{
		return $"InboundConfiguration {{{Protocol}://{EndPoint}}}";
	}
}
