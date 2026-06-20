using GammaRay.Core.InternetAccess;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Network;
using GammaRay.Core.Routing.NetworkProfiles;
using YamlDotNet.RepresentationModel;

namespace GammaRay.Core.Settings;

public class YAMLInternetAccessPointRawProvider : IRawSettingsProvider<IReadOnlyCollection<InternetAccessPoint>>
{
	private IReadOnlyCollection<InternetAccessPoint>? _internetAccessPoints;


	public bool IsInitialized => _internetAccessPoints is not null;


	public IReadOnlyCollection<InternetAccessPoint> Get()
	{
		return _internetAccessPoints ?? throw new InvalidOperationException("Not initialized");
	}

	public void Initialize(YAMLConfigurationLoader YAMLLoader, NetworkProfileProvider networkProfileProvider)
	{
		_internetAccessPoints = LoadInternetAccessPoints(
			YAMLLoader.GetFragment<YamlMappingNode>("internetAccessPoints"),
			networkProfileProvider.Profiles
		);
	}

	private static InternetAccessPoint[] LoadInternetAccessPoints(
		YamlMappingNode node,
		IReadOnlyDictionary<string, NetworkProfile> profiles
	)
	{
		return node.ScalarChildrenMap.Select(kv =>
		{
			var name = kv.Key;
			var node = kv.Value;

			var channels = ((YamlMappingNode)node["channels"]).ScalarChildrenMap.Select(kv =>
			{
				var channelName = kv.Key;
				var node = (YamlMappingNode)kv.Value;

				string driver;
				GenericWebEndPoint endPoint;
				Dictionary<string, string> parameters;
				string[] tags;
				NetworkProfile[] availableInNetwork;

				if (node.TryBindChild<string>("url", out var uriString))
				{
					var uri = new Uri(uriString);
					driver = uri.Scheme;
					endPoint = new GenericWebEndPoint(new WebHost(uri.Host), uri.Port);
					parameters = uri.Query.TrimStart('?').Split('&').Select(s => s.Split('=')).ToDictionary(s => s[0], s => s[1]);
				}
				else
				{
					driver = node["protocol"].Bind<string>();
					endPoint = node["endPoint"].Bind<GenericWebEndPoint>();
					parameters = node.TryBindChild<Dictionary<string, string>>("parameters") ?? [];
				}

				tags = node.TryBindChild<string[]>("tags") ?? [];
				availableInNetwork = node.TryBindChild<string[]>("availableInNetwork")?.Select(n => profiles[n]).ToArray() ?? [];

				var channel = new IAPChannel(driver, endPoint)
				{
					Parameters = parameters,
					Tags = tags,
					AvailableInNetwork = availableInNetwork
				};

				return KeyValuePair.Create(channelName, channel);
			}).ToDictionary();

			return new InternetAccessPoint(name, channels);
		})
		.ToArray();
	}
}
