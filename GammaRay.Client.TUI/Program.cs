using GammaRay.Client.TUI;
using GammaRay.Core.API;
using GammaRay.Core.API.Client;
using GammaRay.Core.InternetAccess;
using GammaRay.Core.Monitoring;
using GammaRay.Core.Monitoring.Converters;
using GammaRay.Core.Routing;
using GammaRay.Core.Routing.Categorization;
using GammaRay.Core.Routing.NetworkProfiles;
using GammaRay.Core.Services;
using GammaRay.Core.Settings;
using GammaRay.Core.Utils.FileSystem;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;

var cancelWait = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) => { cancelWait.Cancel(); e.Cancel = true; };

AsyncContext.Run(async () =>
{
	try
	{
		Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
		Console.WriteLine("║ GammaRay TUI Client                                                          ║");
		Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
		Console.WriteLine();

		var apiClient = new GammaRayAPIClient();

		Console.Write("Enter server address (default: 127.0.0.3): ");
		var serverAddress = Console.ReadLine();
		if (string.IsNullOrWhiteSpace(serverAddress))
			serverAddress = "127.0.0.3";

		Console.WriteLine("\nConnecting to server...");
		await apiClient.ConnectAsync(serverAddress, 5000);
		Console.WriteLine("✓ Connected!");

		Console.WriteLine("Checking API version...");
		var version = await apiClient.RequestAPIVVersionAsync();
		Console.WriteLine($"✓ Server API version: {version}");

		Console.WriteLine("Loading server settings...");
		var settingsContent = await apiClient.RequestReadSettingsAsync();

		var serializerOptionsSource = LoadSettings(settingsContent);


		var tuiMonitoring = new ConnectionTrackingMonitoringSystem(redrawUI, TimeProvider.System);
		var apiMonitoringEventListener = new APIMonitoringEventListener(tuiMonitoring, serializerOptionsSource);

		apiClient.AddEventListener(apiMonitoringEventListener);

		redrawUI(tuiMonitoring);

		static void redrawUI(ConnectionTrackingMonitoringSystem tracking)
		{
			Console.Clear();
			Console.WriteLine("Online connections:");
			var orderedConnections = tracking.Connections.Values.OrderBy(s => s.RoutingResult, RoutingResultComparer.Instance);
			foreach (var connection in orderedConnections)
			{
				Console.Write($"[{connection.InboundDriver}:{connection.EndPoint}] -> [{connection.Destination}]");
				if (connection.RoutingResult is not null)
				{
					var color = (ConsoleColor)(connection.RoutingResult.GetHashCode() & 0xF);
					Console.ForegroundColor = color;
					Console.Write($" {connection.RoutingResult.Value.IAP.Name}/{connection.RoutingResult.Value.ChannelName}");
					Console.ResetColor();
				}

				if (connection.CurrentStatus is OnlineConnection.Status.Closed)
					Console.Write($" CLOSED");
				Console.WriteLine();
			}
		}

		await Task.Delay(-1, cancelWait.Token);

		Console.WriteLine("\nDisconnecting...");
		await apiClient.DisconnectAsync();
		Console.WriteLine("✓ Disconnected");
	}
	catch (Exception ex)
	{
		Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine($"\n✗ Error: {ex.Message}");
		if (ex.InnerException is not null)
			Console.WriteLine($"  Details: {ex.InnerException.Message}");
		Console.ResetColor();
		Console.ReadKey();
	}
});

static MonitoringSerializerOptionsSource LoadSettings(string settingsContent)
{
	using var settingsFile = new StringReader(settingsContent);

	var YAMLLoader = new YAMLConfigurationLoader();

	var networkProfileRawProvider = new YAMLNetworkProfileRawProvider();
	var endPointCategoryRawProvider = new YAMLEndPointCategoryRawProvider(new DummyLocator());
	var internetAccessPointRawProvider = new YAMLInternetAccessPointRawProvider();
	var capabilityClassRawProvider = new YAMLCapabilityClassRawProvider();
	var endPointRoutingConfigurationRawProvider = new YAMLEndPointRoutingConfigurationRawProvider();

	YAMLLoader.LoadSettings(settingsFile);

	networkProfileRawProvider.Initialize(YAMLLoader);
	endPointCategoryRawProvider.Initialize(YAMLLoader);
	capabilityClassRawProvider.Initialize(YAMLLoader);

	var networkProfileProvider = new NetworkProfileProvider(networkProfileRawProvider);
	var endPointCategoryProvider = new EndPointCategoriesProvider(endPointCategoryRawProvider);
	var capabilityClassProvider = new CapabilityClassProvider(capabilityClassRawProvider);

	internetAccessPointRawProvider.Initialize(YAMLLoader, networkProfileProvider);
	var internetAccessPointProvider = new InternetAccessPointProvider(internetAccessPointRawProvider, networkProfileProvider);

	endPointRoutingConfigurationRawProvider.Initialize(YAMLLoader, internetAccessPointProvider);
	var endPointRoutingConfigurationProvider = new EndPointRoutingConfigurationProvider(endPointRoutingConfigurationRawProvider);

	return new MonitoringSerializerOptionsSource(capabilityClassProvider, endPointCategoryProvider, endPointRoutingConfigurationProvider, internetAccessPointProvider, networkProfileProvider);
}


public class DummyLocator : IFileSystemLocator
{
	public bool Exists(string filePath) => true;

	public void Move(string originalFilePath, string newFilePath, bool overwrite = false) { }

	public Stream Open(string path, FileMode mode = FileMode.Open, FileAccess access = FileAccess.Read, FileShare share = FileShare.None)
	{
		return Stream.Null;
	}
}

public class RoutingResultComparer : IComparer<(InternetAccessPoint IAP, string ChannelName)?>
{
	public static readonly RoutingResultComparer Instance = new();


	public int Compare((InternetAccessPoint IAP, string ChannelName)? x, (InternetAccessPoint IAP, string ChannelName)? y)
	{
		if (x == y) // Including both null
			return 0;
		if (x is null)
			return -1;
		if (y is null)
			return 1;

		var IAPCompareResult = StringComparer.Ordinal.Compare(x.Value.IAP.Name, y.Value.IAP.Name);
		if (IAPCompareResult is not 0)
			return IAPCompareResult;

		return StringComparer.Ordinal.Compare(x.Value.ChannelName, y.Value.ChannelName);
	}
}
