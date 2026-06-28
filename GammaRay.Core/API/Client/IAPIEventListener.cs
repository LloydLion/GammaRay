using GammaRay.Core.API.Services.Proto;

namespace GammaRay.Core.API.Client;

public interface IAPIEventListener
{
	public bool HandleEvent(IGammaRayAPIClient sender, MonitoringEvent eventData);
}
