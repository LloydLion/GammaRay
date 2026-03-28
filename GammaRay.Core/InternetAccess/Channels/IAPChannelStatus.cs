using GammaRay.Core.Routing.NetworkProfiles;

namespace GammaRay.Core.InternetAccess.Channels;

public sealed class IAPChannelStatus(
	InternetAccessPoint internetAccessPoint,
	IAPChannel channel,
	NetworkProfile network,
	TimeSpan averageAccessTime
)
{
	public static readonly TimeSpan UnavailableAccessTime = TimeSpan.MaxValue;


	public InternetAccessPoint InternetAccessPoint { get; } = internetAccessPoint;

	public IAPChannel Channel { get; } = channel;

	public NetworkProfile Network { get; } = network;

	public TimeSpan AverageAccessTime { get; } = averageAccessTime;


	public bool IsAvailable => AverageAccessTime != TimeSpan.MaxValue;
}
