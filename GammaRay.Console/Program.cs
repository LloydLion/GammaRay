using GammaRay.Core;
using GammaRay.Core.Inbound;
using GammaRay.Core.InternetAccess;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.InternetAccess.Channels.Drivers;
using GammaRay.Core.Network;
using GammaRay.Core.Network.Identity;
using GammaRay.Core.Routing;
using GammaRay.Core.Routing.Categorization;
using GammaRay.Core.Routing.NetworkProfiles;
using GammaRay.Core.Services;
using GammaRay.Core.Services.Probing;
using GammaRay.Core.Settings;
using GammaRay.Core.Utils;
using GammaRay.Core.Utils.FileSystem;
using Microsoft.Extensions.Options;
using Serilog;
using System.Net;

internal class Program
{
	private async static Task Main(string[] args)
	{
		Log.Logger = new LoggerConfiguration()
			.WriteTo.Console()
			.CreateLogger();

		LoadSettings("settings.yaml");

		var httpInboundDriver = new HTTPInboundDriver(Options.Create(new HTTPInboundDriver.Options { }));
		var httpInbound = httpInboundDriver.CreateInbound(new IPEndPoint(new IPAddress([127, 0, 0, 3]), 2000));

		var socksInboundDriver = new SOCKS5InboundDriver();
		var socksInbound = socksInboundDriver.CreateInbound(new IPEndPoint(new IPAddress([127, 0, 0, 3]), 2001));


		var channelRegistry = new ReflectionBasedDriverRegistry<IChannelDriver>([new LocalChannelDriver(), new SOCKS5ChannelDriver()]);

		var channel = new IAPChannel("socks", new GenericWebEndPoint(new WebHost("127.0.0.2"), 2011));

		var masterServer = new MasterServer([httpInbound, socksInbound], new DummyRouter(channel), channelRegistry);

		masterServer.Run();
	}

	private static void LoadSettings(string path)
	{
		// Entities:
		// - InboundConfiguration
		// - NetworkProfile
		// - EndPointCategory
		// - InternetAccessPoint
		// - CapabilityClass
		// - EndPointRoutingConfiguration
		//
		// Additionally: RoutingGrid

		using var settingsFile = File.OpenText(path);

		var fileSystemLocator = new Locator();
		var YAMLLoader = new YAMLConfigurationLoader();

		var inboundRawProvider = new YAMLInboundRawProvider();
		var networkProfileRawProvider = new YAMLNetworkProfileRawProvider();
		var endPointCategoryRawProvider = new YAMLEndPointCategoryRawProvider(fileSystemLocator);
		var internetAccessPointRawProvider = new YAMLInternetAccessPointRawProvider();
		var capabilityClassRawProvider = new YAMLCapabilityClassRawProvider();
		var endPointRoutingConfigurationRawProvider = new YAMLEndPointRoutingConfigurationRawProvider();

		var routingGridRawProvider = new YAMLRoutingGridRawProvider();

		YAMLLoader.LoadSettings(settingsFile);


		inboundRawProvider.Initialize(YAMLLoader);
		networkProfileRawProvider.Initialize(YAMLLoader);
		endPointCategoryRawProvider.Initialize(YAMLLoader);
		capabilityClassRawProvider.Initialize(YAMLLoader);

		var inboundProvider = new InboundConfigurationProvider(inboundRawProvider);
		var networkProfileProvider = new NetworkProfileProvider(networkProfileRawProvider);
		var endPointCategoryProvider = new EndPointCategoriesProvider(endPointCategoryRawProvider);
		var capabilityClassProvider = new CapabilityClassProvider(capabilityClassRawProvider);

		internetAccessPointRawProvider.Initialize(YAMLLoader, networkProfileProvider);
		var internetAccessPointProvider = new InternetAccessPointProvider(internetAccessPointRawProvider, networkProfileProvider);

		endPointRoutingConfigurationRawProvider.Initialize(YAMLLoader, internetAccessPointProvider);
		var endPointRoutingConfigurationProvider = new EndPointRoutingConfigurationProvider(endPointRoutingConfigurationRawProvider);

		routingGridRawProvider.Initialize(YAMLLoader, networkProfileProvider, endPointCategoryProvider, endPointRoutingConfigurationProvider);
		var routingGridProvider = new RoutingGridProvider(routingGridRawProvider);
	}

	private class DummyRouter(IAPChannel _channel) : IRouter
	{
		public IReadOnlyList<IAPChannel> MakeRoutingDecision(RequestContext context) => [_channel];
	}

	private class DummyStatusRepository : IIAPChannelStatusRepository
	{
		public DateTime GetLastStatusUpdateTime(NetworkProfile networkProfile)
		{
			return DateTime.UtcNow.AddDays(-1);
		}

		public IAPChannelStatus TryGetStatus(InternetAccessPoint point, IAPChannel channel, NetworkProfile currentNetworkProfile)
		{
			return new IAPChannelStatus(point, channel, currentNetworkProfile, TimeSpan.FromMilliseconds(15));
		}

		public ValueTask UpdateStatusesAsync(IEnumerable<IAPChannelStatus> statusTable)
		{
			return ValueTask.CompletedTask;
		}
	}

	public class DummyNetProfileMapping : INetworkProfileMappingRepository
	{
		private readonly static NetworkProfile MainProfile = new("main");

		public NetworkProfile GetProfileFor(NetworkIdentity identity) => MainProfile;
	}

	public class DummyServiceStatusTableOutput : IServiceStatusTableRepository
	{
		private readonly TaskCompletionSource<ServiceStatusTable> _e = new(false);

		public Decayable<ServiceStatusTable>? TryGetTable(Service service)
		{
			return null;
		}

		public void UpdateTable(ServiceStatusTable route)
		{
			_e.SetResult(route);	
		}

		public Task<ServiceStatusTable> AwaitForUpdate()
		{
			return _e.Task;
		}
	}

	private class Locator : IFileSystemLocator
	{
		public Stream OpenFile(string path)
		{
			return File.Open(path, FileMode.Open);
		}
	}
}
