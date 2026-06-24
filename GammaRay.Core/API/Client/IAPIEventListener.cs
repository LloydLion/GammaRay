using GammaRay.Core.API.Proto;

namespace GammaRay.Core.API.Client;

public interface IAPIEventListener
{
	public bool HandleEvent(IGammaRayAPIClient sender, MonitoringEvent eventData);
}
