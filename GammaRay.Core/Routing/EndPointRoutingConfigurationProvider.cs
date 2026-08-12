using GammaRay.Core.InternetAccess;
using GammaRay.Core.Settings.Model;

namespace GammaRay.Core.Routing;

public sealed class EndPointRoutingConfigurationProvider
{
	public EndPointRoutingConfigurationProvider(SettingsModelRoot modelRoot, InternetAccessPointProvider internetAccessPoints)
	{
		IEnumerable<InternetAccessPoint> getIAPsByPattern(string pattern)
		{
			if (pattern.EndsWith('*'))
				return internetAccessPoints.PlainInternetAccessPoints.Where(IAP => IAP.Name.StartsWith(pattern.AsSpan()[..^1]));
			return [internetAccessPoints.InternetAccessPoints[pattern]];
		}
		
		var configurations = modelRoot.EndpointRoutingConfigurations.Select(cm =>
		{
			var chain = new InternetAccessPointChain(
				cm.Value.IAPChain.Select(blob => new InternetAccessPointBlob(
					blob.SelectMany(getIAPsByPattern).ToArray())
				).ToArray()
			);
			return new EndPointRoutingConfiguration(cm.Key)
			{
				ChainPolicy = cm.Value.QueueMode,
				IAPChain = chain,
				RequiredTags = cm.Value.RequiredChannelTags ?? [],
				TagsPolicy = cm.Value.TagsRequirementMode,
				DefaultIAPChain = cm.Value.DefaultIAPChain?
					.SelectMany(getIAPsByPattern).ToArray() ?? chain.Reverse().PlainListOfPoints
			};
		}).ToArray();
		
		EndPointRoutingConfigurations = configurations.ToDictionary(c => c.Name);
		PlainEndPointRoutingConfigurations = EndPointRoutingConfigurations.Values.ToArray();
	}


	public IReadOnlyCollection<EndPointRoutingConfiguration> PlainEndPointRoutingConfigurations { get; }

	public IReadOnlyDictionary<string, EndPointRoutingConfiguration> EndPointRoutingConfigurations { get; }


	public EndPointRoutingConfiguration GetConfigurationByName(string name)
	{
		return EndPointRoutingConfigurations[name];
	}
}
