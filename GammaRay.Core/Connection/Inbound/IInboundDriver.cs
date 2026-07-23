using System.Net;

namespace GammaRay.Core.Connection.Inbound;

public interface IInboundDriver
{
	public IInbound CreateInbound(IPEndPoint localEndPoint);
}
