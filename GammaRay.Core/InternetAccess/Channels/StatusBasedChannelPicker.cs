using GammaRay.Core.Routing.NetworkProfiles;
using GammaRay.Core.Utils;

namespace GammaRay.Core.InternetAccess.Channels;

public sealed class StatusBasedChannelPicker(IIAPChannelStatusRepository _statusRepository) : IIAPChannelPicker
{
	public IAPChannelStatus? PickBestChannel(InternetAccessPoint accessPoint, NetworkProfile currentNetwork, in IAPChannelRequirements requirements)
	{
		var bestChannelStatus = accessPoint.Channels.Values
			.Select(channel => _statusRepository.TryGetStatus(accessPoint, channel, currentNetwork))
			.WhereNotNull()
			.MinBy(s => s.AverageAccessTime);
		// Unavailable status is 'bigger' then any other one

		return bestChannelStatus;
	}
}
