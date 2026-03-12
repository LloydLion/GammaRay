using GammaRay.Core.Network;
using GammaRay.Core.Services;
using GammaRay.Core.Utils.ValueMatching;
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
			var node = kv.Value.AsMapping();

			var detectionRulesNode = node.ExceptChild<YamlSequenceNode>("detectionRules");
			var detectionRules = detectionRulesNode.Children.Select(detectionRuleNodeRaw =>
			{
				var detectionRuleNode = detectionRuleNodeRaw.AsMapping();
				var transportCondition =
					ValueConditionFactory.Parse(detectionRuleNode.TryBindChild<string>("transport"), s => Enum.Parse<TransportType>(s));
				var portCondition =
					ValueConditionFactory.Parse(detectionRuleNode.TryBindChild<string>("port"), s => int.Parse(s));
				return new CapabilityDetectionRule() { Port = portCondition, Transport = transportCondition };
			}).ToArray();

			var capabilityClass = new CapabilityClass(detectionRules);

			return KeyValuePair.Create(kv.Key, capabilityClass);
		}).ToDictionary();
}
