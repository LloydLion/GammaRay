using GammaRay.Core.Network;

namespace GammaRay.Core.Channels;

public interface IChannelDriver
{
	public ValueTask<IOpenChannel?> TryOpenChannelAsync(IAPChannel channel, TransportType requestedTransportType);
}
