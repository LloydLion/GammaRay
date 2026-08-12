using System.Net;

namespace GammaRay.Core.Settings.Model;

public sealed class SettingsAPIModel
{
	public required APIEndpointInformation[] EndPoints;

	public sealed class APIEndpointInformation
	{
		public required IPAddress BindAddress;
		public required int Port;
	}
}
