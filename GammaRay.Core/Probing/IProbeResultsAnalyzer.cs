namespace GammaRay.Core.Probing;

public interface IProbeResultsAnalyzer
{
	public IEnumerable<ProbeResult> ChooseBestRoutes(IEnumerable<ProbeResult> orderedProbeResults);
}
