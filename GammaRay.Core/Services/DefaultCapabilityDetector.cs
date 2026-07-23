using GammaRay.Core.Routing;

namespace GammaRay.Core.Services;

public sealed class DefaultCapabilityDetector : ICapabilityDetector
{
	private readonly CapabilityClassProvider _capabilityClasses;


	public DefaultCapabilityDetector(CapabilityClassProvider capabilityClasses)
	{
		_capabilityClasses = capabilityClasses;
	}


	public Capability Detect(RoutingRequest request)
	{
		CapabilityClass? firstMatchedClass = null;
		foreach (var capabilityClass in _capabilityClasses.PlainCapabilityClasses)
		{
			var passed = capabilityClass.DetectionRules.Any(rule => PerformBasicRuleCheck(request, rule));
			if (passed)
			{
				firstMatchedClass = capabilityClass;
				break;
			}
		}

		if (firstMatchedClass == null)
		{
			firstMatchedClass = _capabilityClasses.PlainCapabilityClasses[^1];
			// TODO: warn user about invalid configuration
		}

		var capability = new Capability(firstMatchedClass, new Dictionary<string, string>());

		return capability;
	}

	private static bool PerformBasicRuleCheck(RoutingRequest request, CapabilityDetectionRule rule) =>
		rule.Transport.IsMatch(request.Destination.Protocol) && rule.Port.IsMatch(request.Destination.Port);
}
