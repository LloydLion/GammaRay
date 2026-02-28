using GammaRay.Core.Routing;
using GammaRay.Core.Routing.Categorization;
using GammaRay.Core.Routing.NetworkProfiles;
using YamlDotNet.RepresentationModel;

namespace GammaRay.Core.Settings;

public class YAMLRoutingGridRawProvider : IRawSettingsProvider<IReadOnlyDictionary<(NetworkProfile Profile, EndPointCategory Category), EndPointRoutingConfiguration>>
{
	private IReadOnlyDictionary<(NetworkProfile, EndPointCategory), EndPointRoutingConfiguration>? _routingGrid;


	public bool IsInitialized => _routingGrid is not null;


	public IReadOnlyDictionary<(NetworkProfile Profile, EndPointCategory Category), EndPointRoutingConfiguration> Get()
	{
		return _routingGrid ?? throw new InvalidOperationException("Not initialized");
	}

	public void Initialize(
		YAMLConfigurationLoader YAMLLoader,
		NetworkProfileProvider networkProfileProvider,
		EndPointCategoriesProvider endPointCategoryProvider,
		EndPointRoutingConfigurationProvider endpointRoutingConfiguration
	)
	{
		_routingGrid = LoadRoutingGrid(
			YAMLLoader.GetFragment("routingGrid"),
			networkProfileProvider.PlainProfiles,
			endPointCategoryProvider.PlainCategories,
			endpointRoutingConfiguration.EndPointRoutingConfigurations
		);
	}

	private static Dictionary<(NetworkProfile, EndPointCategory), EndPointRoutingConfiguration> LoadRoutingGrid(
		YamlMappingNode node,
		IReadOnlyCollection<NetworkProfile> profiles,
		IReadOnlyCollection<EndPointCategory> categories,
		IReadOnlyDictionary<string, EndPointRoutingConfiguration> endPointRoutingConfigurations
	)
	{
		var profileOrderNames = node["profilesOrder"].Bind<string[]>();
		var profilesDict = profiles.ToDictionary(p => p.Name);
		var profileOrder = profileOrderNames.Select(name => profilesDict[name]).ToArray();

		var categoriesDict = categories.ToDictionary(c => c.Name);
		var result = new Dictionary<(NetworkProfile, EndPointCategory), EndPointRoutingConfiguration>();
		var gridNode = (YamlMappingNode)node["grid"];

		foreach (var entry in gridNode.ScalarChildrenMap)
		{
			var categoryName = entry.Key;
			var category = categoriesDict[categoryName];

			var mappingNode = (YamlSequenceNode)entry.Value;
			var namedMapping = mappingNode.Bind<string[]>();
			foreach (var (index, configName) in namedMapping.Index())
			{
				var profile = profileOrder[index];
				var config = endPointRoutingConfigurations[configName];
				result.Add((profile, category), config);
			}
		}

		return result;
	}
}
