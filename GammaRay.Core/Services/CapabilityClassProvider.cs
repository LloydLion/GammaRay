using GammaRay.Core.Settings;

namespace GammaRay.Core.Services;

public class CapabilityClassProvider
{
	public CapabilityClassProvider(IRawSettingsProvider<IReadOnlyList<KeyValuePair<string, CapabilityClass>>> rawProvider)
	{
		var data = rawProvider.Get();
		CapabilityClasses = data.ToDictionary();
		IndexedCapabilityClasses = data.Select((kv, i) => KeyValuePair.Create(kv.Key, (Index: i, Class: kv.Value))).ToDictionary();
		PlainCapabilityClasses = data.Select(s => s.Value).ToArray();
		InverseLookupTable = data.Select((kv, i) => KeyValuePair.Create(kv.Value, (Index: i, Name: kv.Key))).ToDictionary();
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
