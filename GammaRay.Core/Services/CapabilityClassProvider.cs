using GammaRay.Core.Network;
using GammaRay.Core.Services.Probing;
using GammaRay.Core.Settings.Model;
using GammaRay.Core.Utils.ValueMatching;

namespace GammaRay.Core.Services;

public class CapabilityClassProvider
{
	public CapabilityClassProvider(SettingsModelRoot modelRoot)
	{
		var capabilityClasses = modelRoot.CapabilityClasses.Select(cm => new CapabilityClass(cm.Key,

			cm.Value.DetectionRules.Select(rule => new CapabilityDetectionRule
			{
				Port = rule.Port ?? NoneValueCondition<int>.AlwaysTrue,
				Transport = rule.Transport ?? NoneValueCondition<TransportType>.AlwaysTrue
			}).ToArray(),

			new CapabilityProbingMethod(
				cm.Value.ProbingMethod.Driver,
				cm.Value.ProbingMethod.Parameters.Select(parameter =>
					KeyValuePair.Create(parameter.Key, CapabilityLinkedValue.FromString(parameter.Value))
				).ToDictionary()
			)
		)).Select(capClass => KeyValuePair.Create(capClass.Name, capClass)).ToArray();
		
		CapabilityClasses = capabilityClasses.ToDictionary();
		IndexedCapabilityClasses = capabilityClasses.Select((kv, i) => KeyValuePair.Create(kv.Key, (Index: i, Class: kv.Value))).ToDictionary();
		PlainCapabilityClasses = capabilityClasses.Select(s => s.Value).ToArray();
		InverseLookupTable = capabilityClasses.Select((kv, i) => KeyValuePair.Create(kv.Value, (Index: i, Name: kv.Key))).ToDictionary();
	}


	public IReadOnlyDictionary<string, (int Index, CapabilityClass Class)> IndexedCapabilityClasses { get; }

	public IReadOnlyDictionary<string, CapabilityClass> CapabilityClasses { get; }

	public IReadOnlyList<CapabilityClass> PlainCapabilityClasses { get; }

	public IReadOnlyDictionary<CapabilityClass, (int Index, string Name)> InverseLookupTable { get; }



	public CapabilityClass GetClassByName(string name)
	{
		return CapabilityClasses[name];
	}

	public int IndexOf(CapabilityClass capabilityClass)
	{
		return InverseLookupTable[capabilityClass].Index;
	}
}
