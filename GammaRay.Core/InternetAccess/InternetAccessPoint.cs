using GammaRay.Core.InternetAccess.Channels;

namespace GammaRay.Core.InternetAccess;

public sealed class InternetAccessPoint(string name, IReadOnlyDictionary<string, IAPChannel> channels)
{
	public string Name { get; } = name;

	public IReadOnlyDictionary<string, IAPChannel> Channels { get; } = channels;

	public IReadOnlyDictionary<IAPChannel, string> InverseChannels { get; } = channels.ToDictionary(kv => kv.Value, kv => kv.Key);


	public override string ToString()
	{
		return Name;
	}
}
