using GammaRay.Core.Inbound;

namespace GammaRay.Core.Services;

public interface ICapabilityDetector
{
	public Capability Detect(RequestContext request);
}
