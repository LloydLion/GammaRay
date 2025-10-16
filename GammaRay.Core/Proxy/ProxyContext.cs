using System.Net.Sockets;

namespace GammaRay.Core.Proxy;

public class ProxyContext : IDisposable
{
	public ProxyContext(TcpClient client, ProxyServer server)
	{
		Client = client;
		Server = server;
		Stream = Client.GetStream();
	}


	public NetworkStream Stream { get; }

	public TcpClient Client { get; }

	public ProxyServer Server { get; }


	public void Dispose()
	{
		Client.Dispose();
		Stream.Dispose();
	}
}
