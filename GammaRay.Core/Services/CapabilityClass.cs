using GammaRay.Core.Services.Probing;

namespace GammaRay.Core.Services;

public sealed class CapabilityClass(string name, IReadOnlyCollection<CapabilityDetectionRule> detectionRules, CapabilityProbingMethod probingMethod)
{
	public string Name { get; } = name;

	public IReadOnlyCollection<CapabilityDetectionRule> DetectionRules { get; } = detectionRules;

	public CapabilityProbingMethod ProbingMethod { get; } = probingMethod;


	public override string ToString()
	{
		return Name;
	}
}
