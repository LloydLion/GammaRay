using GammaRay.Core;
using GammaRay.Core.Channels;
using GammaRay.Core.Inbound;
using GammaRay.Core.Network;
using GammaRay.Core.Routing;
using GammaRay.Core.Utils;
using Microsoft.Extensions.Options;
using Serilog;

internal class Program
{
	private static void Main(string[] args)
	{
		Log.Logger = new LoggerConfiguration()
			.WriteTo.Console()
			.CreateLogger();

		var httpInboundDriver = new HTTPInboundDriver(Options.Create(new HTTPInboundDriver.Options { }));
		var inbound = httpInboundDriver.CreateInbound(new GenericWebEndPoint(new WebHost("127.0.0.3"), 2000));

		var channelRegistry = new ReflectionBasedDriverRegistry<IChannelDriver>([new LocalChannelDriver()]);

		var masterServer = new MasterServer([inbound], new DummyRouter(), channelRegistry);

		masterServer.Run();
	}

	private class DummyRouter : IRouter
	{
		public IReadOnlyList<IAPChannel> MakeRoutingDecision(RequestContext context)
		{
			return [
				new IAPChannel("local", context.TargetEndPoint.GenericEndPoint)
			];
		}
	}
}
