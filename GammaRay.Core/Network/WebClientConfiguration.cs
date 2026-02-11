namespace GammaRay.Core.Network;

public class WebClientConfiguration(string name)
{
	public string Name { get; } = name;

	//public required InternetAccessPoint InternetAccessPoint { get; init; }

	public string[][] RequiredChannelTags { get; init; } = [];
}
