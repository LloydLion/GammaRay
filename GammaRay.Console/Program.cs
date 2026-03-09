using GammaRay.Core;
using GammaRay.Core.Inbound;
using GammaRay.Core.InternetAccess;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Routing;
using GammaRay.Core.Routing.Categorization;
using GammaRay.Core.Routing.NetworkProfiles;
using GammaRay.Core.Services;
using GammaRay.Core.Settings;
using GammaRay.Core.Utils;
using GammaRay.Core.Utils.FileSystem;
using Microsoft.Extensions.Options;
using Serilog;
using System.Net;

internal class Program
{
	private static void Main(string[] args)
	{
		Log.Logger = new LoggerConfiguration()
			.WriteTo.Console()
			.CreateLogger();

		LoadSettings("settings.yaml");

		var httpInboundDriver = new HTTPInboundDriver(Options.Create(new HTTPInboundDriver.Options { }));
		var inbound = httpInboundDriver.CreateInbound(new IPEndPoint(new IPAddress([127, 0, 0, 3]), 2000));

		var channelRegistry = new ReflectionBasedDriverRegistry<IChannelDriver>([new LocalChannelDriver()]);

		var masterServer = new MasterServer([inbound], new DummyRouter(), channelRegistry);

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

	private class DummyRouter : IRouter
	{
		public IReadOnlyList<IAPChannel> MakeRoutingDecision(RequestContext context)
		{
			return [
				new IAPChannel("local", default)
			];
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
