using System.Net.Sockets;

namespace GammaRay.Core.Proxy;

public class ProxyClientContext : IDisposable
{
	private static readonly Random _globalRandom = new();


	public ProxyClientContext(Socket socket, ProxyServer server)
	{
		Socket = socket;
		Server = server;
		Stream = new NetworkStream(socket);

		ClientId = _globalRandom.Next();
	}


	public NetworkStream Stream { get; }

	public Socket Socket { get; }

	public ProxyServer Server { get; }

	public int ClientId { get; }


	public void Dispose()
	{
		Socket.Dispose();
		Stream.Dispose();
		GC.SuppressFinalize(this);
	}

	public override string ToString()
	{
		return $"Client({Stream.Socket.RemoteEndPoint}):{Convert.ToHexString(BitConverter.GetBytes(ClientId))}";
	}
}
