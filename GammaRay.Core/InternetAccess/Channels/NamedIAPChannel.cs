namespace GammaRay.Core.InternetAccess.Channels;

public readonly record struct NamedIAPChannel(InternetAccessPoint IAP, string ChannelName, IAPChannel Channel)
{
	public static implicit operator IAPChannel(NamedIAPChannel channel) => channel.Channel;


	public static NamedIAPChannel CreateUsingInverseTable(InternetAccessPoint IAP, IAPChannel channel) => new(IAP, IAP.InverseChannels[channel], channel);

	public static NamedIAPChannel CreateUsingForwardTable(InternetAccessPoint IAP, string channel) => new(IAP, channel, IAP.Channels[channel]);


	public override string ToString()
	{
		return $"{IAP.Name}/{ChannelName}";
	}
}
