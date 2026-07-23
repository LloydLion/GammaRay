using GammaRay.Core.Connection.Inbound;
using System.Net;
using YamlDotNet.RepresentationModel;

namespace GammaRay.Core.Settings;

public class YAMLInboundRawProvider : IRawSettingsProvider<IReadOnlyDictionary<string, InboundConfiguration>>
{
	private IReadOnlyDictionary<string, InboundConfiguration>? _inbounds;


	public bool IsInitialized => _inbounds is not null;


	public IReadOnlyDictionary<string, InboundConfiguration> Get()
	{
		return _inbounds ?? throw new InvalidOperationException("Not initialized");
	}

	public void Initialize(YAMLConfigurationLoader YAMLLoader)
	{
		_inbounds = LoadInbounds(YAMLLoader.GetFragment<YamlMappingNode>("inbounds"));
	}

	private static Dictionary<string, InboundConfiguration> LoadInbounds(YamlMappingNode node) =>
		node.ScalarChildrenMap.Select(kv =>
		{
			var name = kv.Key;
			var node = kv.Value;
			InboundConfiguration result;
			if (node is YamlScalarNode scalarNode)
			{
				var uri = new Uri(scalarNode.Value!);
				var protocol = uri.Scheme;
				var endPoint = new IPEndPoint(IPAddress.Parse(uri.Host), uri.Port);
				result = new InboundConfiguration(protocol, endPoint);
			}
			else result = node.Bind<InboundConfiguration>();
			return KeyValuePair.Create(name, result);
		}).ToDictionary();
}
