namespace GammaRay.Core.InternetAccess.Channels.Testing;

public sealed class IAPChannelObservedData
{
	public required TimeSpan[] ObservationRow { get; init; }

	public required bool IsAvailable { get; init; }
}
