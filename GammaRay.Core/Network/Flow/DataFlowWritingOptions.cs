namespace GammaRay.Core.Network.Flow;

public readonly record struct DataFlowWritingOptions
{
	public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);


	public DataFlowWritingOptions() { }


	public TimeSpan Timeout { get; init; } = DefaultTimeout;


	public static void InitializeWithDefaultsIfNeed(ref DataFlowWritingOptions options)
	{
		if (options == default)
			options = new DataFlowWritingOptions();
	}
}
