using System.Runtime.CompilerServices;

namespace GammaRay.Core.Network.Flow;

public sealed class FlowJoinTask(Task task, IDataFlow flowA, IDataFlow flowB, Memory<byte> bufferAToB, Memory<byte> bufferBToA, IFlowJoinObserver? observer = null)
{
	public Task Task { get; } = task;

	public IDataFlow FlowA { get; } = flowA;

	public IDataFlow FlowB { get; } = flowB;

	public Memory<byte> BufferAToB { get; } = bufferAToB;

	public Memory<byte> BufferBToA { get; } = bufferBToA;

	public IFlowJoinObserver? Observer { get; } = observer;


	public TaskAwaiter GetAwaiter() => Task.GetAwaiter();
}
