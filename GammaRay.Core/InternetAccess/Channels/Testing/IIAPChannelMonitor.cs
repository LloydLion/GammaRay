using GammaRay.Core.Network.Profiles;

namespace GammaRay.Core.InternetAccess.Channels.Testing;

public interface IIAPChannelMonitor
{
	public void StartMonitoring();

	public IAPChannelStatus GetStatus(InternetAccessPoint IAP, IAPChannel channel, NetworkProfile currentProfile);
}
