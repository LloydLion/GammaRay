namespace GammaRay.Core.InternetAccess.Channels.Testing;

public readonly record struct IAPChannelSimpleTestResult(TimeSpan TestDuration, IAPChannelSimpleTestResult.TestStatus Status)
{
	public enum TestStatus
	{
		Success,
		Timeout,
		SocketFailure,
		UnexceptedData
	}
}
