using GammaRay.Core;
using GammaRay.Core.Inbound;
using GammaRay.Core.InternetAccess;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.InternetAccess.Channels.Drivers;
using GammaRay.Core.InternetAccess.Channels.Testing;
using GammaRay.Core.Network.Identity;
using GammaRay.Core.Persistence;
using GammaRay.Core.Routing;
using GammaRay.Core.Routing.Categorization;
using GammaRay.Core.Routing.NetworkProfiles;
using GammaRay.Core.Services;
using GammaRay.Core.Services.Probing;
using GammaRay.Core.Services.Probing.Drivers;
using GammaRay.Core.Settings;
using GammaRay.Core.Utils;
using GammaRay.Core.Utils.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Nito.AsyncEx;
using Serilog;

internal class Program
{
	private static void Main(string[] args)
	{
		AsyncContext.Run(async () =>
		{
			Log.Logger = new LoggerConfiguration()
			.WriteTo.Console()
			.CreateLogger();

			var services = new ServiceCollection();

			LoadSettings("settings.yaml", services);

			var sp = services
				.AddSingleton(TimeProvider.System)

				.Configure<SQLiteConnectionFactory.Options>(options => { options.ConnectionString = "Data Source=data.db"; })
				.AddSingleton<IDbConnectionFactory, SQLiteConnectionFactory>()
				.Configure<DbServiceRepository.Options>(s => { })
				.AddSingleton<IServiceRepository, DbServiceRepository>()
				.Configure<DbServiceStatusTableRepository.Options>(s => { })
				.AddSingleton<IServiceStatusTableRepository, DbServiceStatusTableRepository>()
				.AddSingleton<IIAPChannelStatusRepository, DbIAPChannelStatusRepository>()

				.Configure<HTTPInboundDriver.Options>(s => { })
				.AddSingleton<IInboundDriver, HTTPInboundDriver>()
				.AddSingleton<IInboundDriver, SOCKS5InboundDriver>()
				.AddSingleton<IDriverRegistry<IInboundDriver>, ReflectionBasedDriverRegistry<IInboundDriver>>()

				.AddSingleton<IChannelDriver, LocalChannelDriver>()
				.AddSingleton<IChannelDriver, SOCKS5ChannelDriver>()
				.AddSingleton<IDriverRegistry<IChannelDriver>, ReflectionBasedDriverRegistry<IChannelDriver>>()

				.AddSingleton<IProbeDriver, HTTPProbingDriver>()
				.AddSingleton<IDriverRegistry<IProbeDriver>, ReflectionBasedDriverRegistry<IProbeDriver>>()

#if UseWindowsComponents
			.AddSingleton<INetworkIdentifier, GammaRay.Core.Windows.Management.PowerShellHost>()
			.AddSingleton<INetworkIdentifier, GammaRay.Core.Windows.Network.Identity.WindowsNetProfileBasedNetworkIdentifier>()
#else
				.AddSingleton<INetworkIdentifier, InterfaceBasedNetworkIdentifier>()
#endif
				.AddSingleton<INetworkProfileMappingRepository, DummyNetProfileMapping>()

				.AddSingleton<ICapabilityDetector, DefaultCapabilityDetector>()
				.Configure<ProbingManager.Options>(s => { })
				.AddSingleton<IProbingManager, ProbingManager>()

				.AddSingleton<IIAPChannelPicker, StatusBasedChannelPicker>()
				.Configure<DefaultIAPChannelMonitor.Options>(s => { })
				.AddSingleton<IIAPChannelMonitor, DefaultIAPChannelMonitor>()
				.AddSingleton<IIAPChannelSimpleTester, IAPChannelSimpleTester>()

				.AddSingleton<SmartRouter>()

				.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

			((DbServiceRepository)sp.GetRequiredService<IServiceRepository>()).Initialize();
			((DbServiceStatusTableRepository)sp.GetRequiredService<IServiceStatusTableRepository>()).Initialize();
			((DbIAPChannelStatusRepository)sp.GetRequiredService<IIAPChannelStatusRepository>()).Initialize();

			((DefaultIAPChannelMonitor)sp.GetRequiredService<IIAPChannelMonitor>()).StartMonitoring();

			var inbounds = sp.GetRequiredService<InboundConfigurationProvider>()
				.PlainInboundConfigurations
				.Select(c => sp.GetRequiredService<IDriverRegistry<IInboundDriver>>().CreateInboundFromConfiguration(c))
				.ToArray();

			var masterServer = new MasterServer(inbounds, sp.GetRequiredService<SmartRouter>(), sp.GetRequiredService<IDriverRegistry<IChannelDriver>>());

			await masterServer.Run();
		});
	}

	private static void LoadSettings(string path, IServiceCollection services)
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
		services.AddSingleton(inboundProvider);
		var networkProfileProvider = new NetworkProfileProvider(networkProfileRawProvider);
		services.AddSingleton(networkProfileProvider);
		var endPointCategoryProvider = new EndPointCategoriesProvider(endPointCategoryRawProvider);
		services.AddSingleton(endPointCategoryProvider);
		var capabilityClassProvider = new CapabilityClassProvider(capabilityClassRawProvider);
		services.AddSingleton(capabilityClassProvider);

		internetAccessPointRawProvider.Initialize(YAMLLoader, networkProfileProvider);
		var internetAccessPointProvider = new InternetAccessPointProvider(internetAccessPointRawProvider, networkProfileProvider);
		services.AddSingleton(internetAccessPointProvider);

		endPointRoutingConfigurationRawProvider.Initialize(YAMLLoader, internetAccessPointProvider);
		var endPointRoutingConfigurationProvider = new EndPointRoutingConfigurationProvider(endPointRoutingConfigurationRawProvider);
		services.AddSingleton(endPointRoutingConfigurationProvider);

		routingGridRawProvider.Initialize(YAMLLoader, networkProfileProvider, endPointCategoryProvider, endPointRoutingConfigurationProvider);
		var routingGridProvider = new RoutingGridProvider(routingGridRawProvider);
		services.AddSingleton(routingGridProvider);
	}

	private class DummyRouter(IAPChannel _channel) : IRouter
	{
		public IAPChannel MakeRoutingDecision(RequestContext context) => _channel;
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

		public void UpdateStatuses(IEnumerable<IAPChannelStatus> statusTable)
		{
			
		}
	}

	public class DummyNetProfileMapping(NetworkProfileProvider _networkProfileProvider) : INetworkProfileMappingRepository
	{
		public NetworkProfile GetProfileFor(NetworkIdentity identity) => _networkProfileProvider.DefaultProfile;
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
