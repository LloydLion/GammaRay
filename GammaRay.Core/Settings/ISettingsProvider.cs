using GammaRay.Core.Routing;
using GammaRay.Core.Settings.Entities;

namespace GammaRay.Core.Settings;

public interface ISettingsProvider : IConfigurationsProvider, IDomainCategorizer, IRouteGridProvider
{
	public void LoadSettings();


	public IReadOnlyDictionary<string, InboundSettings> Inbounds { get; }

	public IReadOnlyCollection<NetworkProfile> RegisteredProfiles { get; }
}
