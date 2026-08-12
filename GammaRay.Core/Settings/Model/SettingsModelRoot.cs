namespace GammaRay.Core.Settings.Model;

public sealed class SettingsModelRoot
{
	public SettingsAPIModel? API;
	public required SD<SettingsInboundModel> Inbounds;
	public required SD<SettingsInternetAccessPointModel> InternetAccessPoints;
	public required SD<SettingsCapabilityClassModel> CapabilityClasses;
	public required SD<SettingsEndpointRoutingConfigurationModel> EndpointRoutingConfigurations;
	public required SD<SettingsNetworkProfileModel> NetworkProfiles;
	public required SD<SettingsEndPointCategoryModel> EndPointCategories;
	public required SettingsRoutingRuleModel[] RoutingRules;
}
