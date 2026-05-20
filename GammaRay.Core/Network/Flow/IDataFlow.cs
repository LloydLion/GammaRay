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
	extension(IDataFlow flow)
	{
		public Task JoinAsync(IDataFlow other) => flow.JoinAsync(other, new byte[ushort.MaxValue], new byte[ushort.MaxValue]);

		public async Task JoinAsync(IDataFlow other, Memory<byte> buffer1, Memory<byte> buffer2)
		{
			if (buffer1.Length is not ushort.MaxValue || buffer2.Length is not ushort.MaxValue)
				throw new ArgumentException($"Length of each memory must be {ushort.MaxValue}(aka ushort.MaxValue, maximum size of IP packet)");

			Func<IDataFlow, Memory<byte>, CancellationToken, ValueTask<int>> readDelegate, writeDelegate;


			switch ((other, flow))
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

			var task1to2 = pipeFlows(buffer1, readDelegate, writeDelegate, flow, other, cts.Token);
			var task2to1 = pipeFlows(buffer2, readDelegate, writeDelegate, other, flow, cts.Token);

			var completedTask = await Task.WhenAny(task1to2, task2to1);
			cts.Cancel();

			if (completedTask == task1to2) await task2to1;
			else await task1to2;


			static async Task pipeFlows(
				Memory<byte> buffer,
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

					try
					{ await writeDelegate(receiver, buffer[..received], cancellation); }
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
