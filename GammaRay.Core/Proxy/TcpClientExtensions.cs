using System.Net.Sockets;

namespace GammaRay.Core.Proxy;

internal static class TcpClientExtensions
{
	public static Task ConnectAsync(this TcpClient client, HttpEndPoint endPoint)
	{
		return client.ConnectAsync(endPoint.Host.DomainName, endPoint.Port);
	}
}
