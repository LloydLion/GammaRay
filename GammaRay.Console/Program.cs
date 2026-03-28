using GammaRay.Core.Inbound;
using GammaRay.Core.InternetAccess;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Network;
using GammaRay.Core.Network.Identity;
using GammaRay.Core.Routing;
using GammaRay.Core.Routing.Categorization;
using GammaRay.Core.Routing.NetworkProfiles;
using GammaRay.Core.Services;
using GammaRay.Core.Services.Probing;
using GammaRay.Core.Services.Probing.Drivers;
using GammaRay.Core.Settings;
using GammaRay.Core.Utils;
using GammaRay.Core.Utils.FileSystem;
using Microsoft.Extensions.Options;
using Serilog;

internal class Program
{
	private async static Task Main(string[] args)
	{
		Log.Logger = new LoggerConfiguration()
			.WriteTo.Console()
			.CreateLogger();

		//LoadSettings("settings.yaml");

		Console.Write("Enter end point: ");
		var endPoint = WebEndPoint.Parse(Console.ReadLine()!, 443, TransportType.StreamBased);

		var channelRegistry = new ReflectionBasedDriverRegistry<IChannelDriver>([new LocalChannelDriver()]);
		var probeDriverRegistry = new ReflectionBasedDriverRegistry<IProbeDriver>([new HTTPProbingDriver()]);
		var netId = new InterfaceBasedNetworkIdentifier();

		var multiProber = new ProbingManager(
			probeDriverRegistry,
			new DummyStatusRepository(),
			channelRegistry,
			netId,
			new DummyNetProfileMapping(),
			Options.Create(new ProbingManager.Options())
		);

		var IAP = new InternetAccessPoint("local-main") { Channels = new Dictionary<string, IAPChannel> { { "main", new IAPChannel("local", default) } } };

		var capabilityProbingMethod = new CapabilityProbingMethod("HTTP", new Dictionary<string, CapabilityLinkedValue>()
			{ { "useTLS", CapabilityLinkedValue.Property("UseTLS") } });
		var capabilityClass = new CapabilityClass([], capabilityProbingMethod);
		var capability = new Capability(capabilityClass, new Dictionary<string, string>()
			{ { "UseTLS", "true" } });
		var service = new Service(endPoint, capability);

		var output = new DummyServiceStatusTableOutput();

		multiProber.StartProbing(service, [IAP], output);

		var table = await output.AwaitForUpdate();
		var status = table.Table[IAP];

		if (status.IsUnavailable)
			Console.WriteLine("Unavailable");
		else Console.WriteLine(status.AverageProbeTime.TotalMilliseconds);
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

	private class DummyRouter : IRouter
	{
		public IReadOnlyList<IAPChannel> MakeRoutingDecision(RequestContext context)
		{
			return [
				new IAPChannel("local", default)
			];
		}
	}

	private class DummyStatusRepository : IIAPChannelStatusRepository
	{
		public IAPChannelStatus GetStatus(InternetAccessPoint point, IAPChannel channel, NetworkProfile currentNetworkProfile)
		{
			return new IAPChannelStatus(point, channel, currentNetworkProfile, TimeSpan.FromMilliseconds(15));
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
