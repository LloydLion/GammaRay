using GammaRay.Core.API.Services.Proto;
using Grpc.Core;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;

namespace GammaRay.Core.API.Client;

public class GammaRayAPIClient() : IGammaRayAPIClient
{
	private ConnectionContext? _connection;
	private readonly HashSet<IAPIEventListener> _eventListeners = [];


	[MemberNotNullWhen(true, nameof(_connection))]
	public bool IsConnected => _connection is not null;


	public async ValueTask ConnectAsync(string hostname, int port)
	{
		var channel = new Channel(hostname, port, ChannelCredentials.Insecure);

		var aggregateClient = new AggregateServiceClient(channel);

		var cts = new CancellationTokenSource();

		_connection = new ConnectionContext(channel, aggregateClient, cts);
		_connection.MonitoringEventsReceiveLoopTask = ReceiveEventsLoop(_connection);
	}

	public async ValueTask DisconnectAsync() => await DisconnectAsync(fromReceiveLoop: false);

	public async ValueTask<int> RequestAPIVVersionAsync()
	{
		var response = await RequireConnection().ServiceClient.Basic.GetAPIVersionAsync(new Empty());
		return response.Version;
	}

	public async ValueTask<string> RequestReadSettingsAsync()
	{
		var response = await RequireConnection().ServiceClient.Settings.GetCurrentSettingsFileAsync(new Empty());
		return response.Content;
	}

	public async ValueTask RequestReloadApplicationAsync()
	{
		await RequireConnection().ServiceClient.Control.ReloadApplicationAsync(new Empty());
		await DisconnectAsync();
	}

	public async ValueTask RequestWriteSettingsAsync(string settingsContent)
	{
		await RequireConnection().ServiceClient.Settings.UploadNewSettingsFileAsync(new SettingsFileRequest { Content = settingsContent });
	}

	public void AddEventListener(IAPIEventListener listener) => _eventListeners.Add(listener);

	public void RemoveEventListener(IAPIEventListener listener) => _eventListeners.Remove(listener);

	public async ValueTask<IReadOnlyCollection<FullServiceInfoReponse>> QueryServices(ServiceFilter serviceFilter)
	{
		var stream = RequireConnection().ServiceClient.Services.QueryFullServiceInfo(serviceFilter).ResponseStream;
		var result = new List<FullServiceInfoReponse>();
		while (await stream.MoveNext())
			result.Add(stream.Current);
		return result;
	}

	public async ValueTask<IReadOnlyCollection<IAPChannelStatusResponse>> QueryChannelStatuses(IAPChannelFilter channelFilter)
	{
		var stream = RequireConnection().ServiceClient.Channels.QueryIAPChannelStatus(channelFilter).ResponseStream;
		var result = new List<IAPChannelStatusResponse>();
		while (await stream.MoveNext())
			result.Add(stream.Current);
		return result;
	}

	private async Task ReceiveEventsLoop(ConnectionContext connection)
	{
		await Task.Yield();
		try
		{
			using var call = connection.ServiceClient.Monitoring.SubscribeEvents(new Empty(), cancellationToken: connection.Cancelation.Token);
			while (await call.ResponseStream.MoveNext(connection.Cancelation.Token))
			{
				foreach (var listener in _eventListeners)
				{
					bool handled = false;
					try
					{
						handled = listener.HandleEvent(this, call.ResponseStream.Current);
					}
					catch (Exception) { }

					if (handled)
						break;
				}
			}
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			Debugger.BreakForUserUnhandledException(ex);
			await DisconnectAsync(fromReceiveLoop: true);
		}
	}

	private ConnectionContext RequireConnection() => _connection ?? throw new InvalidOperationException("Connect client first");
	
	private async ValueTask DisconnectAsync(bool fromReceiveLoop)
	{
		if (IsConnected == false)
			throw new InvalidOperationException("Not connected");

		_connection.Cancelation.Cancel();
		if (_connection.MonitoringEventsReceiveLoopTask is not null && fromReceiveLoop == false)
			await _connection.MonitoringEventsReceiveLoopTask;

		await _connection.Channel.ShutdownAsync();

		_connection = null;
	}


	private class ConnectionContext(Channel channel, AggregateServiceClient serviceClient, CancellationTokenSource cts)
	{
		public Channel Channel { get; } = channel;

		public AggregateServiceClient ServiceClient { get; } = serviceClient;

		public CancellationTokenSource Cancelation { get; } = cts;

		public Task? MonitoringEventsReceiveLoopTask { get; set; }
	}
}
