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

var cancelWait = new TaskCompletionSource();
Console.CancelKeyPress += (s, e) => { cancelWait.SetResult(); e.Cancel = true; };

AsyncContext.Run(async () =>
{
	var apiClient = new GammaRayAPIClient(TimeProvider.System, Options.Create(new GammaRayAPIClient.Options()));
	var networkDriver = new NetworkAPIEndPointDriver();

	Console.ReadKey();

	await apiClient.ConnectAsync(networkDriver, "127.0.0.3:5000");

	var version = await apiClient.RequestAPIVVersionAsync();
	Console.WriteLine($"Server API version {version}");

	var settingsContent = await apiClient.RequestReadSettingsAsync();
	Console.WriteLine(settingsContent);

	var serializerOptionsSource = LoadSettings(settingsContent);
	var consoleMonitoring = new ConsoleMonitoringSystem();
	var apiMonitoringEventListener = new APIMonitoringEventListener(consoleMonitoring, serializerOptionsSource);

	apiClient.AddEventListener(apiMonitoringEventListener);

	await apiClient.ControlMonitoringAsync(APIConstants.MonitoringMode.EnabledWithReportProperties);

	await cancelWait.Task;
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
