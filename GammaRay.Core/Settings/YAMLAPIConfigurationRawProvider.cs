using GammaRay.Core.API;
using GammaRay.Core.InternetAccess;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Network;
using GammaRay.Core.Routing.NetworkProfiles;
using YamlDotNet.RepresentationModel;

namespace GammaRay.Core.Settings;

public sealed class YAMLAPIConfigurationRawProvider : IRawSettingsProvider<APIConfiguration>
{
	private APIConfiguration? _configuration;


	public bool IsInitialized => _configuration is not null;


	public APIConfiguration Get()
	{
		return _configuration ?? throw new InvalidOperationException("Not initialized");
	}

	public void Initialize(YAMLConfigurationLoader YAMLLoader)
	{
		_configuration = LoadAPIConfiguration(YAMLLoader.GetFragment<YamlMappingNode>("api"));
	}

	private static APIConfiguration LoadAPIConfiguration(YamlMappingNode node)
	{
		var endPoints = node.ExceptChild<YamlSequenceNode>("endPoints").Select(node =>
		{
			var protocol = node["protocol"].Bind<string>();
			var configurationString = node["configuration"].Bind<string>();
			return new APIEndpointInformation(protocol, configurationString);
		})
		.ToArray();

		return new APIConfiguration(endPoints);
	}
}
