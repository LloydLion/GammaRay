using GammaRay.Core.Network;
using GammaRay.Core.Utils;

namespace GammaRay.Core.Services;

public interface IServiceRepository
{
	public Decayable<Service>? TryGetService(WebEndPoint webEndPoint);

	public void RegisterService(Service service);
}
