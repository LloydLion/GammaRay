using GammaRay.Core.Network;

namespace GammaRay.Core.Inbound;

public interface IInboundDriver
{
	public IInbound CreateInbound(GenericWebEndPoint localEndPoint);
}
