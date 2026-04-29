using GammaRay.Core.Monitoring;
using GammaRay.Core.Utils;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text;

namespace GammaRay.Core.API.Client;

public class GammaRayAPIClient(TimeProvider time, IOptions<GammaRayAPIClient.Options> options) : IGammaRayAPIClient
{
	private ConnectionContext? _connection;
	private readonly Options _options = options.Value;
	private readonly HashSet<IAPIEventListener> _eventListeners = [];
	private readonly ArrayPool<byte> _pool = ArrayPool<byte>.Create();
	private readonly TimeoutHandle _responseTimeoutHandle = new(time);


	public bool IsConnected => _connection is not null;


	public async ValueTask ConnectAsync(IAPIEndPointDriver driver, string configurationString)
	{
		var capturedContext = SynchronizationContext.Current ?? new();
		var clientConnection = await driver.ConnectAsClientAsync(configurationString);
		var webSocket = WebSocket.CreateFromStream(clientConnection.Stream, isServer: false, subProtocol: null, TimeSpan.FromSeconds(10));

		_connection = new ConnectionContext(clientConnection, webSocket, capturedContext);
		_connection.ReceiveLoopTask = ReceiveLoop(_connection);
	}

	public async ValueTask DisconnectAsync()
	{
		ThrowIfNotConnected();
		var loopTask = _connection.ReceiveLoopTask;
		await DisconnectInternalAsync();
		if (loopTask is not null)
			await loopTask;
	}

	public async ValueTask<byte> RequestAPIVVersionAsync() =>
		await Request(APIConstants.RequestType.GetAPIVersion, (_, _) => 0, (response, _) => response[0], 0);

	public async ValueTask<string> RequestReadSettingsAsync() =>
		await Request(APIConstants.RequestType.GetCurrentSettingsFile, (_, _) => 0, (response, _) => Encoding.UTF8.GetString(response), 0);

	public async ValueTask RequestReloadApplicationAsync()
	{
		await Request(APIConstants.RequestType.ReloadApplication, (_, _) => 0, (_, _) => 0, 0);
		await DisconnectAsync();
	}

	public async ValueTask RequestWriteSettingsAsync(string settingsContent) =>
		await Request(APIConstants.RequestType.UploadNewSettingsFile, (request, settingsContent) => Encoding.UTF8.GetBytes(settingsContent, request), (_, _) => 0, settingsContent);

	public async ValueTask ControlMonitoringAsync(APIConstants.MonitoringMode monitoringMode) =>
		await Request(APIConstants.RequestType.ControlMonitoring, (request, monitoringMode) => { request[0] = (byte)monitoringMode; return 1; }, (_, _) => 0, monitoringMode);

	public void AddEventListener(IAPIEventListener listener) => _eventListeners.Add(listener);

	public void RemoveEventListener(IAPIEventListener listener) => _eventListeners.Remove(listener);


	private async ValueTask DisconnectInternalAsync() // Made for disconnecting from receive loop where it cannot await itself
	{
		ThrowIfNotConnected();
		await _connection.ClientConnection.CloseAsync();
		_connection.Cancellation.Cancel();
		_connection = null;
	}

	[MemberNotNull(nameof(_connection))]
	private void ThrowIfNotConnected()
	{
		if (_connection is null)
			throw new InvalidOperationException("Client is not connected");
	}

	private async ValueTask<TResult> Request<TResult, TContext>(
		APIConstants.RequestType requestType,
		Func<Span<byte>, TContext, int> requestFormer,
		Func<ReadOnlySpan<byte>, TContext, TResult> responseHandler,
		TContext context
	)
	{
		ThrowIfNotConnected();
		var buffer = _pool.Rent(APIConstants.AllocationBufferSize);
		try
		{
			buffer[0] = (byte)requestType;
			var wrote = requestFormer(buffer.AsSpan(1..), context);
			var requestBuffer = buffer.AsMemory(..(wrote + 1));

			var receiveTask = new ResponseReceiveTask<TContext, TResult>(responseHandler, context);
			_connection.ReceiveTask = receiveTask;

			await _connection.WebSocket.SendAsync(requestBuffer, WebSocketMessageType.Binary, true, default);

			var result = await _responseTimeoutHandle.DoAsyncOperationWithTimeout(
				_options.ResponseTimeout, receiveTask, async (receiveTask, cancel) =>
					await receiveTask.TaskSource.Task.WaitAsync(cancel)
			);

			return result;
		}
		catch (IOException ex)
		{
			await DisconnectAsync();
			throw new GammaRayAPIException("IO exception, connection closed", ex);
		}
		catch (TimeoutException)
		{
			await DisconnectAsync();
			throw new GammaRayAPIException("Timeout, connection closed");
		}
		finally
		{
			_pool.Return(buffer);
		}
	}

	private async Task ReceiveLoop(ConnectionContext connection)
	{
		try
		{
			await Task.Yield();

			var cancellationToken = connection.Cancellation.Token;
			var buffer = _pool.Rent(APIConstants.AllocationBufferSize);

			while (cancellationToken.IsCancellationRequested == false)
			{
				ValueWebSocketReceiveResult receiveResult;
				try
				{
					receiveResult = await connection.WebSocket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
				}
				catch (OperationCanceledException)
				{
					return;
				}
				catch (Exception)
				{
					await DisconnectInternalAsync();
					return;
				}

				if (cancellationToken.IsCancellationRequested)
					return;
				if (receiveResult.MessageType is WebSocketMessageType.Text)
					continue;
				if (receiveResult.MessageType is WebSocketMessageType.Close)
				{
					await DisconnectInternalAsync();
					return;
				}

				var receivedBuffer = buffer.AsSpan(1..receiveResult.Count);

				var type = (APIConstants.ServerMessageType)buffer[0];

				var receiveTask = connection.ReceiveTask;
				connection.ReceiveTask = null;

				if (type is APIConstants.ServerMessageType.Response && receiveTask is not null)
				{
					receiveTask.HandleResponse(receivedBuffer);
				}
				else if (type is APIConstants.ServerMessageType.Event)
				{
					HandleEvent(receivedBuffer);
				}
			}
		}
		catch (Exception ex) { Debugger.BreakForUserUnhandledException(ex); }
	}

	private void HandleEvent(ReadOnlySpan<byte> eventData)
	{
		foreach (var listener in _eventListeners)
		{
			bool handled = false;
			try
			{
				handled = listener.HandleEvent(this, eventData);
			}
			catch (Exception ex)
			{
				//Debugger.BreakForUserUnhandledException(ex);
				Console.WriteLine(ex);
			}

			if (handled)
				return;
		}		
	}


	private class ConnectionContext(
		IAPIClientConnection clientConnection,
		WebSocket webSocket,
		SynchronizationContext capturedContext
	)
	{
		public ResponseReceiveTask? ReceiveTask { get; set; }

		public IAPIClientConnection ClientConnection { get; } = clientConnection;

		public WebSocket WebSocket { get; } = webSocket;

		public SynchronizationContext CapturedContext { get; } = capturedContext;

		public CancellationTokenSource Cancellation { get; } = new();

		public Task? ReceiveLoopTask { get; set; }
	}

	private sealed class ResponseReceiveTask<TContext, TResult>(Func<ReadOnlySpan<byte>, TContext, TResult> callback, TContext context) : ResponseReceiveTask
	{
		public Func<ReadOnlySpan<byte>, TContext, TResult> Callback { get; } = callback;

		public TContext Context { get; } = context;

		public TaskCompletionSource<TResult> TaskSource { get; } = new();


		public override void HandleResponse(ReadOnlySpan<byte> response)
		{
			var responseCode = (APIConstants.ResponseCode)response[0];
			if (responseCode != APIConstants.ResponseCode.Success)
			{
				var messageFromServer = Encoding.UTF8.GetString(response[1..]);
				TaskSource.SetException(new GammaRayAPIException($"Request failed with response code {responseCode}: {messageFromServer}"));
				return;
			}

			try
			{
				TaskSource.SetResult(Callback(response[1..], Context));
			}
			catch (Exception ex)
			{
				TaskSource.SetException(ex);
			}
		}
	}

	private abstract class ResponseReceiveTask
	{
		public abstract void HandleResponse(ReadOnlySpan<byte> response);
	}

	public class Options
	{
		public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromSeconds(10);
	}
}
