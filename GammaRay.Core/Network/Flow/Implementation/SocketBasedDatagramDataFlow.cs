
using System.Net;
using System.Net.Sockets;

namespace GammaRay.Core.Network.Flow.Implementation;

public sealed class SocketBasedDatagramDataFlow : IDatagramDataFlow
{
	private readonly Socket _underlyingSocket;
	private readonly EndPoint _remoteEndPoint;


	public SocketBasedDatagramDataFlow(Socket underlyingSocket, EndPoint remoteEndPoint)
	{
		if (underlyingSocket.SocketType is not SocketType.Dgram)
			throw new ArgumentException($"Invalid socket type. Use socket of Datagram type. Actual: {underlyingSocket.SocketType}", nameof(underlyingSocket));

		_underlyingSocket = underlyingSocket;
		_remoteEndPoint = remoteEndPoint;
	}


	public async ValueTask<int> ReadDatagramAsync(Memory<byte> buffer, CancellationToken cancellationToken)
	{
	tryAgain:
		var result = await _underlyingSocket.ReceiveFromAsync(buffer, SocketFlags.None, _remoteEndPoint, cancellationToken);
		if (result.RemoteEndPoint.Equals(_remoteEndPoint) == false)
			goto tryAgain;
		return result.ReceivedBytes;
	}

	public async ValueTask<int> WriteDatagramAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
	{
		return await _underlyingSocket.SendToAsync(buffer, _remoteEndPoint, cancellationToken);
	}
}
