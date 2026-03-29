using GammaRay.Core.Utils;

namespace GammaRay.Core.Network.Flow;

public sealed class DataFlowStreamWrapper : Stream
{
	private readonly IStreamDataFlow _dataFlow;


	public DataFlowStreamWrapper(IStreamDataFlow dataFlow)
	{
		_dataFlow = dataFlow;
	}


	public override bool CanRead => true;

	public override bool CanWrite => true;

	public override bool CanSeek => false;

	public override long Length => throw new NotSupportedException();

	public override bool CanTimeout => true;

	public override long Position
	{
		get => throw new NotSupportedException();
		set => throw new NotSupportedException();
	}

	public override int ReadTimeout
	{
		get => ReadingOptions.Timeout.TotalMillisecondsInt;
		set => ReadingOptions = ReadingOptions with { Timeout = TimeSpan.FromMilliseconds(value) };
	}

	public override int WriteTimeout
	{
		get => WritingOptions.Timeout.TotalMillisecondsInt;
		set => WritingOptions = WritingOptions with { Timeout = TimeSpan.FromMilliseconds(value) };
	}

	public DataFlowReadingOptions ReadingOptions { get; set; }

	public DataFlowWritingOptions WritingOptions { get; set; }


	public override void Flush() { }

	public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
		_dataFlow.ReadAsync(buffer, ReadingOptions, cancellationToken);

	public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
		_dataFlow.ReadAsync(buffer.AsMemory(offset, count), ReadingOptions, cancellationToken).AsTask();

	public override int Read(byte[] buffer, int offset, int count) =>
		_dataFlow.Read(buffer.AsSpan(offset, count), ReadingOptions);

	public async override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
		await _dataFlow.WriteAsync(buffer, WritingOptions, cancellationToken);

	public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
		_dataFlow.WriteAsync(buffer.AsMemory(offset, count), WritingOptions, cancellationToken).AsTask();

	public override void Write(byte[] buffer, int offset, int count) =>
		_dataFlow.WriteAsync(buffer.AsMemory(offset, count), WritingOptions).AsTask().Wait();

	public override long Seek(long offset, SeekOrigin origin)
		=> throw new NotSupportedException();

	public override void SetLength(long value)
		=> throw new NotSupportedException();
}
