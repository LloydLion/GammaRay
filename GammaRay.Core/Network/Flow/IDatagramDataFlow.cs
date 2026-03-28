namespace GammaRay.Core.Network.Flow;

public interface IDatagramDataFlow : IReadOnlyDatagramDataFlow, IDataFlow
{
	public ValueTask<int> WriteDatagramAsync(ReadOnlyMemory<byte> buffer, DataFlowWritingOptions writingOptions = default, CancellationToken cancellationToken = default);
}
