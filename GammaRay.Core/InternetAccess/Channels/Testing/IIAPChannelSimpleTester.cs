using GammaRay.Core.Monitoring;

namespace GammaRay.Core.InternetAccess.Channels.Testing;

public interface IIAPChannelSimpleTester
{
	public ValueTask<bool> PerformTestAsync(IAPChannel channel, CancellationToken cancellation, MonitoringContext monitoring);
}
