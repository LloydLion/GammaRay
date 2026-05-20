using GammaRay.Core.Inbound;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Network.Flow;
using GammaRay.Core.Routing;

namespace GammaRay.Core
{
	public sealed class MasterServer(
		IInbound[] _inbounds,
		IRouter _router,
		IChannelDriverRegistry _channelDriverRegistry
	)
	{
		public async Task Run(CancellationToken stopToken)
		{
			foreach (var inbound in _inbounds)
				inbound.OnNewRequest(RequestCallback);

			var tasks = new HashSet<Task>();

			foreach (var inbound in _inbounds)
				tasks.Add(inbound.Run(stopToken));

			await Task.WhenAll(tasks);
		}

		private async ValueTask RequestCallback(IInbound sender, RequestContext context)
		{
			IAPChannel channel = _router.MakeRoutingDecision(context);

			await using var openingResult =
				await _channelDriverRegistry
					.ProvideDriver(channel.DriverName)
					.TryOpenChannelAsync(channel, context.TargetEndPoint);

			IDataFlow correspondingFlow = openingResult.OpenChannel.GetFlow();
			IDataFlow incomingFlow = context.IncomingDataFlow;

			await incomingFlow.JoinAsync(correspondingFlow);
		}
	}
}
