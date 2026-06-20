using GammaRay.Core.Routing.Categorization;
using GammaRay.Core.Routing.NetworkProfiles;
using GammaRay.Core.Utils.ValueMatching;

namespace GammaRay.Core.Routing.Rules;

public sealed class RoutingRule(EndPointRoutingConfiguration targetConfiguration)
{
	public ValueCondition<EndPointCategory> EndPointCategoryCondition { get; init; } = NoneValueCondition<EndPointCategory>.AlwaysTrue;

	public ValueCondition<NetworkProfile> NetworkProfileCondition { get; init; } = NoneValueCondition<NetworkProfile>.AlwaysTrue;

	public EndPointRoutingConfiguration TargetConfiguration { get; } = targetConfiguration;


	public bool IsMatch(RoutingContext context)
	{
		return EndPointCategoryCondition.IsMatch(context.EndPointCategory) && NetworkProfileCondition.IsMatch(context.NetworkProfile);
	}
}

public readonly record struct RoutingContext(EndPointCategory EndPointCategory, NetworkProfile NetworkProfile);
