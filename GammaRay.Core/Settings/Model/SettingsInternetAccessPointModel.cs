using GammaRay.Core.Network;

namespace GammaRay.Core.Settings.Model;

public sealed class SettingsInternetAccessPointModel
{
	public required SD<Channel> Channels;

	public sealed class Channel
	{
		public required string Protocol;
		public required GenericWebEndPoint EndPoint;
		public SD<string>? Parameters;
		public string[]? Tags;
		public string[]? AvailableInNetwork;
	}
}
