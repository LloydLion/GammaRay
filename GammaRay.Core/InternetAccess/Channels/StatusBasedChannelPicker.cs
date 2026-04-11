using GammaRay.Core.Routing.NetworkProfiles;
using GammaRay.Core.Utils;

namespace GammaRay.Core.InternetAccess.Channels;

public sealed class StatusBasedChannelPicker(IIAPChannelStatusRepository _statusRepository) : IIAPChannelPicker
{
	private readonly Dictionary<InternetAccessPoint, IAPChannelStatus> pseudoStatuses = [];


	public IAPChannelStatus? PickBestChannel(InternetAccessPoint accessPoint, NetworkProfile currentNetwork, in IAPChannelRequirements requirements)
	{
		// Special handling for local IAPs
		if (accessPoint.Name.StartsWith(InternetAccessPointProvider.LocalIAPPrefix))
		{
			var localNetwork = accessPoint.Channels[InternetAccessPointProvider.LocalIAPChannelName].AvailableInNetwork[0];
			if (localNetwork != currentNetwork)
				return null;

			if (pseudoStatuses.TryGetValue(accessPoint, out var status))
				return status;
			status = new IAPChannelStatus(accessPoint, accessPoint.Channels[InternetAccessPointProvider.LocalIAPChannelName], localNetwork, TimeSpan.Zero);
			pseudoStatuses.Add(accessPoint, status);
			return status;
		}


		var bestChannelStatus = accessPoint.Channels.Values
			.Where(channel => channel.AvailableInNetwork.Contains(currentNetwork))
			.Select(channel => _statusRepository.TryGetStatus(accessPoint, channel, currentNetwork))
			.WhereNotNull()
			.MinBy(s => s.AverageAccessTime);
		// Unavailable status is 'bigger' then any other one

		return bestChannelStatus;
	}
}
