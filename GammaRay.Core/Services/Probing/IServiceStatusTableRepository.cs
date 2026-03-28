using GammaRay.Core.Utils;

namespace GammaRay.Core.Services.Probing;

public interface IServiceStatusTableRepository
{
	public Decayable<ServiceStatusTable>? TryGetTable(Service service);

	public void UpdateTable(ServiceStatusTable route);
}
