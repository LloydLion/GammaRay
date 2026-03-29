using GammaRay.Core.Network;
using GammaRay.Core.Network.Flow.Implementation;
using GammaRay.Core.Protocols.HTTP;
using GammaRay.Core.Utils;
using Microsoft.Extensions.Options;
using Serilog;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HttpRequestHeader = GammaRay.Core.Protocols.HTTP.HttpRequestHeader;
using HttpResponseHeader = GammaRay.Core.Protocols.HTTP.HttpResponseHeader;

namespace GammaRay.Core.Inbound;

[RecommendedDriverName("http")]
public sealed class HTTPInboundDriver(IOptions<HTTPInboundDriver.Options> options) : IInboundDriver
{
	private readonly Options _options = options.Value;


	public IInbound CreateInbound(IPEndPoint localEndPoint)
	{
		return new Inbound(localEndPoint, _options);
	}


	public class Options
	{
		public TimeSpan MasterClientTimeout { get; set; } = TimeSpan.FromSeconds(2);
	}

	private class Inbound(IPEndPoint localEndPoint, Options options) : IInbound
	{
		private static readonly ILogger _logger = Log.ForContext<Inbound>();

		private const string ProxyConnectionHeader = "Proxy-Connection";
		private static readonly string ConnectionEstablishedMessageString =
			new HttpResponseHeader(200, "Connection established", HttpMessageHeader.HTTP11, []).Serialize();
		private static readonly byte[] ConnectionEstablishedMessage =
			Encoding.UTF8.GetBytes(ConnectionEstablishedMessageString);


		private readonly IPEndPoint _localEndPoint = localEndPoint;
		private readonly Options _options = options;
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

					// -- Read HTTP header for proxy
					var rawHeader = HttpMessageHeader.ReadRawHeader(clientContext.Stream);
					if (rawHeader.Length == 0)
						return;
					var header = HttpRequestHeader.Parse(rawHeader);
					var destinationEndPoint = header.Uri.EndPoint;
					destinationEndPoint ??= GenericWebEndPoint.Parse(header.Headers.TryGetSingle("Host")
						?? throw new Exception("Client do not specified destination host"));

					// -- Create request context
					var requestType = header.Method == "CONNECT" ? HttpProxyRequestType.Connect : HttpProxyRequestType.HTTP;
					var requestContext = new RequestContext(
						new WebEndPoint(destinationEndPoint.Value, TransportType.StreamBased),
						FormIncomingDataFlow(clientContext, requestType)
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
						clientContext.Logger.Information("Client requested to keep connection alive");
						shouldKeepConnection = true;
					}
				}
				while (shouldKeepConnection);
			}
			catch (Exception ex)
			{
				clientContext.Logger.Error(ex, "Error while handling client");
			}
			finally
			{
				clientContext.Logger.Information("Client done, connection closed");

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
			var clientContext = new ProxyClientContext(clientSocket, _logger);
			clientContext.Stream.WriteTimeout = clientContext.Stream.ReadTimeout = _options.MasterClientTimeout.TotalMillisecondsInt;
			return clientContext;
		}

		private ValueTask CallCallback(RequestContext requestContext) => _requestCallback!.Invoke(this, requestContext);
	}
}
