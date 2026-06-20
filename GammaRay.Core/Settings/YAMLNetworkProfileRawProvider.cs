using GammaRay.Core.Routing.NetworkProfiles;
using YamlDotNet.RepresentationModel;

namespace GammaRay.Core.Settings;

public class YAMLNetworkProfileRawProvider : IRawSettingsProvider<IReadOnlyCollection<NetworkProfile>>
{
	private IReadOnlyCollection<NetworkProfile>? _profiles;


	public bool IsInitialized => _profiles is not null;


	public IReadOnlyCollection<NetworkProfile> Get()
	{
		return _profiles ?? throw new InvalidOperationException("Not initialized");
	}

	public void Initialize(YAMLConfigurationLoader YAMLLoader)
	{
		_profiles = LoadNetworkProfiles(YAMLLoader.GetFragment<YamlMappingNode>("networkProfiles"));
	}

	private static NetworkProfile[] LoadNetworkProfiles(YamlMappingNode node) =>
		node.ScalarChildrenMap.Select(kv => new NetworkProfile(kv.Key)).ToArray();
}
