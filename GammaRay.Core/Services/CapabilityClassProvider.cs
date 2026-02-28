using GammaRay.Core.Settings;

namespace GammaRay.Core.Services;

public class CapabilityClassProvider
{
	public CapabilityClassProvider(IRawSettingsProvider<IReadOnlyDictionary<string, CapabilityClass>> rawProvider)
	{
		CapabilityClasses = rawProvider.Get().ToDictionary();
		PlainCapabilityClasses = CapabilityClasses.Values.ToArray();
	}


	public IReadOnlyDictionary<string, CapabilityClass> CapabilityClasses { get; }

	public IReadOnlyCollection<CapabilityClass> PlainCapabilityClasses { get; }


	public CapabilityClass GetClassByName(string name)
	{
		return CapabilityClasses[name];
	}
}
