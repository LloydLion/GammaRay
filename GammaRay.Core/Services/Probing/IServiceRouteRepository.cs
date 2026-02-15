namespace GammaRay.Core.Services.Probing;

public interface IServiceRouteRepository
{
	public ServiceRoute? TryGetRoute(Service service);

	public void RegisterRoute(ServiceRoute route);
}
