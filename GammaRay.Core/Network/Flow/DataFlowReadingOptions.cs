namespace GammaRay.Core.Network.Flow;

public readonly record struct DataFlowReadingOptions
{
	public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);


	public DataFlowReadingOptions() { }


	public TimeSpan Timeout { get; init; } = DefaultTimeout;

	public bool PeekOnly { get; init; } = false;


	public static void InitializeWithDefaultsIfNeed(ref DataFlowReadingOptions options)
	{
		if (options == default)
			options = new DataFlowReadingOptions();
	}
}
