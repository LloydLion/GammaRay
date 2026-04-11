using GammaRay.Core.Routing.NetworkProfiles;

namespace GammaRay.Core.InternetAccess.Channels;

public interface IIAPChannelStatusRepository
{
	public IAPChannelStatus? TryGetStatus(InternetAccessPoint point, IAPChannel channel, NetworkProfile currentNetworkProfile);

	public void UpdateStatuses(IEnumerable<IAPChannelStatus> statusTable);

	public DateTime GetLastStatusUpdateTime(NetworkProfile networkProfile);
}
