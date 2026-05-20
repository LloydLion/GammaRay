namespace GammaRay.Core.Services.Probing;

public interface IProbeDriver
{
	public Task<ProbeResult> ProbeAsync(ProbingArgs args);
}
