using GammaRay.Core.Utils;

namespace GammaRay.Core.Connection.Observation;

public sealed class ConnectionObservationRow(int size)
{
	public RingBuffer<ConnectionObservationFrame> Buffer { get; } = new(size);


	public void Push(ConnectionObservationFrame frame)
	{
		Buffer.Push(frame);
	}
}
