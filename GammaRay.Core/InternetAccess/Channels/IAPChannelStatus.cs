namespace GammaRay.Core.InternetAccess.Channels;

public readonly record struct IAPChannelStatus(TimeSpan CharacteristicAccessTime, TimeSpan AverageAccessTime, double AccessChance, bool IsAvailable, TimeSpan AverageLifeTime)
{
	public static IAPChannelStatus BestStatus { get; } = new IAPChannelStatus(TimeSpan.Zero, TimeSpan.Zero, 1.0, true, TimeSpan.MaxValue);
}
