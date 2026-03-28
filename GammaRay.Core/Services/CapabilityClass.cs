using GammaRay.Core.Services.Probing;

namespace GammaRay.Core.Services;

public sealed class CapabilityClass(IReadOnlyCollection<CapabilityDetectionRule> detectionRules, CapabilityProbingMethod probingMethod)
{
	public IReadOnlyCollection<CapabilityDetectionRule> DetectionRules { get; } = detectionRules;

	public CapabilityProbingMethod ProbingMethod { get; } = probingMethod;
}
