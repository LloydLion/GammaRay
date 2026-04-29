using GammaRay.Core.Host;
using GammaRay.Core.Settings;
using GammaRay.Core.Utils;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;

namespace GammaRay.Core.API;

public sealed class APIServer
{
	private readonly IDriverRegistry<IAPIEndPointDriver> _drivers;
	private readonly APIConfigurationProvider _configurationProvider;
	private readonly APIBasedMonitoringSystem _monitoringSystem;
	private readonly ApplicationControl _applicationControl;
	private readonly SettingsFileHolder _settingsFile;
	private readonly Dictionary<APIConstants.RequestType, Func<RequestContext, ValueTask<int>>> _handlers;


	public APIServer(
		IDriverRegistry<IAPIEndPointDriver> drivers,
		APIConfigurationProvider configurationProvider,
		APIBasedMonitoringSystem monitoringSystem,
		ApplicationControl applicationControl,
		SettingsFileHolder settingsFile
	)
	{
		_handlers = new()
		{
			{ APIConstants.RequestType.GetAPIVersion, HandleGetAPIVersionRequest },
			{ APIConstants.RequestType.ControlMonitoring, HandleControlMonitoringRequest },
			{ APIConstants.RequestType.GetCurrentSettingsFile, HandleGetCurrentSettingsFile },
			{ APIConstants.RequestType.UploadNewSettingsFile, HandleUploadNewSettingsFile },
			{ APIConstants.RequestType.ReloadApplication, HandleReloadApplication },
		};
		_drivers = drivers;
		_configurationProvider = configurationProvider;
		_monitoringSystem = monitoringSystem;
		_applicationControl = applicationControl;
		_settingsFile = settingsFile;
	}


	public async Task Run(CancellationToken cancellationToken = default)
	{
		var endPointsInformation = _configurationProvider.EndPoints;
		var endPoints = endPointsInformation.Select(info =>
			_drivers.ProvideDriver(info.Protocol).CreateListening(info.ConfigurationString)
		).ToArray();

		foreach (var endPoint in endPoints)
			endPoint.SetConnectionHandler(ConnectionHandler);

		var tasks = new HashSet<Task>(endPoints.Length);
		foreach (var endPoint in endPoints)
			tasks.Add(endPoint.Run(cancellationToken));

		await Task.WhenAll(tasks);
	}

	private async Task ConnectionHandler(APIConnection connection, CancellationToken cancellation)
	{
		using var webSocket = WebSocket.CreateFromStream(connection.Stream, true, null, TimeSpan.FromSeconds(10));

		var sink = new APIEventSink(webSocket);
		var buffer = new byte[APIConstants.AllocationBufferSize];

		while (true)
		{
			var receiveResult = await webSocket.ReceiveAsync(buffer, cancellation);
			if (receiveResult.EndOfMessage == false)
				continue;
			if (receiveResult.MessageType is WebSocketMessageType.Text)
				continue;
			if (receiveResult.MessageType is WebSocketMessageType.Close)
				break;

			var requestLength = receiveResult.Count;

			int responseLength;
			var requestType = (APIConstants.RequestType)buffer[0];
			buffer[0] = (byte)APIConstants.ServerMessageType.Response;

			try
			{
				if (_handlers.TryGetValue(requestType, out var handler) == false)
				{
					buffer[0] = (byte)APIConstants.ResponseCode.UnknownRequestType;
					responseLength = 2;
				}
				else
				{
					var context = new RequestContext(buffer, connection, sink, requestLength);
					responseLength = await handler(context) + 1;
				}
			}
			catch (Exception ex)
			{
				var exceptionString = ex.ToString();
				var wrote = Encoding.UTF8.GetBytes(exceptionString, buffer.AsSpan(2..));
				buffer[1] = (byte)APIConstants.ResponseCode.ServerSideError;
				responseLength = wrote + 2;
			}

			await webSocket.SendAsync(buffer.AsMemory(..responseLength), WebSocketMessageType.Binary, true, default);
		}
	}

	private class APIEventSink(WebSocket _webSocket) : IAPIEventSink
	{
		public async ValueTask SendEvent(Memory<byte> buffer)
		{
			if (_webSocket.State != WebSocketState.Open)
				return;
			try
			{
				buffer.Span[0] = (byte)APIConstants.ServerMessageType.Event;
				await _webSocket.SendAsync(buffer, WebSocketMessageType.Binary, false, default);
			}
			catch (Exception ex) { Console.WriteLine(ex); }
		}
	}

	private ValueTask<int> HandleGetAPIVersionRequest(RequestContext p)
	{
		p.IOBuffer[1] = (byte)APIConstants.ResponseCode.Success;
		p.IOBuffer[2] = APIConstants.APIVersion;
		return ValueTask.FromResult(2);
	}

	private ValueTask<int> HandleControlMonitoringRequest(RequestContext p)
	{
		var newMode = (APIConstants.MonitoringMode)p.IOBuffer[1];
		if (Enum.IsDefined(newMode) == false)
		{
			p.IOBuffer[1] = (byte)APIConstants.ResponseCode.ClientSideError;
			var wrote = Encoding.UTF8.GetBytes("Not defined MonitoringMode", p.IOBuffer.AsSpan(2..));
			return ValueTask.FromResult(wrote + 1);
		}

		if (newMode == APIConstants.MonitoringMode.Disabled)
		{
			_monitoringSystem.StopTranslation(p.Sink);
		}
		else
		{
			_monitoringSystem.ConfigureTranslation(p.Sink, new()
			{
				Enabled = true,
				PropertyTranslationEnabled = newMode == APIConstants.MonitoringMode.EnabledWithReportProperties
			});
		}

		p.IOBuffer[1] = (byte)APIConstants.ResponseCode.Success;
		return ValueTask.FromResult(1);
	}

	private async ValueTask<int> HandleGetCurrentSettingsFile(RequestContext p)
	{
		using var settingsFile = _settingsFile.ReadConfigurationFile();
		var settings = await settingsFile.ReadToEndAsync();
		var wrote = Encoding.UTF8.GetBytes(settings, p.IOBuffer.AsSpan(2..));
		p.IOBuffer[1] = (byte)APIConstants.ResponseCode.Success;
		return wrote + 1;
	}

	private async ValueTask<int> HandleUploadNewSettingsFile(RequestContext p)
	{
		using var settingsFile = _settingsFile.WriteConfigurationFile();
		var settings = Encoding.UTF8.GetString(p.IOBuffer.AsSpan(1..p.RequestLength));
		await settingsFile.WriteAsync(settings);
		p.IOBuffer[1] = (byte)APIConstants.ResponseCode.Success;
		return 1;
	}

	private ValueTask<int> HandleReloadApplication(RequestContext p)
	{
		_applicationControl.Restart();
		p.IOBuffer[1] = (byte)APIConstants.ResponseCode.Success;
		return ValueTask.FromResult(1);
	}


	private readonly record struct RequestContext(byte[] IOBuffer, APIConnection Connection, APIEventSink Sink, int RequestLength);
}
