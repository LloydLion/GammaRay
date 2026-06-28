using GammaRay.Core.Network;
using GammaRay.Core.Utils;

namespace GammaRay.Core.Services;

public interface IServiceRepository
{
	public Decayable<Service>? TryGetService(WebEndPoint webEndPoint);

	public IReadOnlyCollection<Decayable<Service>> ListServices();

	public void RegisterService(Service service);
}
