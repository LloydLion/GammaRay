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
		public async Task Run()
		{
			foreach (var inbound in _inbounds)
				inbound.OnNewRequest(RequestCallback);

			var cts = new CancellationTokenSource();
			var tasks = new HashSet<Task>();

			foreach (var inbound in _inbounds)
				tasks.Add(inbound.Run(cts.Token));

			await Task.WhenAll(tasks);
		}

		private async ValueTask RequestCallback(IInbound sender, RequestContext context)
		{
			IAPChannel channel = _router.MakeRoutingDecision(context);

			await using IOpenChannel? openChannel =
				await _channelDriverRegistry
					.ProvideDriver(channel.DriverName)
					.TryOpenChannelAsync(channel, context.TargetEndPoint) ?? throw new Exception("Failed to open channel");

			IDataFlow correspondingFlow = openChannel.GetFlow();
			IDataFlow incomingFlow = context.IncomingDataFlow;

			await incomingFlow.JoinAsync(correspondingFlow);
		}
	}
}
