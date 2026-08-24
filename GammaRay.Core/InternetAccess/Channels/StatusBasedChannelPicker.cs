using GammaRay.Core.InternetAccess.Channels.Testing;
using GammaRay.Core.Network.Profiles;

namespace GammaRay.Core.InternetAccess.Channels;

public sealed class StatusBasedChannelPicker(IIAPChannelMonitor _monitor) : IIAPChannelPicker
{
	public (IAPChannelStatus Status, IAPChannel Channel)? PickBestChannel(InternetAccessPoint accessPoint, NetworkProfile currentNetwork, in IAPChannelRequirements requirements)
	{
		// Special handling for local IAPs
		if (accessPoint.Name.StartsWith(InternetAccessPointProvider.LocalIAPPrefix))
		{
			var channel = accessPoint.Channels[InternetAccessPointProvider.LocalIAPChannelName];
			var localNetwork = channel.AvailableInNetwork[0];
			if (localNetwork == currentNetwork)
				return (IAPChannelStatus.BestStatus, channel);
			else return null;
		}


		(IAPChannelStatus Status, IAPChannel Channel)? bestChannel = null;

		int currentMinLifetimeRank = 3; 
	retry:
	
		foreach (var channel in accessPoint.Channels.Values)
		{
			if (channel.AvailableInNetwork.Contains(currentNetwork) == false)
				continue;

			var status = _monitor.GetStatus(accessPoint, channel, currentNetwork);

			if (status.IsAvailable == false)
				continue;
			if (status.CharacteristicAccessTime == TimeSpan.MaxValue)
				continue;
			
			if (getAverageLifeTimeRank(status.AverageLifeTime) < currentMinLifetimeRank)
				continue;

			if (bestChannel is null || status.CharacteristicAccessTime <= bestChannel.Value.Status.CharacteristicAccessTime)
				bestChannel = (status, channel);
		}

		if (bestChannel is null && currentMinLifetimeRank != 0)
		{
			currentMinLifetimeRank--;
			goto retry;
		}

		return bestChannel;


		int getAverageLifeTimeRank(TimeSpan averageLifeTime) => averageLifeTime.Minutes switch
		{
			29 or 30 => 3,
			>= 25 => 2,
			>= 12 => 1,
			_ => 0
		};
	}
}
