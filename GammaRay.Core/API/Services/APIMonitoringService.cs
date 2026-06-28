using GammaRay.Core.API.Services.Proto;
using Grpc.Core;
using Channel = System.Threading.Channels.Channel;

namespace GammaRay.Core.API.Services;

public sealed class APIMonitoringService(APIBasedMonitoringProvider _monitoringSystem) : MonitoringService.MonitoringServiceBase
{
	public override async Task SubscribeEvents(Empty request, IServerStreamWriter<MonitoringEvent> responseStream, ServerCallContext context)
	{
		var channel = Channel.CreateUnbounded<MonitoringEvent>();

		using var subscription = _monitoringSystem.Subscribe((monitoringEvent) => channel.Writer.TryWrite(monitoringEvent));


		using (var enumerator = _monitoringSystem.GetPendingEvents().GetEnumerator())
		{
			if (enumerator.MoveNext())
				while (true)
				{
					var evt = enumerator.Current;
					var hasNext = enumerator.MoveNext();

					evt.Type = hasNext ? MonitoringEvent.Types.SyncronizationType.Buffered : MonitoringEvent.Types.SyncronizationType.LastBuffered;
					await responseStream.WriteAsync(evt);

					if (hasNext == false)
						break;
				}
		}

		try
		{
			await foreach (var evt in channel.Reader.ReadAllAsync(context.CancellationToken))
			{
				await responseStream.WriteAsync(evt);
			}
		}
		catch (OperationCanceledException) { }
	}
}
