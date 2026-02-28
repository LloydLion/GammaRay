using GammaRay.Core.Routing.NetworkProfiles;

namespace GammaRay.Core.InternetAccess.Channels;

public interface IIAPChannelStatusRepository
{
	public IAPChannelStatus GetStatus(InternetAccessPoint point, IAPChannel channel, NetworkProfile currentNetworkProfile);
}
