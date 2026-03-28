namespace GammaRay.Core.Services.Probing;

public sealed class CapabilityProbingMethod(string driver, IReadOnlyDictionary<string, CapabilityLinkedValue> parameters)
{
	public string Driver { get; } = driver;

	public IReadOnlyDictionary<string, CapabilityLinkedValue> Parameters { get; } = parameters;
}
