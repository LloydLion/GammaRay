using GammaRay.Core.Routing.NetworkProfiles;

namespace GammaRay.Core.InternetAccess.Channels;

public sealed class IAPChannelStatus(InternetAccessPoint internetAccessPoint, IAPChannel channel, NetworkProfile network, int metric, DateTime validUntil)
{
	public InternetAccessPoint InternetAccessPoint { get; } = internetAccessPoint;

	public IAPChannel Channel { get; } = channel;

	public NetworkProfile Network { get; } = network;

	public int Metric { get; } = metric;

	public DateTime ValidUntil { get; } = validUntil;


	public bool IsAvailable => Metric > 0;
}
