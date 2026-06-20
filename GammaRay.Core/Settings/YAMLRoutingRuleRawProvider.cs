using GammaRay.Core.Routing;
using GammaRay.Core.Routing.Categorization;
using GammaRay.Core.Routing.NetworkProfiles;
using GammaRay.Core.Routing.Rules;
using GammaRay.Core.Utils.ValueMatching;
using System.Data;
using YamlDotNet.RepresentationModel;

namespace GammaRay.Core.Settings;

public class YAMLRoutingRuleRawProvider : IRawSettingsProvider<IReadOnlyList<RoutingRule>>
{
	private RoutingRule[]? _inbounds;


	public bool IsInitialized => _inbounds is not null;


	public IReadOnlyList<RoutingRule> Get()
	{
		return _inbounds ?? throw new InvalidOperationException("Not initialized");
	}

	public void Initialize(YAMLConfigurationLoader YAMLLoader, EndPointRoutingConfigurationProvider endPointRoutingConfigurations)
	{
		_inbounds = LoadInbounds(YAMLLoader.GetFragment<YamlSequenceNode>("routingRules"), endPointRoutingConfigurations);
	}

	private static RoutingRule[] LoadInbounds(YamlSequenceNode node, EndPointRoutingConfigurationProvider endPointRoutingConfigurations) =>
		node.Select(node =>
		{
			var nodeMap = node.AsMapping();

			var targetConfiguration = endPointRoutingConfigurations.GetConfigurationByName(nodeMap["to"].Bind<string>());

			ValueCondition<string> endPointCategoryCondition = NoneValueCondition<string>.AlwaysTrue;
			if (nodeMap.TryBindChild<string>("endPointCategory", out var endPointCategory))
				endPointCategoryCondition = ValueConditionFactory.Parse(endPointCategory, (value) => new string(value));

			ValueCondition<string> networkProfileCondition = NoneValueCondition<string>.AlwaysTrue;
			if (nodeMap.TryBindChild<string>("networkProfile", out var networkProfile))
				networkProfileCondition = ValueConditionFactory.Parse(networkProfile, (value) => new string(value));

			return new RoutingRule(targetConfiguration)
			{
				EndPointCategoryCondition = endPointCategoryCondition.Select((EndPointCategory category) => category.Name),
				NetworkProfileCondition = networkProfileCondition.Select((NetworkProfile profile) => profile.Name)
			};
		}).ToArray();
}
