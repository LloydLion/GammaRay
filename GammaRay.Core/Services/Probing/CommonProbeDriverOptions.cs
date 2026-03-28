namespace GammaRay.Core.Services.Probing;

public sealed class CommonProbeDriverOptions
{
	public TimeSpan RTTTimeout { get; init; } = TimeSpan.FromSeconds(10);

	public TimeSpan ContinuousDataTimeout { get; init; } = TimeSpan.FromSeconds(2);
}
