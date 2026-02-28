using GammaRay.Core.Services;
using YamlDotNet.RepresentationModel;

namespace GammaRay.Core.Settings;

public class YAMLCapabilityClassRawProvider : IRawSettingsProvider<IReadOnlyDictionary<string, CapabilityClass>>
{
	private IReadOnlyDictionary<string, CapabilityClass>? _capabilityClasses;


	public bool IsInitialized => _capabilityClasses is not null;


	public IReadOnlyDictionary<string, CapabilityClass> Get()
	{
		return _capabilityClasses ?? throw new InvalidOperationException("Not initialized");
	}

	public void Initialize(YAMLConfigurationLoader YAMLLoader)
	{
		_capabilityClasses = LoadCapabilityClasses(YAMLLoader.GetFragment("capabilityClasses"));
	}

	private static Dictionary<string, CapabilityClass> LoadCapabilityClasses(YamlMappingNode node) =>
		node.ScalarChildrenMap.Select(kv =>
		{
			return KeyValuePair.Create(kv.Key, new CapabilityClass());
		}).ToDictionary();
}
