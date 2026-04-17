using GammaRay.Core.Network;
using GammaRay.Core.Services;
using GammaRay.Core.Services.Probing;
using GammaRay.Core.Utils.ValueMatching;
using YamlDotNet.RepresentationModel;

namespace GammaRay.Core.Settings;

public class YAMLCapabilityClassRawProvider : IRawSettingsProvider<IReadOnlyList<KeyValuePair<string, CapabilityClass>>>
{
	private IReadOnlyList<KeyValuePair<string, CapabilityClass>>? _capabilityClasses;


	public bool IsInitialized => _capabilityClasses is not null;


	public IReadOnlyList<KeyValuePair<string, CapabilityClass>> Get()
	{
		return _capabilityClasses ?? throw new InvalidOperationException("Not initialized");
	}

	public void Initialize(YAMLConfigurationLoader YAMLLoader)
	{
		_capabilityClasses = LoadCapabilityClasses(YAMLLoader.GetFragment("capabilityClasses"));
	}

	private static KeyValuePair<string, CapabilityClass>[] LoadCapabilityClasses(YamlMappingNode node) =>
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

			var probingMethodNode = node.ExceptMappingChild("probingMethod");
			var probingMethodDriver = probingMethodNode["driver"].Bind<string>();
			var probingMethodParameters = probingMethodNode.ExceptMappingChild("parameters").ScalarChildrenMap
				.Select(kv =>
				{
					var rawParameter = kv.Value.Bind<string>();
					if (rawParameter.StartsWith('.'))
						return KeyValuePair.Create(kv.Key, CapabilityLinkedValue.Property(rawParameter[1..]));
					return KeyValuePair.Create(kv.Key, CapabilityLinkedValue.Constant(rawParameter));
				}).ToDictionary();
			var probingMethod = new CapabilityProbingMethod(probingMethodDriver, probingMethodParameters);

			var capabilityClass = new CapabilityClass(kv.Key, detectionRules, probingMethod);

			return KeyValuePair.Create(kv.Key, capabilityClass);
		}).ToArray();
}
