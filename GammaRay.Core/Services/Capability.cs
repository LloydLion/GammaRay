namespace GammaRay.Core.Services;

public sealed class Capability(CapabilityClass capabilityClass, IReadOnlyDictionary<string, string> properties)
{
	public CapabilityClass Class { get; } = capabilityClass;

	public IReadOnlyDictionary<string, string> Properties { get; } = properties;
}
