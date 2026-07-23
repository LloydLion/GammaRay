using GammaRay.Core.Routing;

namespace GammaRay.Core.Services;

public interface ICapabilityDetector
{
	public Capability Detect(RoutingRequest request);
}
