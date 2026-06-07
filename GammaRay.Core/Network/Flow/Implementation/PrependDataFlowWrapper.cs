namespace GammaRay.Core.Network.Flow.Implementation;

public sealed class PrependDataFlowWrapper : IStreamDataFlow
{
	private readonly ReadOnlyMemory<byte> _prependBuffer;
	private readonly IStreamDataFlow _dataFlow;
	private int _prependPosition;


	public PrependDataFlowWrapper(ReadOnlyMemory<byte> prependBuffer, IStreamDataFlow dataFlow)
	{
		_prependBuffer = prependBuffer;
		_dataFlow = dataFlow;
	}


	public int Read(Span<byte> buffer, DataFlowReadingOptions readingOptions = default)
	{
		if (ReadBuffer(buffer, readingOptions.PeekOnly, out var readFromBuffer))
		{
			if (readFromBuffer > 0) return readFromBuffer;
			
			return _dataFlow.Read(buffer, readingOptions);
		}
		return buffer.Length;
	}

	public async ValueTask<int> ReadAsync(Memory<byte> buffer, DataFlowReadingOptions readingOptions = default, CancellationToken cancellationToken = default)
	{
		if (ReadBuffer(buffer.Span, readingOptions.PeekOnly, out var readFromBuffer))
		{
			if (readFromBuffer > 0) return readFromBuffer;

			return await _dataFlow.ReadAsync(buffer, readingOptions, cancellationToken);
		}
		return buffer.Length;
	}

	private bool ReadBuffer(Span<byte> buffer, bool peekOnly, out int readFromBuffer)
	{
		var prependAvailable = _prependBuffer.Length - _prependPosition;
		if (prependAvailable >= buffer.Length)
		{
			_prependBuffer.Span[_prependPosition..buffer.Length].CopyTo(buffer);
			if (!peekOnly) _prependPosition += buffer.Length;
			readFromBuffer = buffer.Length;
			return false;
		}
		else
		{
			_prependBuffer.Span[_prependPosition..].CopyTo(buffer[..prependAvailable]);
			if (!peekOnly) _prependPosition = _prependBuffer.Length;
			readFromBuffer = prependAvailable;
			return true;
		}
	}

	public ValueTask<int> WriteAsync(ReadOnlyMemory<byte> buffer, DataFlowWritingOptions writingOptions = default, CancellationToken cancellationToken = default)
	{
		return _dataFlow.WriteAsync(buffer, writingOptions, cancellationToken);
	}
}
