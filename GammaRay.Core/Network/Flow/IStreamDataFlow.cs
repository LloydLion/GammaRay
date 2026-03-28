namespace GammaRay.Core.Network.Flow;

public interface IStreamDataFlow : IReadOnlyStreamDataFlow, IDataFlow
{
	public ValueTask<int> WriteAsync(ReadOnlyMemory<byte> buffer, DataFlowWritingOptions writingOptions = default, CancellationToken cancellationToken = default);
}
