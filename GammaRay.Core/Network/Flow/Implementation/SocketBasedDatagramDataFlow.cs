
using GammaRay.Core.Utils;
using Serilog.Debugging;
using System.Net;
using System.Net.Sockets;

namespace GammaRay.Core.Network.Flow.Implementation;

public sealed class SocketBasedDatagramDataFlow : IDatagramDataFlow, IDisposable
{
	private readonly Socket _underlyingSocket;
	private readonly EndPoint _remoteEndPoint;

	private readonly TimeoutHandle _readTimeout;
	private readonly TimeoutHandle _writeTimeout;


	public SocketBasedDatagramDataFlow(Socket underlyingSocket, EndPoint remoteEndPoint)
	{
		if (underlyingSocket.SocketType is not SocketType.Dgram)
			throw new ArgumentException($"Invalid socket type. Use socket of Datagram type. Actual: {underlyingSocket.SocketType}", nameof(underlyingSocket));

		_underlyingSocket = underlyingSocket;
		_remoteEndPoint = remoteEndPoint;

		_readTimeout = new(TimeProvider.System);
		_writeTimeout = new(TimeProvider.System);
	}

	public void Dispose()
	{
		_readTimeout.Dispose();
		_writeTimeout.Dispose();
	}

	public ValueTask<int> ReadDatagramAsync(Memory<byte> buffer, DataFlowReadingOptions readingOptions, CancellationToken cancellationToken)
	{
		DataFlowReadingOptions.InitializeWithDefaultsIfNeed(ref readingOptions);
		return _readTimeout.DoAsyncOperationWithTimeout(readingOptions.Timeout, (buffer, self: this), async static (a, cancellationToken) =>
		{
		nextReceive:
			var result = await a.self._underlyingSocket.ReceiveFromAsync(a.buffer, SocketFlags.None, a.self._remoteEndPoint, cancellationToken);
			if (result.RemoteEndPoint.Equals(a.self._remoteEndPoint) == false)
				goto nextReceive;
			return result.ReceivedBytes;
		}, cancellationToken);
	}

	public ValueTask<int> WriteDatagramAsync(ReadOnlyMemory<byte> buffer, DataFlowWritingOptions writingOptions, CancellationToken cancellationToken)
	{
		DataFlowWritingOptions.InitializeWithDefaultsIfNeed(ref writingOptions);
		return _writeTimeout.DoAsyncOperationWithTimeout(
			writingOptions.Timeout, (buffer, self: this),
			async static (a, cancellationToken) => await a.self._underlyingSocket.SendToAsync(a.buffer, a.self._remoteEndPoint, cancellationToken),
			cancellationToken
		);
	}
}
