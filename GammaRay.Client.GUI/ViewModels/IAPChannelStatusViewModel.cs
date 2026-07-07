namespace GammaRay.Client.GUI.ViewModels;

public sealed class IAPChannelStatusViewModel(
	string channel,
	string network,
	TimeSpan characteristicAccessTime,
	bool isAvailable
)
{
	public string Channel { get; } = channel;

	public string Network { get; } = network;

	public TimeSpan CharacteristicAccessTime { get; } = characteristicAccessTime;

	public bool IsAvailable { get; } = isAvailable;
}
