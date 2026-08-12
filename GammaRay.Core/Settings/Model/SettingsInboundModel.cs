using System.Net;

namespace GammaRay.Core.Settings.Model;

public sealed class SettingsInboundModel
{
	public required string Protocol;
	public required IPEndPoint EndPoint;
}
