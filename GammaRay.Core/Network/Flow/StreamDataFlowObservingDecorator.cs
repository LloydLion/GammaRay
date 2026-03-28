namespace GammaRay.Core.Network.Flow;

public sealed class StreamDataFlowObservingDecorator : IStreamDataFlow
{
	private readonly IStreamDataFlow _baseFlow;
	private readonly List<Action<ReadOnlySpan<byte>>> _readHooks = [];
	private readonly List<Action<ReadOnlySpan<byte>>> _writeHooks = [];


	public StreamDataFlowObservingDecorator(IStreamDataFlow baseFlow)
	{
		_baseFlow = baseFlow;
	}


	public StreamDataFlowObservingDecorator AddReadHook(Action<ReadOnlySpan<byte>> hook)
	{
		_readHooks.Add(hook);
		return this;
	}

	public StreamDataFlowObservingDecorator AddWriteHook(Action<ReadOnlySpan<byte>> hook)
	{
		_writeHooks.Add(hook);
		return this;
	}

	public int Read(Span<byte> buffer, DataFlowReadingOptions readingOptions = default)
	{
		var read = _baseFlow.Read(buffer, readingOptions);
		var realBuffer = buffer[..read];
		foreach (var hook in _readHooks)
			hook(realBuffer);
		return read;
	}

	public async ValueTask<int> ReadAsync(Memory<byte> buffer, DataFlowReadingOptions readingOptions = default, CancellationToken cancellationToken = default)
	{
		var read = await _baseFlow.ReadAsync(buffer, readingOptions, cancellationToken);
		var realBuffer = buffer.Span[..read];
		foreach (var hook in _readHooks)
			hook(realBuffer);
		return read;
	}

	public async ValueTask<int> WriteAsync(ReadOnlyMemory<byte> buffer, DataFlowWritingOptions writingOptions = default, CancellationToken cancellationToken = default)
	{
		var wrote = await _baseFlow.WriteAsync(buffer, writingOptions, cancellationToken);
		var realBuffer = buffer.Span[..wrote];
		foreach (var hook in _writeHooks)
			hook(realBuffer);
		return wrote;
	}
}
