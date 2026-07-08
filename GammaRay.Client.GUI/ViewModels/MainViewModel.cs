using Avalonia.Controls;
using DynamicData;
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
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;
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
	}


	public ConnectionsViewModel Connections { get; } = new();

	public ChannelsViewModel Channels { get; } = new();

	public ServicesViewModel Services { get; } = new();

	public ServerConnection? ServerConnection { get; set { this.RaiseAndSetIfChanged(ref field, value); this.RaisePropertyChanged(nameof(IsConnected)); } }

	public bool IsConnected => ServerConnection is not null;

	public ReactiveCommand<Unit, Unit> OpenNewServerConnectionCommand { get; }

	public ReactiveCommand<Unit, Unit> DisconnectFromServerCommand { get; }


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

		var settings = await _apiClient.RequestReadSettingsAsync();
		var serializerOptionsSource = LoadSettings(settings);

		var listener = new APIMonitoringEventListener(monitoringSystem, serializerOptionsSource);
		_apiClient.AddEventListener(listener);

		var observer = new ServerStateObserver(_apiClient, Channels.Channels, Services.Services);
		observer.Start();

		ServerConnection = new ServerConnection(
			_apiClient, serializerOptionsSource, monitoringConnectionTracker, monitoringSystem, listener, observer
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

	private static MonitoringSerializerOptionsSource LoadSettings(string settingsContent)
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
				var convertedStatuses = statuses.Select(s => new IAPChannelStatusViewModel($"{s.IAP}/{s.Channel}", s.Network, s.CharacteristicAccessTime.ToTimeSpan(), s.IsAvailable));
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
