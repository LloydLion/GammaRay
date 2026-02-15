using GammaRay.Core.InternetAccess;

namespace GammaRay.Core.Services.Probing;

public interface IProber
{
	public void StartProbing(Service service, InternetAccessPointChain extendedChain, IServiceRouteRepository routeOutput);
}
