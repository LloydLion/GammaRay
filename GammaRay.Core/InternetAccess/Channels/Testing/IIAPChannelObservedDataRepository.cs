using GammaRay.Core.Network.Profiles;

namespace GammaRay.Core.InternetAccess.Channels.Testing;

public interface IIAPChannelObservedDataRepository
{
	public IAPChannelObservedData? TryGetObservedData(InternetAccessPoint IAP, IAPChannel channel, NetworkProfile network);

	public void SaveObservedData(InternetAccessPoint IAP, IAPChannel channel, NetworkProfile network, IAPChannelObservedData data);
}
