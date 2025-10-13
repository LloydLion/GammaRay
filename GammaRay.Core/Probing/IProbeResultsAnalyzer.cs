namespace GammaRay.Core.Probing;

public interface IProbeResultsAnalyzer
{
	public int ChooseBestRoute(IEnumerable<ProbeResult> orderedProbeResults);
}
