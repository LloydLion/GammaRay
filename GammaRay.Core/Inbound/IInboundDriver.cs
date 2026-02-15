using System.Net;

namespace GammaRay.Core.Inbound;

public interface IInboundDriver
{
	public IInbound CreateInbound(IPEndPoint localEndPoint);
}
