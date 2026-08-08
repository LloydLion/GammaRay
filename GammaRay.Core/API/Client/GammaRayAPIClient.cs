using GammaRay.Core.API.Services.Proto;
using Grpc.Core;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using GammaRay.Core.Utils.FileSystem;

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

	public IFileSystemLocator CreateRemoteFileSystemLocator()
	{
		return new RemoteFileSystemLocator(RequireConnection().ServiceClient.FileSystem);
	}

	public async ValueTask RequestReloadApplicationAsync()
	{
		await RequireConnection().ServiceClient.Control.ReloadApplicationAsync(new Empty());
		await DisconnectAsync();
	}

	public async ValueTask RequestWriteSettingsAsync(string settingsContent)
	{
		await RequireConnection().ServiceClient.FileSystem.SetFileContentAsync(new SetFileContentRequest { Path = "settings.json", Content = settingsContent });
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

	public async ValueTask<Network.Identity.NetworkIdentity?> GetCurrentNetworkIdentity()
	{
		var identity = await RequireConnection().ServiceClient.Network.GetCurrentNetworkIdentityAsync(new Empty());
		return identity.HasSerializedForm ? new Network.Identity.NetworkIdentity(identity.SerializedForm) : null;
	}

	public async ValueTask<IReadOnlyDictionary<Network.Identity.NetworkIdentity, string?>> QueryNetworkProfileMapping(NetworkProfileMappingFilter filter)
	{
		var stream = RequireConnection().ServiceClient.Network.QueryNetworkProfileMapping(filter).ResponseStream;
		var result = new Dictionary<Network.Identity.NetworkIdentity, string?>();
		while (await stream.MoveNext())
		{
			var current = stream.Current;
			result.Add(new Network.Identity.NetworkIdentity(current.NetworkIdentity), current.HasNetworkProfile ? current.NetworkProfile : null);
		}
		return result;
	}

	public async ValueTask SetNetworkProfileMapping(string profile, Network.Identity.NetworkIdentity identity)
	{
		await RequireConnection().ServiceClient.Network.SetNetworkProfileMappingAsync(
			new NetworkProfileMapping() { NetworkIdentity = identity.SerializedForm, NetworkProfile = profile }
		);
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
		catch (RpcException rpc) when (rpc.StatusCode == StatusCode.Cancelled) { }
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
