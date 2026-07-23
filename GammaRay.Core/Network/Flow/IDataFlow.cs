namespace GammaRay.Core.Network.Flow;

/*
 * IReadOnlyDataFlow -> IDataFlow (2)
 *   |
 *   | -> IReadOnlyStreamDataFlow -> IStreamDataFlow <- (2)
 *   |
 *   | -> IReadOnlyDatagramDataFlow -> IDatagramDataFlow <- (2)
 */

public interface IDataFlow : IReadOnlyDataFlow
{

}

static class DataFlowExtensions
{
	extension(IDataFlow flowA)
	{
		public FlowJoinTask JoinAsync(IDataFlow other, IFlowJoinObserver? observer = null) => flowA.JoinAsync(other, new byte[ushort.MaxValue], new byte[ushort.MaxValue], observer);

		public FlowJoinTask JoinAsync(IDataFlow flowB, Memory<byte> bufferAToB, Memory<byte> bufferBToA, IFlowJoinObserver? observer = null) =>
			new (flowA.JoinInternalAsync(flowB, new byte[ushort.MaxValue], new byte[ushort.MaxValue], observer),
				flowA, flowB, bufferAToB, bufferBToA, observer);

		private async Task JoinInternalAsync(IDataFlow flowB, Memory<byte> bufferAToB, Memory<byte> bufferBToA, IFlowJoinObserver? observer = null)
		{
			if (bufferAToB.Length is not ushort.MaxValue || bufferBToA.Length is not ushort.MaxValue)
				throw new ArgumentException($"Length of each memory must be {ushort.MaxValue}(aka ushort.MaxValue, maximum size of IP packet)");

			Func<IDataFlow, Memory<byte>, CancellationToken, ValueTask<int>> readDelegate, writeDelegate;

			switch ((flowB, flowA))
			{
				case (IStreamDataFlow, IStreamDataFlow):
					readDelegate = static (flow, buffer, cancel) => ((IStreamDataFlow)flow).ReadAsync(buffer, new() { Timeout = Timeout.InfiniteTimeSpan }, cancel);
					writeDelegate = static (flow, buffer, cancel) => ((IStreamDataFlow)flow).WriteAsync(buffer, new() { Timeout = Timeout.InfiniteTimeSpan }, cancel);
					break;
				case (IDatagramDataFlow, IDatagramDataFlow):
					readDelegate = static (flow, buffer, cancel) => ((IDatagramDataFlow)flow).ReadDatagramAsync(buffer, new() { Timeout = Timeout.InfiniteTimeSpan }, cancel);
					writeDelegate = static (flow, buffer, cancel) => ((IDatagramDataFlow)flow).WriteDatagramAsync(buffer, new() { Timeout = Timeout.InfiniteTimeSpan }, cancel);
					break;

				case (IDatagramDataFlow, IStreamDataFlow) or (IStreamDataFlow, IDatagramDataFlow):
					throw new InvalidOperationException("2 data flows must be same type (both datagram or stream)");
				default:
					ThrowNotSupportedDataFlowType<int>();
					return;
			}

			var cts = new CancellationTokenSource();

			var taskAToB = pipeFlows(bufferAToB, observer, direction: true, readDelegate, writeDelegate, flowA, flowB, cts.Token);
			var taskBToA = pipeFlows(bufferBToA, observer, direction: false, readDelegate, writeDelegate, flowB, flowA, cts.Token);

			var completedTask = await Task.WhenAny(taskAToB, taskBToA);
			cts.Cancel();

			if (completedTask == taskAToB) await taskBToA;
			else await taskAToB;

			observer?.NotifyEndOfJoin();


			static async Task pipeFlows(
				Memory<byte> buffer, IFlowJoinObserver? observer, bool direction,
				Func<IDataFlow, Memory<byte>, CancellationToken, ValueTask<int>> readDelegate,
				Func<IDataFlow, Memory<byte>, CancellationToken, ValueTask<int>> writeDelegate,
				IDataFlow transmitter, IDataFlow receiver, CancellationToken cancellation)
			{
				while (true)
				{
					int received;

					try
					{ received = await readDelegate(transmitter, buffer, cancellation); }
					catch (Exception) { break; }

					if (received == 0)
						break;

					var workBuffer = buffer[..received];

					if (observer is not null)
					{
						if (direction)
							observer.NotifyDataFromAToB(workBuffer);
						else observer.NotifyDataFromBToA(workBuffer);
					}

					try
					{ await writeDelegate(receiver, workBuffer, cancellation); }
					catch (Exception) { break; }
				}
			}
		}
	}

	extension(IReadOnlyDataFlow flow)
	{
		public TransportType Type => flow switch
		{
			IReadOnlyDatagramDataFlow => TransportType.DatagramBased,
			IReadOnlyStreamDataFlow => TransportType.StreamBased,
			_ => ThrowNotSupportedDataFlowType<TransportType>()
		};
	}

	private static T ThrowNotSupportedDataFlowType<T>() => throw new NotSupportedException("Only datagram and stream data flow types are supported");
}
