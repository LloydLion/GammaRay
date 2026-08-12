using GammaRay.Core.Utils.ValueMatching;

namespace GammaRay.Core.Settings.Model;

public sealed class SettingsRoutingRuleModel
{
	public required string To;
	public ValueCondition<string>? EndPointCategory;
	public ValueCondition<string>? NetworkProfile;
}
