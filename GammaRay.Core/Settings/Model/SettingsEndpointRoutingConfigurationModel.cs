using GammaRay.Core.Utils;

namespace GammaRay.Core.Settings.Model;

public sealed class SettingsEndpointRoutingConfigurationModel
{
	public required string[][] IAPChain;
	public RequirementPolicy QueueMode;
	public RequirementPolicy TagsRequirementMode;
	public string[][]? RequiredChannelTags;
	public string[]? DefaultIAPChain;
}
