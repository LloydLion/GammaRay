namespace GammaRay.Core.Network.Flow;

public interface IReadOnlyStreamDataFlow : IReadOnlyDataFlow
{
	public int Read(Span<byte> buffer, DataFlowReadingOptions readingOptions = default);

	public ValueTask<int> ReadAsync(Memory<byte> buffer, DataFlowReadingOptions readingOptions = default, CancellationToken cancellationToken = default);
}

public static class ReadOnlyStreamDataFlowExtensions
{
	extension(IReadOnlyStreamDataFlow flow)
	{
		public int ReadByte(DataFlowReadingOptions readingOptions = default)
		{
			Span<byte> buffer = stackalloc byte[1];
			var read = flow.Read(buffer, readingOptions);
			if (read == 0)
				return -1;
			return buffer[0];
		}
	}
}
