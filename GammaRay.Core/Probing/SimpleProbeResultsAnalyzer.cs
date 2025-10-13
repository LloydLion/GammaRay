namespace GammaRay.Core.Probing;

public class SimpleProbeResultsAnalyzer : IProbeResultsAnalyzer
{
	public int ChooseBestRoute(IEnumerable<ProbeResult> orderedProbeResults)
	{
		int index = 0;
		foreach (var result in orderedProbeResults)
		{
			if (result is ProbeSuccessResult)
				return index;
			index++;
		}
		return -1;
	}
}
