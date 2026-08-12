using GammaRay.Core.Settings.Model;

namespace GammaRay.Core.Connection.Inbound;

public sealed class InboundConfigurationProvider
{
	public InboundConfigurationProvider(SettingsModelRoot modelRoot)
	{
		InboundConfigurations = modelRoot.Inbounds
			.Select(cm => KeyValuePair.Create(cm.Key, new InboundConfiguration(cm.Value.Protocol, cm.Value.EndPoint)))
			.ToDictionary();
		
		PlainInboundConfigurations = InboundConfigurations.Values.ToArray();
	}


	public IReadOnlyCollection<InboundConfiguration> PlainInboundConfigurations { get; }

	public IReadOnlyDictionary<string, InboundConfiguration> InboundConfigurations { get; }


	public InboundConfiguration GetConfigurationByName(string name)
	{
		return InboundConfigurations[name];
	}
}
