using Serilog;
using System.Net;
using System.Net.Sockets;

namespace GammaRay.Core.Protocols.HTTP;

public class ProxyClientContext : IDisposable
{
	private static readonly Random GlobalRandom = new();


	public ProxyClientContext(Socket socket, ILogger baseLogger)
	{
		Socket = socket;
		Stream = new NetworkStream(socket);

		ClientId = GlobalRandom.Next();

		Logger = baseLogger.ForContext(nameof(ClientId), ClientId, destructureObjects: false);
	}


	public NetworkStream Stream { get; }

	public Socket Socket { get; }

	public int ClientId { get; }

	public ILogger Logger { get; }


	public void Dispose()
	{
		Socket.Dispose();
		Stream.Dispose();
		GC.SuppressFinalize(this);
	}

	public override string ToString()
	{
		string? remoteEndPointString;
		var remoteEndPoint = Stream.Socket.RemoteEndPoint;
		if (remoteEndPoint is IPEndPoint ipEndPoint && ipEndPoint.Address == IPAddress.Loopback)
			remoteEndPointString = "loop:" + ipEndPoint.Port;
		else remoteEndPointString = remoteEndPoint?.ToString();
		return $"Client({remoteEndPointString}):{Convert.ToHexString(BitConverter.GetBytes(ClientId))}";
	}
}
