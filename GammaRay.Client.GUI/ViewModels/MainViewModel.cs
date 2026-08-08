using Avalonia.Controls;
using DynamicData;
using GammaRay.Client.GUI.Views;
using GammaRay.Core.API.Client;
using GammaRay.Core.API.Services.Proto;
using GammaRay.Core.InternetAccess;
using GammaRay.Core.Monitoring;
using GammaRay.Core.Monitoring.Converters;
using GammaRay.Core.Network;
using GammaRay.Core.Network.Profiles;
using GammaRay.Core.Routing;
using GammaRay.Core.Routing.Categorization;
using GammaRay.Core.Services;
using GammaRay.Core.Settings;
using GammaRay.Core.Utils;
using GammaRay.Core.Utils.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;
using NetworkIdentity = GammaRay.Core.Network.Identity.NetworkIdentity;
using ServiceIAPStatus = GammaRay.Core.Services.Probing.ServiceIAPStatus;

namespace GammaRay.Client.GUI.ViewModels;

public class MainViewModel : ViewModelBase
{
	private readonly GammaRayAPIClient _apiClient;
	private readonly Window _owner;


	public MainViewModel(GammaRayAPIClient client, Window owner)
	{
		_apiClient = client;
		_owner = owner;

		OpenNewServerConnectionCommand = ReactiveCommand.CreateFromTask(ConnectToServer);
		DisconnectFromServerCommand = ReactiveCommand.CreateFromTask(DisconnectFromServer, this.ObservableForProperty(x => x.IsConnected).Value());
		OpenNetworkWindowCommand = ReactiveCommand.CreateFromTask(OpenNetworkWindow, this.ObservableForProperty(x => x.IsConnected).Value());
	}


	public ConnectionsViewModel Connections { get; } = new();

	public ChannelsViewModel Channels { get; } = new();

	public ServicesViewModel Services { get; } = new();

	public ServerConnection? ServerConnection { get; set { this.RaiseAndSetIfChanged(ref field, value); this.RaisePropertyChanged(nameof(IsConnected)); } }

	public bool IsConnected => ServerConnection is not null;

	public ReactiveCommand<Unit, Unit> OpenNewServerConnectionCommand { get; }

	public ReactiveCommand<Unit, Unit> DisconnectFromServerCommand { get; }

	public ReactiveCommand<Unit, Unit> OpenNetworkWindowCommand { get; }


	private async Task ConnectToServer()
	{
		if (ServerConnection is not null)
		{
			await DisconnectFromServer();
		}

		var dialog = new ConnectServerWindow { DataContext = new ConnectServerWindowViewModel() };
		var connectOptions = await dialog.ShowDialog<ConnectServerWindowDialogResult>(_owner);

		await _apiClient.ConnectAsync(connectOptions.HostName, connectOptions.Port);

		var monitoringConnectionTracker = new MonitoringConnectionTracker(Connections.Connections);
		var monitoringSystem = new MonitoringSystem([monitoringConnectionTracker]);

		var remoteFS = _apiClient.CreateRemoteFileSystemLocator();
		var settings = await remoteFS.GetFileContentAsync("settings.yaml");
		settings ??= await remoteFS.GetFileContentAsync("settings.bak.yaml");
		if (settings is null)
			throw new Exception("Failed to load settings from server");

		var settingServicesBuilder = new ServiceCollection();
		LoadSettings(settings, settingServicesBuilder, remoteFS);
		settingServicesBuilder.AddSingleton<MonitoringSerializerOptionsSource>();
		var settingServices = settingServicesBuilder.BuildServiceProvider();

		var serializerOptionsSource = settingServices.GetRequiredService<MonitoringSerializerOptionsSource>();

		var listener = new APIMonitoringEventListener(monitoringSystem, serializerOptionsSource);
		_apiClient.AddEventListener(listener);

		var observer = new ServerStateObserver(_apiClient, Channels.Channels, Services.Services);
		observer.Start();

		ServerConnection = new ServerConnection(
			_apiClient, serializerOptionsSource, monitoringConnectionTracker,
			monitoringSystem, listener, observer, settingServices.GetRequiredService<NetworkProfileProvider>()
		);
	}

	private async Task DisconnectFromServer()
	{
		if (ServerConnection is null)
			return;

		await ServerConnection.Observer.DisposeAsync();
		_apiClient.RemoveEventListener(ServerConnection.EventListener);

		await _apiClient.DisconnectAsync();

		Connections.Connections.Clear();
		Channels.Channels.Clear();
		Services.Services.Clear();

		ServerConnection = null;
	}

	private async Task OpenNetworkWindow()
	{
		if (ServerConnection is null)
			return;

		var rawMapping = await ServerConnection.APIClient.QueryNetworkProfileMapping(new());
		var mapping = rawMapping.Select(kv => new NetworkProfileMappingViewModel(kv.Key.SerializedForm, kv.Value ?? string.Empty)).ToArray();
		var currentIdentity = await ServerConnection.APIClient.GetCurrentNetworkIdentity();

		string? currentProfile = null;

		if (currentIdentity is not null)
		{
			if (rawMapping.TryGetValue(currentIdentity.Value, out currentProfile) == false || currentProfile is null)
				currentProfile = ServerConnection.NetworkProfiles.DefaultProfile.Name;
		}

		var applyChangesCommand = ReactiveCommand.CreateFromTask(async () => 
		{
			foreach (var mapItem in mapping.Where(s => s.WasChanged))
			{
				var identity = new NetworkIdentity(mapItem.Identity);

				if (ServerConnection.NetworkProfiles.PlainProfiles.Select(s => s.Name).Contains(mapItem.Profile) == false)
					mapItem.Profile = ServerConnection.NetworkProfiles.DefaultProfile.Name;

				await ServerConnection.APIClient.SetNetworkProfileMapping(mapItem.Profile, identity);

				mapItem.WasChanged = false;
			}
		});

		var viewModel = new NetworkWindowViewModel(mapping, currentIdentity?.SerializedForm, currentProfile, applyChangesCommand);

		var networkWindow = new NetworkWindow() { DataContext = viewModel };
		networkWindow.Show(_owner);
	}

	private static void LoadSettings(string settingsContent, IServiceCollection output, IFileSystemLocator fileSystem)
	{
		var YAMLLoader = new YAMLConfigurationLoader();

		var networkProfileRawProvider = new YAMLNetworkProfileRawProvider();
		var endPointCategoryRawProvider = new YAMLEndPointCategoryRawProvider(fileSystem);
		var internetAccessPointRawProvider = new YAMLInternetAccessPointRawProvider();
		var capabilityClassRawProvider = new YAMLCapabilityClassRawProvider();
		var endPointRoutingConfigurationRawProvider = new YAMLEndPointRoutingConfigurationRawProvider();

		YAMLLoader.LoadSettings(settingsContent);

		networkProfileRawProvider.Initialize(YAMLLoader);
		endPointCategoryRawProvider.Initialize(YAMLLoader);
		capabilityClassRawProvider.Initialize(YAMLLoader);

		var networkProfileProvider = new NetworkProfileProvider(networkProfileRawProvider);
		output.AddSingleton(networkProfileProvider);
		var endPointCategoryProvider = new EndPointCategoriesProvider(endPointCategoryRawProvider);
		output.AddSingleton(endPointCategoryProvider);
		var capabilityClassProvider = new CapabilityClassProvider(capabilityClassRawProvider);
		output.AddSingleton(capabilityClassProvider);

		internetAccessPointRawProvider.Initialize(YAMLLoader, networkProfileProvider);
		var internetAccessPointProvider = new InternetAccessPointProvider(internetAccessPointRawProvider, networkProfileProvider);
		output.AddSingleton(internetAccessPointProvider);

		endPointRoutingConfigurationRawProvider.Initialize(YAMLLoader, internetAccessPointProvider);
		var endPointRoutingConfigurationProvider = new EndPointRoutingConfigurationProvider(endPointRoutingConfigurationRawProvider);
		output.AddSingleton(endPointRoutingConfigurationProvider);
	}
}


public sealed class ServerStateObserver(
	GammaRayAPIClient _client,
	ObservableCollection<IAPChannelStatusViewModel> _statusesOutput,
	ObservableCollection<FullServiceInfoViewModel> _servicesOutput
) : IAsyncDisposable
{
	private Task? _observingTask;
	private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(10));


	public void Start()
	{
		_observingTask = Observe();
	}

	public async Task Observe()
	{
		await Task.Yield();
		do
		{
			try
			{
				var statuses = await _client.QueryChannelStatuses(new IAPChannelFilter());
				var convertedStatuses = statuses.Select(s =>
				{
					var CAT = s.CharacteristicAccessTime.Seconds > 100000 ? TimeSpan.Zero : s.CharacteristicAccessTime.ToTimeSpan();
					var AVG = s.AverageAccessTime.Seconds > 100000 ? TimeSpan.Zero : s.AverageAccessTime.ToTimeSpan();
					var Lifetime = s.AverageLifeTime.Seconds > 100000 ? TimeSpan.Zero : s.AverageLifeTime.ToTimeSpan();
					return new IAPChannelStatusViewModel($"{s.IAP}/{s.Channel}", s.Network, CAT, AVG, s.IsAvailable, Lifetime);
				});
				_statusesOutput.Clear();
				_statusesOutput.AddRange(convertedStatuses);

				var services = await _client.QueryServices(new ServiceFilter());
				var now = DateTime.UtcNow;
				var convertedServices = services.Select(s =>
				{
					var table = s.StatusTableData.Select(kv =>
						KeyValuePair.Create(kv.Key, new ServiceIAPStatus((ServiceIAPStatus.StatusType)(int)kv.Value.Type, kv.Value.AverageProbeTime.ToTimeSpan()))
					);

					var remainingTime = (
						s.StatusTableDecayTime is not null
							? Math.Min(s.ServiceDecayTime.ToDateTime(), s.StatusTableDecayTime.ToDateTime())
							: s.ServiceDecayTime.ToDateTime()
					) - now;
					
					remainingTime = Math.Max(remainingTime, TimeSpan.Zero);

					return new FullServiceInfoViewModel(new GenericWebEndPoint(new(s.HostName), s.Port), s.CapabilityClass, table, remainingTime);
				});
				_servicesOutput.Clear();
				_servicesOutput.AddRange(convertedServices);

			}
			catch (Exception ex) { Debugger.BreakForUserUnhandledException(ex); }
		}
		while (await _timer.WaitForNextTickAsync());
	}

	public async ValueTask DisposeAsync()
	{
		_timer.Dispose();
		if (_observingTask is not null)
			await _observingTask;
	}
}
