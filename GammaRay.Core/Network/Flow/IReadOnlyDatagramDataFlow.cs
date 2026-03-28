namespace GammaRay.Core.Network.Flow;

public interface IReadOnlyDatagramDataFlow : IReadOnlyDataFlow
{
	public ValueTask<int> ReadDatagramAsync(Memory<byte> buffer, DataFlowReadingOptions readingOptions = default, CancellationToken cancellationToken = default);
}
