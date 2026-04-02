namespace GammaRay.Core.InternetAccess.Channels;

public readonly record struct IAPChannelRequirements()
{
	public string[][] RequiredTags { get; init; } = [];
}
