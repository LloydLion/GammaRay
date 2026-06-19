using GammaRay.Core.Routing.NetworkProfiles;

namespace GammaRay.Core.InternetAccess.Channels;

public interface IIAPChannelPicker
{
	public (IAPChannelStatus Status, IAPChannel Channel)? PickBestChannel(InternetAccessPoint accessPoint, NetworkProfile currentNetwork, in IAPChannelRequirements requirements);
}
