using GammaRay.Core;
using GammaRay.Core.API;
using GammaRay.Core.Host;
using GammaRay.Core.Inbound;
using GammaRay.Core.InternetAccess;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.InternetAccess.Channels.Drivers;
using GammaRay.Core.InternetAccess.Channels.Testing;
using GammaRay.Core.Monitoring;
using GammaRay.Core.Monitoring.Converters;
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
using Microsoft.Extensions.Options;

internal class Program
{
	private static void Main(string[] args)
	{
		var applicationControl = new ApplicationControl(async (applicationControl, cancel) =>
		{
			var services = new ServiceCollection();

			LoadSettings(services);

			await using var sp = services
				.AddSingleton(applicationControl)

				.AddSingleton(TimeProvider.System)

				.Configure<SQLiteConnectionFactory.Options>(options => { options.ConnectionString = "Data Source=data.db"; })
				.AddSingleton<IDbConnectionFactory, SQLiteConnectionFactory>()
				.Configure<DbServiceRepository.Options>(s => { })
				.AddSingleton<IServiceRepository, DbServiceRepository>()
				.Configure<DbServiceStatusTableRepository.Options>(s => { })
				.AddSingleton<IServiceStatusTableRepository, DbServiceStatusTableRepository>()
				.AddSingleton<IIAPChannelObservedDataRepository, DbIAPChannelObservedDataRepository>()

				.Configure<HTTPInboundDriver.Options>(s => { })
				.AddSingleton<IInboundDriver, HTTPInboundDriver>()
				.AddSingleton<IInboundDriver, SOCKS5InboundDriver>()
				.AddSingleton<IDriverRegistry<IInboundDriver>, ReflectionBasedDriverRegistry<IInboundDriver>>()

				.AddSingleton<IChannelDriver, LocalChannelDriver>()
				.AddSingleton<IChannelDriver, SOCKS5ChannelDriver>()
				.AddSingleton<IDriverRegistry<IChannelDriver>, ReflectionBasedDriverRegistry<IChannelDriver>>()

				.AddSingleton<IProbeDriver, HTTPProbingDriver>()
				.AddSingleton<IProbeDriver, MTProtoProbingDriver>()
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

				.AddSingleton<ConsoleMonitoringSystem>()
				.AddSingleton<APIBasedMonitoringSystem>()
				.AddSingleton<IMonitoringSystem, MultiHeadMonitoringSystem>(sp => new([
					sp.GetRequiredService<ConsoleMonitoringSystem>(),
					sp.GetRequiredService<APIBasedMonitoringSystem>()
				]))
				.AddSingleton<MonitoringEventBuffer>()
				.AddSingleton<MonitoringSerializerOptionsSource>()
				
				.AddSingleton<IAPIEndPointDriver, NetworkAPIEndPointDriver>()
				.AddSingleton<IDriverRegistry<IAPIEndPointDriver>, ReflectionBasedDriverRegistry<IAPIEndPointDriver>>()
				.AddSingleton<APIServer>()

				.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

			sp.GetRequiredService<APIBasedMonitoringSystem>().Initialize();

			((NetworkIdentifierBase)sp.GetRequiredService<INetworkIdentifier>()).Initialize();

			((DbServiceRepository)sp.GetRequiredService<IServiceRepository>()).Initialize();
			((DbServiceStatusTableRepository)sp.GetRequiredService<IServiceStatusTableRepository>()).Initialize();
			((DbIAPChannelObservedDataRepository)sp.GetRequiredService<IIAPChannelObservedDataRepository>()).Initialize();

			((DefaultIAPChannelMonitor)sp.GetRequiredService<IIAPChannelMonitor>()).StartMonitoring();

			var inbounds = sp.GetRequiredService<InboundConfigurationProvider>()
				.PlainInboundConfigurations
				.Select(c => sp.GetRequiredService<IDriverRegistry<IInboundDriver>>().CreateInboundFromConfiguration(c))
				.ToArray();

			var masterServer = new MasterServer(inbounds, sp.GetRequiredService<SmartRouter>(), sp.GetRequiredService<IDriverRegistry<IChannelDriver>>());
			var masterServerTask = masterServer.Run(cancel);

			var apiServer = sp.GetRequiredService<APIServer>();
			var apiServerTask = apiServer.Run(cancel);


			await Task.WhenAll([masterServerTask, apiServerTask]);
		});

		applicationControl.MainLoop();
	}

	private static void LoadSettings(IServiceCollection services)
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

		var fileSystemLocator = new Locator();

		var settingsFileHolder = new SettingsFileHolder(Options.Create(new SettingsFileHolder.Options()), fileSystemLocator);

		bool readBackupSettingsFile = false;

	retryLoadSettings:
		try
		{
			using var settingsFile = settingsFileHolder.ReadConfigurationFile(readBackupSettingsFile);

			var YAMLLoader = new YAMLConfigurationLoader();

			var inboundRawProvider = new YAMLInboundRawProvider();
			var networkProfileRawProvider = new YAMLNetworkProfileRawProvider();
			var endPointCategoryRawProvider = new YAMLEndPointCategoryRawProvider(fileSystemLocator);
			var internetAccessPointRawProvider = new YAMLInternetAccessPointRawProvider();
			var capabilityClassRawProvider = new YAMLCapabilityClassRawProvider();
			var endPointRoutingConfigurationRawProvider = new YAMLEndPointRoutingConfigurationRawProvider();
			var apiConfigurationRawProvider = new YAMLAPIConfigurationRawProvider();

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

			apiConfigurationRawProvider.Initialize(YAMLLoader);
			var apiConfigurationProvider = new APIConfigurationProvider(apiConfigurationRawProvider);
			services.AddSingleton(apiConfigurationProvider);

			services.AddSingleton(settingsFileHolder);
		}
		catch (Exception ex)
		{
			if (readBackupSettingsFile)
				throw;
			else
			{
				Console.WriteLine(ex);
				readBackupSettingsFile = true;
				goto retryLoadSettings;
			}
		}
	}


	public class DummyNetProfileMapping(NetworkProfileProvider _networkProfileProvider) : INetworkProfileMappingRepository
	{
		public NetworkProfile GetProfileFor(NetworkIdentity identity) => _networkProfileProvider.DefaultProfile;
	}

	private class Locator : IFileSystemLocator
	{
		public bool Exists(string filePath)
		{
			return File.Exists(filePath);
		}

		public void Move(string originalFilePath, string newFilePath, bool overwrite = false)
		{
			File.Move(originalFilePath, newFilePath, overwrite);
		}

		public Stream Open(string path, FileMode mode = FileMode.Open, FileAccess access = FileAccess.Read, FileShare share = FileShare.None)
		{
			return File.Open(path, mode, access, share);
		}
	}
}
