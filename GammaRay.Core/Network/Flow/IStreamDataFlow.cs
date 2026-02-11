namespace GammaRay.Core.Network.Flow;

public interface IStreamDataFlow : IReadOnlyStreamDataFlow, IDataFlow
{
	public ValueTask<int> WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default);
}
