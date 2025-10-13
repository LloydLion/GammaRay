namespace GammaRay.Core.Probing;

public interface ISiteProber
{
	public Task<ProbeResult> ProbeAsync(Site target, string configName, CancellationToken token = default);

	public IEnumerable<NetClientConfiguration> ListAvailableConfigurations();
}
