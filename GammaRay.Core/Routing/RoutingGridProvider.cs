using GammaRay.Core.Routing.Categorization;
using GammaRay.Core.Routing.NetworkProfiles;
using GammaRay.Core.Settings;

namespace GammaRay.Core.Routing;

public class RoutingGridProvider
{
	private readonly Dictionary<(NetworkProfile, EndPointCategory), EndPointRoutingConfiguration> _routingGrid;


	public RoutingGridProvider(IRawSettingsProvider<IReadOnlyDictionary<(NetworkProfile Profile, EndPointCategory Category), EndPointRoutingConfiguration>> rawProvider)
	{
		var grid = rawProvider.Get().ToDictionary();
		var profiles = grid.Select(s => s.Key.Profile).Distinct().ToArray();
		var categories = grid.Select(s => s.Key.Category).Distinct().ToArray();

		foreach (var profile in profiles)
			foreach (var category in categories)
				if (!grid.ContainsKey((profile, category)))
					throw new ArgumentException($"Missing routing configuration for profile '{profile.Name}' and category '{category.Name}'");

		_routingGrid = grid;
	}


	public EndPointRoutingConfiguration GetConfiguration(NetworkProfile currentNetworkProfile, EndPointCategory endpointCategory)
	{
		return _routingGrid[(currentNetworkProfile, endpointCategory)];
	}
}
