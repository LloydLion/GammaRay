using GammaRay.Core.Network;

namespace GammaRay.Core.InternetAccess.Channels;

public interface IChannelDriver
{
	public ValueTask<IOpenChannel?> TryOpenChannelAsync(IAPChannel channel, TransportType requestedTransportType);
}
