using GammaRay.Core.API;
using GammaRay.Core.InternetAccess;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Network;
using GammaRay.Core.Routing.NetworkProfiles;
using System.Net;
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
			var bindAddress = IPAddress.Parse(node["bindAddress"].Bind<string>());
			var port = node["port"].Bind<int>();
			return new APIEndpointInformation(bindAddress, port);
		})
		.ToArray();

		return new APIConfiguration(endPoints);
	}
}
