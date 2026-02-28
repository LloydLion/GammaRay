using GammaRay.Core.InternetAccess;
using GammaRay.Core.Routing;
using GammaRay.Core.Routing.Categorization;
using GammaRay.Core.Routing.NetworkProfiles;
using GammaRay.Core.Utils;
using YamlDotNet.RepresentationModel;

namespace GammaRay.Core.Settings;

public sealed class YAMLEndPointRoutingConfigurationRawProvider : IRawSettingsProvider<IReadOnlyDictionary<string, EndPointRoutingConfiguration>>
{
	private Dictionary<string, EndPointRoutingConfiguration>? _endpointRoutingConfigurations;


	public bool IsInitialized => _endpointRoutingConfigurations is not null;
	

	public IReadOnlyDictionary<string, EndPointRoutingConfiguration> Get()
	{
		return _endpointRoutingConfigurations ?? throw new InvalidOperationException("Not initialized");
	}

	public void Initialize(YAMLConfigurationLoader YAMLLoader, InternetAccessPointProvider internetAccessPointProvider)
	{
		_endpointRoutingConfigurations = LoadEndpointRoutingConfigurations(
			YAMLLoader.GetFragment("endpointRoutingConfigurations"),
			internetAccessPointProvider.PlainInternetAccessPoints
		);
	}

	private static Dictionary<string, EndPointRoutingConfiguration> LoadEndpointRoutingConfigurations(
		YamlMappingNode node,
		IReadOnlyCollection<InternetAccessPoint> accessPoints
	) =>
		node.ScalarChildrenMap.Select(kv =>
		{
			var name = kv.Key;
			var node = (YamlMappingNode)kv.Value;

			var queueMode = node.TryBindChild<RequirementPolicy>("queueMode");
			var tagsRequirementMode = node.TryBindChild<RequirementPolicy>("tagsRequirementMode");
			var requiredChannelTags = node.TryBindChild<string[][]>("requiredChannelTags") ?? [];

			var rawIAPChain = node["IAPChain"].Bind<string[][]>();
			var IAPChain = new InternetAccessPointChain(rawIAPChain.Select(blob => new InternetAccessPointBlob(blob.SelectMany(iapPattern => {
				if (iapPattern.EndsWith('*'))
					return accessPoints.Where(IAP => IAP.Name.StartsWith(iapPattern));
				else return [accessPoints.Single(IAP => IAP.Name == iapPattern)];
			}).ToArray())).ToArray());


			var cfg = new EndPointRoutingConfiguration()
			{
				ChainPolicy = queueMode,
				TagsPolicy = tagsRequirementMode,
				RequiredTags = requiredChannelTags,
				IAPChain = IAPChain,
				DefaultIAPChain = IAPChain.Reverse()
			};

			return KeyValuePair.Create(name, cfg);
		}).ToDictionary();
}
