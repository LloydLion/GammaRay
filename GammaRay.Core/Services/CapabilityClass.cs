namespace GammaRay.Core.Services;

public sealed class CapabilityClass(IReadOnlyCollection<CapabilityDetectionRule> detectionRules)
{
	public IReadOnlyCollection<CapabilityDetectionRule> DetectionRules { get; } = detectionRules;
}
