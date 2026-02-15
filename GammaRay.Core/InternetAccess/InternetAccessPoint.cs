using GammaRay.Core.InternetAccess.Channels;

namespace GammaRay.Core.InternetAccess;

public sealed class InternetAccessPoint(string name)
{
	public string Name { get; } = name;

	public required IReadOnlyDictionary<string, IAPChannel> Channels { get; init; }
}
