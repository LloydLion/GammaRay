using GammaRay.Core.Settings;

namespace GammaRay.Core.Inbound;

public sealed class InboundConfigurationProvider
{
	public InboundConfigurationProvider(IRawSettingsProvider<IReadOnlyDictionary<string, InboundConfiguration>> rawProvider)
	{
		InboundConfigurations = rawProvider.Get().ToDictionary();
		PlainInboundConfigurations = InboundConfigurations.Values.ToArray();
	}


	public IReadOnlyCollection<InboundConfiguration> PlainInboundConfigurations { get; }

	public IReadOnlyDictionary<string, InboundConfiguration> InboundConfigurations { get; }


	public InboundConfiguration GetConfigurationByName(string name)
	{
		return InboundConfigurations[name];
	}
}
