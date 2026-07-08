using GammaRay.Core.InternetAccess;
using GammaRay.Core.Routing;
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
			YAMLLoader.GetFragment<YamlMappingNode>("endpointRoutingConfigurations"),
			internetAccessPointProvider.InternetAccessPoints
		);
	}

	private static Dictionary<string, EndPointRoutingConfiguration> LoadEndpointRoutingConfigurations(
		YamlMappingNode node,
		IReadOnlyDictionary<string, InternetAccessPoint> accessPoints
	)
	{
		IEnumerable<InternetAccessPoint> getIAPsUsingPattern(string pattern)
		{
			if (pattern.EndsWith('*'))
				return accessPoints.Values.Where(IAP => IAP.Name.StartsWith(pattern.AsSpan()[..^1]));
			else return [accessPoints[pattern]];
		}

		return node.ScalarChildrenMap.Select(kv =>
		{
			var name = kv.Key;
			var node = (YamlMappingNode)kv.Value;

			var queueMode = node.TryBindChild<RequirementPolicy>("queueMode");
			var tagsRequirementMode = node.TryBindChild<RequirementPolicy>("tagsRequirementMode");
			var requiredChannelTags = node.TryBindChild<string[][]>("requiredChannelTags") ?? [];

			var rawIAPChain = node["IAPChain"].Bind<string[][]>();
			var IAPChain = new InternetAccessPointChain(
				rawIAPChain.Select(blob => new InternetAccessPointBlob(blob.SelectMany(getIAPsUsingPattern).ToArray())).ToArray()
			);

			var rawDefaultIAPChain = node.TryBindChild<string[]>("defaultIAPChain");
			var defaultIAPChain = rawDefaultIAPChain is not null
				? rawDefaultIAPChain.SelectMany(getIAPsUsingPattern).ToArray()
				: IAPChain.Reverse().PlainListOfPoints;


			var cfg = new EndPointRoutingConfiguration(name)
			{
				ChainPolicy = queueMode,
				TagsPolicy = tagsRequirementMode,
				RequiredTags = requiredChannelTags,
				IAPChain = IAPChain,
				DefaultIAPChain = defaultIAPChain
			};

			return KeyValuePair.Create(name, cfg);
		}).ToDictionary();
	}
}
