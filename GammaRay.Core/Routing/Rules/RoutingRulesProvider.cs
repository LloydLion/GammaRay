using GammaRay.Core.Network.Profiles;
using GammaRay.Core.Routing.Categorization;
using GammaRay.Core.Settings.Model;
using GammaRay.Core.Utils.ValueMatching;

namespace GammaRay.Core.Routing.Rules;

public class RoutingRulesProvider
{
	public RoutingRulesProvider(SettingsModelRoot modelRoot, EndPointRoutingConfigurationProvider endpointRoutingConfigurations)
	{
		var rules = modelRoot.RoutingRules.Select(cm => new RoutingRule(endpointRoutingConfigurations.GetConfigurationByName(cm.To))
		{
			EndPointCategoryCondition = cm.EndPointCategory?.Select((EndPointCategory c) => c.Name) ??
				(ValueCondition<EndPointCategory>)NoneValueCondition<EndPointCategory>.AlwaysTrue,
			NetworkProfileCondition = cm.NetworkProfile?.Select((NetworkProfile c) => c.Name) ?? 
				(ValueCondition<NetworkProfile>)NoneValueCondition<NetworkProfile>.AlwaysTrue
		});
		Rules = rules.ToArray();
	}


	public IReadOnlyList<RoutingRule> Rules { get; }


	public EndPointRoutingConfiguration? Route(RoutingContext context)
	{
		foreach (var rule in Rules)
			if (rule.IsMatch(context))
				return rule.TargetConfiguration;

		return null;
	}
}
