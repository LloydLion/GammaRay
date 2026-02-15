using GammaRay.Core.InternetAccess;

namespace GammaRay.Core.Services.Probing;

public sealed class ServiceRoute(Service service, DateTime validUntil, InternetAccessPointChain chain)
{
	public Service Service { get; } = service;

	public DateTime ValidUntil { get; } = validUntil;

	public InternetAccessPointChain Chain { get; } = chain;
}
