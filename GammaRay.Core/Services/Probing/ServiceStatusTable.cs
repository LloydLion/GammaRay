using GammaRay.Core.InternetAccess;

namespace GammaRay.Core.Services.Probing;

public sealed class ServiceStatusTable(Service service, IReadOnlyDictionary<InternetAccessPoint, ServiceIAPStatus> table)
{
	public Service Service { get; } = service;

	public IReadOnlyDictionary<InternetAccessPoint, ServiceIAPStatus> Table { get; } = table;
}
