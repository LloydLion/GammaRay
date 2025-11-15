namespace GammaRay.Core.Probing;

public class SimpleProbeResultsAnalyzer : IProbeResultsAnalyzer
{
	public IEnumerable<ProbeResult> ChooseBestRoutes(IEnumerable<ProbeResult> orderedProbeResults)
	{
		return orderedProbeResults.Where(s => s is ProbeSuccessResult);
	}
}
