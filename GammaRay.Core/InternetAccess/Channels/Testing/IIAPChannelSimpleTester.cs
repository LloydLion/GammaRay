namespace GammaRay.Core.InternetAccess.Channels.Testing;

public interface IIAPChannelSimpleTester
{
	public ValueTask<IAPChannelSimpleTestResult> PerformTestAsync(IAPChannel channel, CancellationToken cancellationToken);
}
