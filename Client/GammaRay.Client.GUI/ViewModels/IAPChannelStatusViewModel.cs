namespace GammaRay.Client.GUI.ViewModels;

public sealed class IAPChannelStatusViewModel(
	string channel,
	string network,
	TimeSpan characteristicAccessTime,
	TimeSpan averageAccessTime,
	bool isAvailable,
	TimeSpan averageLifeTime
)
{
	public string Channel { get; } = channel;

	public string Network { get; } = network;

	public TimeSpan CharacteristicAccessTime { get; } = characteristicAccessTime;

	public TimeSpan AverageAccessTime { get; } = averageAccessTime;

	public bool IsAvailable { get; } = isAvailable;

	public TimeSpan AverageLifeTime { get; } = averageLifeTime;
}
