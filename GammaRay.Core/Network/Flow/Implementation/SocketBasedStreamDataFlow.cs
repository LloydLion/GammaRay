using GammaRay.Core.Utils;
using System.Net.Sockets;

namespace GammaRay.Core.Network.Flow.Implementation;

public sealed class SocketBasedStreamDataFlow : IStreamDataFlow, IDisposable
{
	private readonly Socket _underlyingSocket;

	private readonly TimeoutHandle _readTimeout;
	private readonly TimeoutHandle _writeTimeout;

	private TimeSpan _currentReceiveTimeout = new DataFlowReadingOptions().Timeout;


	public SocketBasedStreamDataFlow(Socket underlyingSocket)
	{
		if (underlyingSocket.SocketType is not SocketType.Stream)
			throw new ArgumentException($"Invalid socket type. Use socket of Stream type. Actual: {underlyingSocket.SocketType}", nameof(underlyingSocket));

		_underlyingSocket = underlyingSocket;

		_readTimeout = new(TimeProvider.System);
		_writeTimeout = new(TimeProvider.System);

		_underlyingSocket.SetReceiveTimeout(_currentReceiveTimeout);
	}


	public ValueTask<int> ReadAsync(Memory<byte> buffer, DataFlowReadingOptions readingOptions, CancellationToken cancellationToken)
	{
		DataFlowReadingOptions.InitializeWithDefaultsIfNeed(ref readingOptions);
		return _readTimeout.DoAsyncOperationWithTimeout(
			readingOptions.Timeout,
			(buffer, readingOptions, self: this),
			static (a, cancellationToken) =>
				a.self._underlyingSocket.ReceiveAsync(a.buffer, CreateSocketFlagsForReading(a.readingOptions), cancellationToken),
			cancellationToken
		);
	}

	public ValueTask<int> WriteAsync(ReadOnlyMemory<byte> buffer, DataFlowWritingOptions writingOptions, CancellationToken cancellationToken)
	{
		DataFlowWritingOptions.InitializeWithDefaultsIfNeed(ref writingOptions);
		return _writeTimeout.DoAsyncOperationWithTimeout(writingOptions.Timeout, buffer, _underlyingSocket.SendAsync, cancellationToken);
	}

	public int Read(Span<byte> buffer, DataFlowReadingOptions readingOptions)
	{
		DataFlowReadingOptions.InitializeWithDefaultsIfNeed(ref readingOptions);
		if (readingOptions.Timeout != _currentReceiveTimeout)
			_underlyingSocket.SetReceiveTimeout(_currentReceiveTimeout = readingOptions.Timeout);
		return _underlyingSocket.Receive(buffer, CreateSocketFlagsForReading(readingOptions));
	}

	public void Dispose()
	{
		_readTimeout.Dispose();
		_writeTimeout.Dispose();
	}

	private static SocketFlags CreateSocketFlagsForReading(DataFlowReadingOptions readingOptions)
	{
		return readingOptions.PeekOnly ? SocketFlags.Peek : SocketFlags.None;
	}
}
