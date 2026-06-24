using System.Net;

namespace GammaRay.Core.API;

public sealed class APIEndpointInformation(IPAddress bindAddress, int port)
{
	public IPAddress BindAddress { get; } = bindAddress;

	public int Port { get; } = port;
}
