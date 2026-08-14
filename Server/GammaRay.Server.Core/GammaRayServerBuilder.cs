using GammaRay.Core.API;
using GammaRay.Core.API.Services;
using GammaRay.Core.Connection;
using GammaRay.Core.Connection.Inbound;
using GammaRay.Core.Host;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.InternetAccess.Channels.Drivers;
using GammaRay.Core.InternetAccess.Channels.Testing;
using GammaRay.Core.Monitoring;
using GammaRay.Core.Monitoring.Converters;
using GammaRay.Core.Network.Identity;
using GammaRay.Core.Network.Profiles;
using GammaRay.Core.Persistence;
using GammaRay.Core.Routing;
using GammaRay.Core.Services;
using GammaRay.Core.Services.Probing;
using GammaRay.Core.Services.Probing.Drivers;
using GammaRay.Core.Utils;
using GammaRay.Core.Utils.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GammaRay.Server.Core;

public sealed class GammaRayServerBuilder
{
	private bool _addConsoleMonitoring = false;

	
	private GammaRayServerBuilder(IServiceCollection services, IFileSystemLocator fileSystemLocator, bool addConsoleMonitoring)
	{
		_addConsoleMonitoring = addConsoleMonitoring;
		Services = services;
		FileSystem = fileSystemLocator;
	}

	
	public IServiceCollection Services { get; }

	public IFileSystemLocator FileSystem { get; }


	public GammaRayServerBuilder Configure(Action<GammaRayServerBuilder> configure)
	{
		configure(this);
		return this;
	}

	public GammaRayServerBuilder ControlConsoleMonitoring(bool? enable)
	{
		if (enable is not null)
			_addConsoleMonitoring = enable.Value;
		return this;
	}
		
	public async ValueTask BuildAndRunAsync(CancellationToken cancel)
	{
		if (_addConsoleMonitoring)
			Services.AddSingleton<IMonitoringProvider, ConsoleMonitoringProvider>();

		await using var sp = Services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
		
		((NetworkIdentifierBase)sp.GetRequiredService<INetworkIdentifier>()).Initialize();

		((DbServiceRepository)sp.GetRequiredService<IServiceRepository>()).Initialize();
		((DbServiceStatusTableRepository)sp.GetRequiredService<IServiceStatusTableRepository>()).Initialize();
		((DbIAPChannelObservedDataRepository)sp.GetRequiredService<IIAPChannelObservedDataRepository>()).Initialize();
		((DbNetworkProfileMappingRepository)sp.GetRequiredService<INetworkProfileMappingRepository>()).Initialize();

		((DefaultIAPChannelMonitor)sp.GetRequiredService<IIAPChannelMonitor>()).StartMonitoring();

		var inboundDriverRegistry = sp.GetRequiredService<IDriverRegistry<IInboundDriver>>();
		var inbounds = sp.GetRequiredService<InboundConfigurationProvider>()
			.InboundConfigurations
			.Select(kv =>
			{
				var config = kv.Value;
				var driver = inboundDriverRegistry.ProvideDriver(config.Protocol);
				var inbound = driver.CreateInbound(config.EndPoint);
				return new NamedInbound(inbound, driver, kv.Key, config.Protocol);
			})
			.ToArray();

		var masterServer = sp.GetRequiredService<IMasterServer>();
		var masterServerTask = masterServer.Run(inbounds, cancel);

		var apiServer = sp.GetRequiredService<APIServer>();
		var apiServerTask = apiServer.Run(cancel);


		await Task.WhenAll(masterServerTask, apiServerTask);
	}

	public static GammaRayServerBuilder Create(ApplicationControl applicationControl)
	{
		var services = new ServiceCollection();
		var fileSystemLocator = new LocalFileSystemLocator(Options.Create(new LocalFileSystemLocator.Options()));

		var sp = services
			.AddSingleton(applicationControl)

			.AddSingleton(TimeProvider.System)
			.AddSingleton<IFileSystemLocator>(fileSystemLocator)

			.Configure<SQLiteConnectionFactory.Options>(options =>
			{
				options.ConnectionString = "Data Source=data.db";
			})
			.AddSingleton<IDbConnectionFactory, SQLiteConnectionFactory>()
			.Configure<DbServiceRepository.Options>(_ => { })
			.AddSingleton<IServiceRepository, DbServiceRepository>()
			.Configure<DbServiceStatusTableRepository.Options>(_ => { })
			.AddSingleton<IServiceStatusTableRepository, DbServiceStatusTableRepository>()
			.AddSingleton<IIAPChannelObservedDataRepository, DbIAPChannelObservedDataRepository>()
			.AddSingleton<INetworkProfileMappingRepository, DbNetworkProfileMappingRepository>()

			.Configure<HTTPInboundDriver.Options>(_ => { })
			.AddSingleton<IInboundDriver, HTTPInboundDriver>()
			.AddSingleton<IInboundDriver, SOCKS5InboundDriver>()
			.AddSingleton<IDriverRegistry<IInboundDriver>, ReflectionBasedDriverRegistry<IInboundDriver>>()

			.AddSingleton<IChannelDriver, LocalChannelDriver>()
			.AddSingleton<IChannelDriver, SOCKS5ChannelDriver>()
			.AddSingleton<IDriverRegistry<IChannelDriver>, ReflectionBasedDriverRegistry<IChannelDriver>>()

			.AddSingleton<IProbeDriver, HTTPProbingDriver>()
			.AddSingleton<IProbeDriver, MTProtoProbingDriver>()
			.AddSingleton<IDriverRegistry<IProbeDriver>, ReflectionBasedDriverRegistry<IProbeDriver>>()

#if UseWindowsSpecificComponents
			.AddSingleton<GammaRay.Core.OSSpecific.Windows.Management.PowerShellHost>()
			.AddSingleton<INetworkIdentifier, GammaRay.Core.OSSpecific.Windows.Network.Identity.WindowsNetProfileBasedNetworkIdentifier>()
#else
			.AddSingleton<INetworkIdentifier, InterfaceBasedNetworkIdentifier>()
#endif

			.AddSingleton<ICapabilityDetector, DefaultCapabilityDetector>()
			.Configure<ProbingManager.Options>(_ => { })
			.AddSingleton<IProbingManager, ProbingManager>()

			.AddSingleton<IIAPChannelPicker, StatusBasedChannelPicker>()
			.Configure<DefaultIAPChannelMonitor.Options>(_ => { })
			.AddSingleton<IIAPChannelMonitor, DefaultIAPChannelMonitor>()
			.AddSingleton<IIAPChannelSimpleTester, IAPChannelSimpleTester>()

			.AddSingleton<IRouter, SmartRouter>()
			.AddSingleton<IMasterServer, MasterServer>()

			.AddSingleton<IMonitoringProvider>(sp => sp.GetRequiredService<APIBasedMonitoringProvider>())
			.AddSingleton<MonitoringSystem>()
			.AddSingleton(sp => sp.GetRequiredService<MonitoringSystem>().Context)
			.AddSingleton<MonitoringSerializerOptionsSource>()


			.AddSingleton<APIBasedMonitoringProvider>()
			.AddSingleton<APIBasicService>()
			.AddSingleton<APIChannelsService>()
			.AddSingleton<APIControlService>()
			.AddSingleton<APIMonitoringService>()
			.AddSingleton<APIServicesService>()
			.AddSingleton<APIFileSystemService>()
			.AddSingleton<APINetworkService>()
			.AddSingleton<APIServer>();
		
		
		bool addConsoleMonitoring = false;
#if EnableConsoleMonitoringByDefault
		addConsoleMonitoring = true;
#endif
			
		return new GammaRayServerBuilder(services, fileSystemLocator, addConsoleMonitoring);
	}
}
