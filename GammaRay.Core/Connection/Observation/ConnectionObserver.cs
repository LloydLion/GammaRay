using GammaRay.Core.Network.Flow;

namespace GammaRay.Core.Connection.Observation;

public sealed class ConnectionObservationContext
{
	public long BytesReceived { get; set; }

	public long BytesSent { get; set; }
}

public readonly record struct ConnectionObservationFrame(long BytesReceived, long BytesSent);


public sealed class ConnectionObserver : IFlowJoinObserver
{
	private readonly ConnectionObservationContext _context = new();


	public ConnectionObservationContext Context => _context;


	public void ResetContext()
	{
		_context.BytesSent = _context.BytesReceived = 0;
	}

	void IFlowJoinObserver.NotifyDataFromAToB(ReadOnlyMemory<byte> data)
	{
		_context.BytesSent += data.Length;
	}

	void IFlowJoinObserver.NotifyDataFromBToA(ReadOnlyMemory<byte> data)
	{
		_context.BytesReceived += data.Length;
	}

	void IFlowJoinObserver.NotifyEndOfJoin() { }
}
