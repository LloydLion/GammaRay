using GammaRay.Core.Monitoring;

namespace GammaRay.Core.InternetAccess.Channels.Testing;

public interface IIAPChannelHardTester
{
	public ValueTask<bool> PerformTestAsync(IAPChannel channel, TrackableProcedure monitoring);
}
