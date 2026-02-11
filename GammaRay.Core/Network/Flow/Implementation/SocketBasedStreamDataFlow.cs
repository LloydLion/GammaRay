using System.Net.Sockets;

namespace GammaRay.Core.Network.Flow.Implementation;

public sealed class SocketBasedStreamDataFlow : IStreamDataFlow
{
	private readonly Socket _underlyingSocket;


	public SocketBasedStreamDataFlow(Socket underlyingSocket)
	{
		if (underlyingSocket.SocketType is not SocketType.Stream)
			throw new ArgumentException($"Invalid socket type. Use socket of Stream type. Actual: {underlyingSocket.SocketType}", nameof(underlyingSocket));

		_underlyingSocket = underlyingSocket;
	}


	public int DataAvailable => _underlyingSocket.Available;


	public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
	{
		return await _underlyingSocket.ReceiveAsync(buffer, cancellationToken);
	}

	public async ValueTask<int> WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
	{
		return await _underlyingSocket.SendAsync(buffer, cancellationToken);
	}

	public void Read(Span<byte> buffer)
	{
		_underlyingSocket.Receive(buffer);
	}
}
