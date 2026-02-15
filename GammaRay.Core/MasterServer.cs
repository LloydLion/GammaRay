using GammaRay.Core.Inbound;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Network;
using GammaRay.Core.Network.Flow;
using GammaRay.Core.Routing;
using Nito.AsyncEx;

namespace GammaRay.Core
{
	public sealed class MasterServer(
		IInbound[] _inbounds,
		IRouter _router,
		IChannelDriverRegistry _channelDriverRegistry
	)
	{
		public void Run()
		{
			AsyncContext.Run(async () =>
			{
				foreach (var inbound in _inbounds)
					inbound.OnNewRequest(RequestCallback);

				var cts = new CancellationTokenSource();
				var tasks = new HashSet<Task>();

				foreach (var inbound in _inbounds)
					tasks.Add(inbound.Run(cts.Token));

				await Task.WhenAll(tasks);
			});
		}

		private async ValueTask RequestCallback(IInbound sender, RequestContext context)
		{
			IReadOnlyList<IAPChannel> channelsQueue = _router.MakeRoutingDecision(context);

			TransportType requestedTransportType = context.TargetEndPoint.Protocol;

			await using IOpenChannel openChannel = await OpenChannelAsync(channelsQueue, requestedTransportType);

			IDataFlow correspondingFlow = openChannel.GetFlow();
			IDataFlow incomingFlow = context.IncomingDataFlow;

			await incomingFlow.JoinAsync(correspondingFlow);
		}

		private async ValueTask<IOpenChannel> OpenChannelAsync(IReadOnlyList<IAPChannel> channelsQueue, TransportType requestedTransportType)
		{
			IOpenChannel? openChannel = null;
			foreach (var channel in channelsQueue)
			{
				IChannelDriver driver = _channelDriverRegistry.ProvideDriver(channel.DriverName);
				openChannel = await driver.TryOpenChannelAsync(channel, requestedTransportType);
				if (openChannel is not null)
					break;
			}
			if (openChannel is null)
				throw new Exception("Connection failed");
			return openChannel;
		}
	}
}
