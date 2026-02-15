using GammaRay.Core.Network;

namespace GammaRay.Core.Services;

public interface IServiceRepository
{
	public Service? TryGetService(WebEndPoint webEndPoint);

	public void RegisterService(Service service);
}
