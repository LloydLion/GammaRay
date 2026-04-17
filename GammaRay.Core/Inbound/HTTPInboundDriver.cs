using GammaRay.Core.Monitoring;
using GammaRay.Core.Network;
using GammaRay.Core.Network.Flow.Implementation;
using GammaRay.Core.Protocols.HTTP;
using GammaRay.Core.Utils;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HttpRequestHeader = GammaRay.Core.Protocols.HTTP.HttpRequestHeader;
using HttpResponseHeader = GammaRay.Core.Protocols.HTTP.HttpResponseHeader;

namespace GammaRay.Core.Inbound;

[RecommendedDriverName("http")]
public sealed class HTTPInboundDriver(
	TimeProvider _time,
	IMonitoringSystem _monitoringSystem,
	IOptions<HTTPInboundDriver.Options> options
) : IInboundDriver
{
	private readonly Options _options = options.Value;
	private readonly TimeProvider _time = _time;
	private readonly IMonitoringSystem _monitoringSystem = _monitoringSystem;


	public IInbound CreateInbound(IPEndPoint localEndPoint)
	{
		return new Inbound(localEndPoint, this);
	}


	public class Options
	{
		public TimeSpan MasterClientTimeout { get; set; } = TimeSpan.FromSeconds(2);
	}

	private class Inbound(IPEndPoint localEndPoint, HTTPInboundDriver owner) : IInbound
	{
		private const string ProxyConnectionHeader = "Proxy-Connection";
		private static readonly string ConnectionEstablishedMessageString =
			new HttpResponseHeader(200, "Connection established", HttpMessageHeader.HTTP11, []).Serialize();
		private static readonly byte[] ConnectionEstablishedMessage =
			Encoding.UTF8.GetBytes(ConnectionEstablishedMessageString);


		private readonly IPEndPoint _localEndPoint = localEndPoint;
		private readonly HTTPInboundDriver _owner = owner;
		private IncomingRequestCallback? _requestCallback;


		public void OnNewRequest(IncomingRequestCallback callback) => _requestCallback = callback;

		public async Task Run(CancellationToken stopToken = default)
		{
			if (_requestCallback is null)
				throw new InvalidOperationException($"Request callback is required. Set it using {nameof(OnNewRequest)}");

			var socket = CreateSocket();
			socket.Listen();

			var onlineClients = new HashSet<Task>();

			while (stopToken.IsCancellationRequested == false)
			{
				try
				{
					var clientSocket = await socket.AcceptAsync(stopToken);
					var clientContext = ConfigureIncomingClient(clientSocket);
					processClient(clientContext);
				}
				catch (Exception) { }
			}

			await Task.WhenAll(onlineClients);


			async void processClient(ProxyClientContext context)
			{
				var handleTask = HandleClient(context);
				onlineClients.Add(handleTask);
				await handleTask;
				onlineClients.Remove(handleTask);
			}
		}

		private async Task HandleClient(ProxyClientContext clientContext)
		{
			await Task.Yield();

			try
			{
				bool shouldKeepConnection;
				do
				{
					// -- Wait for new Data
					var isClientConnected = await AwaitNewData(clientContext.Socket);
					if (isClientConnected == false)
						return;

					// -- Prepare to processing
					var now = _owner._time.GetUtcNow().UtcDateTime;
					using var monitoring = new MonitoringContext("Connection", now, _owner._monitoringSystem);
					using var report = monitoring.NewReport<Report>();
					report.RemoteEndPoint = (IPEndPoint)clientContext.Socket.RemoteEndPoint!;

					// -- Read HTTP header for proxy
					var rawHeader = HttpMessageHeader.ReadRawHeader(clientContext.Stream);
					if (rawHeader.Length == 0)
						return;
					var header = HttpRequestHeader.Parse(rawHeader);
					var destinationEndPoint = header.Uri.EndPoint;
					destinationEndPoint ??= GenericWebEndPoint.Parse(header.Headers.TryGetSingle("Host")
						?? throw new Exception("Client do not specified destination host"));
					report.DestinationEndPoint = destinationEndPoint.Value;

					// -- Create request context
					var requestType = header.Method == "CONNECT" ? HttpProxyRequestType.Connect : HttpProxyRequestType.HTTP;
					var requestContext = new RequestContext(
						new WebEndPoint(destinationEndPoint.Value, TransportType.StreamBased),
						FormIncomingDataFlow(clientContext, requestType),
						now, monitoring
					);

					// -- Write response
					await clientContext.Stream.WriteAsync(ConnectionEstablishedMessage);

					// -- Call callback
					await CallCallback(requestContext);

					// -- Make connection decision
					shouldKeepConnection = false;
					var connectionHeaderValue = header.Headers.TryGetSingle(ProxyConnectionHeader);
					if (string.Equals(connectionHeaderValue, "keep-alive", StringComparison.OrdinalIgnoreCase))
					{
						shouldKeepConnection = true;
					}
					report.ShouldKeepConnectionAlive = shouldKeepConnection;
				}
				while (shouldKeepConnection);
			}
			catch (Exception) { }
			finally
			{
				clientContext.Dispose();
			}
		}

		private static SocketBasedStreamDataFlow FormIncomingDataFlow(ProxyClientContext clientContext, HttpProxyRequestType requestType)
		{
			if (requestType != HttpProxyRequestType.Connect)
				throw new NotSupportedException();

			return new SocketBasedStreamDataFlow(clientContext.Socket);
		}

		private static async Task<bool> AwaitNewData(Socket socket)
		{
			var oldTimeout = socket.ReceiveTimeout;
			socket.ReceiveTimeout = -1;

			await socket.ReceiveAsync(Array.Empty<byte>());

			socket.ReceiveTimeout = oldTimeout;

			return socket.Connected;
		}

		private Socket CreateSocket()
		{
			var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			socket.Bind(_localEndPoint);
			return socket;
		}

		private ProxyClientContext ConfigureIncomingClient(Socket clientSocket)
		{
			var clientContext = new ProxyClientContext(clientSocket);
			clientContext.Stream.WriteTimeout = clientContext.Stream.ReadTimeout = _owner._options.MasterClientTimeout.TotalMillisecondsInt;
			return clientContext;
		}

		private ValueTask CallCallback(RequestContext requestContext) => _requestCallback!.Invoke(this, requestContext);


		public class Report() : SystemReport(nameof(HTTPInboundDriver))
		{
			public ReportProperty<IPEndPoint> RemoteEndPoint { get; set => SetProperty(ref field, value.Value); }

			public ReportProperty<GenericWebEndPoint> DestinationEndPoint { get; set => SetProperty(ref field, value.Value); }

			public ReportProperty<bool> ShouldKeepConnectionAlive { get; set => SetProperty(ref field, value.Value); }
		}
	}
}
