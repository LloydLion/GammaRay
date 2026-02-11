namespace GammaRay.Core.Network.Flow;

public interface IReadOnlyStreamDataFlow : IReadOnlyDataFlow
{
	public int DataAvailable { get; }


	public void Read(Span<byte> buffer);

	public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
}
