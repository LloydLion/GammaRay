using GammaRay.Core.InternetAccess;

namespace GammaRay.Core.Services.Probing;

public interface IProbingManager
{
	public void StartProbing(Service service, IReadOnlyCollection<InternetAccessPoint> pointsToProbeVia, IServiceStatusTableRepository routeOutput);
}
