using GammaRay.Core.Settings;

namespace GammaRay.Core.Routing.Rules;

public class RoutingRulesProvider
{
	public RoutingRulesProvider(IRawSettingsProvider<IReadOnlyList<RoutingRule>> rawProvider)
	{
		var rules = rawProvider.Get();
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
