using GammaRay.Core.Proxy;
using GammaRay.Core.Routing;

namespace GammaRay.Core.Settings;

public interface ISettingsProvider : IConfigurationsProvider, IDomainCategorizer, IRouteGridProvider
{
	public void LoadSettings();


	public IReadOnlyCollection<ProxyInbound> Inbounds { get; }

	public IReadOnlyCollection<NetworkProfile> RegisteredProfiles { get; }
}
