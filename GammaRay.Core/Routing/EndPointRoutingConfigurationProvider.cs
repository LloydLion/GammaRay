using GammaRay.Core.Settings;

namespace GammaRay.Core.Routing;

public sealed class EndPointRoutingConfigurationProvider
{
	public EndPointRoutingConfigurationProvider(IRawSettingsProvider<IReadOnlyDictionary<string, EndPointRoutingConfiguration>> rawProvider)
	{
		EndPointRoutingConfigurations = rawProvider.Get().ToDictionary();
		PlainEndPointRoutingConfigurations = EndPointRoutingConfigurations.Values.ToArray();
	}


	public IReadOnlyCollection<EndPointRoutingConfiguration> PlainEndPointRoutingConfigurations { get; }

	public IReadOnlyDictionary<string, EndPointRoutingConfiguration> EndPointRoutingConfigurations { get; }


	public EndPointRoutingConfiguration GetConfigurationByName(string name)
	{
		return EndPointRoutingConfigurations[name];
	}
}
