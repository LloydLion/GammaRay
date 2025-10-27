using Microsoft.Extensions.Options;
using Nito.AsyncEx;
using Serilog;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GammaRay.Core.Proxy;

public class ProxyServer
{
	private const string ProxyConnectionHeader = "Proxy-Connection";
	private const string ConnectionHeader = "Connection";
	private static readonly string ConnectionEstablishedMessageString =
		new HttpResponseHeader(200, "Connection established", HttpMessageHeader.HTTP11, []).Serialize();
	private static readonly byte[] ConnectionEstablishedMessage =
		Encoding.UTF8.GetBytes(ConnectionEstablishedMessageString);


	private static readonly ILogger _logger = Log.ForContext<ProxyServer>();

	private readonly Options _options;
	private readonly IProxyServerRouter _router;


	public ProxyServer(IOptions<Options> options, IProxyServerRouter router)
	{
		_options = options.Value;
		_router = router;
	}


	public void Run(IEnumerable<ProxyInbound> inbounds, CancellationToken token = default)
	{
		ActiveInbound[] activeInbounds = inbounds.Select(ActiveInbound.Create).ToArray();

		_logger.Information("Proxy server started, using inbounds: {Inbounds}", activeInbounds.Select(s => $"{s.InboundInfo.Name}|{s.InboundInfo.EndPoint}"));

		AsyncContext.Run(async () =>
		{
			var onlineClients = new HashSet<Task>();
			foreach (var inbound in activeInbounds)
			{
				inbound.StartListen();
				inbound.AcceptNewClient();
			}

			try
			{
				while (token.IsCancellationRequested == false)
				{
					try
					{
						var finishedTask = await Task.WhenAny(activeInbounds.Select(s => s.AcceptTask));
						var targetInbound = Array.Find(activeInbounds, s => s.AcceptTask == finishedTask);
						targetInbound!.AcceptNewClient();

						var clientSocket = finishedTask.Result;

						onlineClients.Add(HandleClientAsync(targetInbound!, clientSocket));
					}
					catch (TaskCanceledException) { }
				}
			}
			finally
			{
				await Task.WhenAll(onlineClients);
			}
		});
	}


	private async Task HandleClientAsync(ActiveInbound inbound, Socket client)
	{
		await Task.Yield();

		using var clientContext = new ProxyClientContext(client, this, _logger);
		clientContext.Stream.WriteTimeout = clientContext.Stream.ReadTimeout = (int)_options.MasterClientTimeout.TotalMilliseconds;
		clientContext.Logger.Information("New client connected from {Inbound}, RemoteEndPoint: {EndPoint}", inbound.InboundInfo.Name, clientContext.Socket.RemoteEndPoint);

		ProxyRequestContext? requestContext = null;

		try
		{
			bool shouldKeepConnection;
			int ordinalRequestNumber = 0;
			do
			{
				ordinalRequestNumber++;
				shouldKeepConnection = false;

				int waitCycles = (int)(_options.MasterClientTimeout.TotalMilliseconds / 250);
				while (clientContext.Stream.DataAvailable == false && waitCycles-- != 0)
					await Task.Delay(250);
				if (waitCycles == -1)
				{
					clientContext.Logger.Information("Closing connection due client inactivity");
					return;
				}

				clientContext.Logger.Debug("New data available. Trying to read socket");

				var rawHeader = HttpMessageHeader.ReadRawHeader(clientContext.Stream);
				if (rawHeader.Length == 0) return;
				var header = HttpRequestHeader.Parse(rawHeader);

				HttpEndPoint endpoint = header.Uri.EndPoint;
				var requestType = header.Method == "CONNECT" ? HttpProxyRequestType.Connect : HttpProxyRequestType.HTTP;
				var connection = header.Headers.TryGetSingle(ProxyConnectionHeader);

				requestContext = new ProxyRequestContext(clientContext, header, endpoint, requestType, ordinalRequestNumber);
				requestContext.Logger.Information("New request: {RequestType} to {EndPointHost}:{EndPointPort}. Request header: {@RequestHeader}",
					requestContext.RequestType, requestContext.EndPoint.Host, requestContext.EndPoint.Port, requestContext.Header);

				var result = await _router.RouteRequestAsync(requestContext);
				requestContext.Logger.Information("Routed to {ClientConfigurations}", result.ClientConfigurations.Select(s => s.Name));

				await HandleRequestAsync(requestContext, result);
				requestContext.Logger.Information("Request finished");

				if (string.Equals(connection, "keep-alive", StringComparison.OrdinalIgnoreCase))
				{
					clientContext.Logger.Information("Client requested to keep connection alive");
					shouldKeepConnection = true;
				}
			}
			while (shouldKeepConnection);
		}
		catch (Exception ex)
		{
			if (requestContext is not null)
				requestContext.Logger.Error(ex, "Error while handling request");
			else
				clientContext.Logger.Error(ex, "Error while handling client");
		}
		finally
		{
			clientContext.Logger.Information("Client done, connection closed");
		}
	}

	private static async Task HandleRequestAsync(ProxyRequestContext context, ProxyRoutingResult routingDecision)
	{
		NetClientConfiguration? usedConfiguration = null;
		using var client = new TcpClient();
		foreach (var configuration in routingDecision.ClientConfigurations)
		{
			try
			{
				ConfigureClient(client, configuration);

				switch ((context.RequestType, configuration.ProxyServer))
				{
					case (HttpProxyRequestType.Connect, not null):
						await ConnectClientAsync(client, configuration, configuration.ProxyServer);
						await DialConnectWithProxyServerAsync(client, context.EndPoint);
						await context.Stream.WriteAsync(ConnectionEstablishedMessage);
						break;

					case (HttpProxyRequestType.Connect, null):
						await ConnectClientAsync(client, configuration, context.EndPoint);
						await context.Stream.WriteAsync(ConnectionEstablishedMessage);
						break;

					case (HttpProxyRequestType.HTTP, not null):
						await ConnectClientAsync(client, configuration, configuration.ProxyServer);
						await SendHeaderToRemote(client, context.Header);
						break;

					case (HttpProxyRequestType.HTTP, null):
						await ConnectClientAsync(client, configuration, context.Header.Uri.EndPoint);
						context.Header.Headers.RemoveAll(ProxyConnectionHeader);
						context.Header.Headers.RemoveAll(ConnectionHeader);
						context.Header.Headers.Add(ConnectionHeader, "close");
						await SendHeaderToRemote(client, context.Header);
						break;
				}

				usedConfiguration = configuration;
				break;
			}
			catch (SocketException)
			{
				continue;
			}
			catch (ProxyDialException ex)
			{
				context.Logger.Warning(ex, "Failed dial with upstream proxy server (from '{ConfigurationName}')", configuration.Name);
				continue;
			}
		}

		if (usedConfiguration is null)
			throw new Exception($"Enable to connect to {context.EndPoint} or/and one of the upstream proxies");

		context.Logger.Information("Connected using configuration: '{ConfigurationName}'", usedConfiguration.Name);

		await RelayTwoWay(context.Stream, client.GetStream());
	}


	private static async Task DialConnectWithProxyServerAsync(TcpClient connectedClient, HttpEndPoint endPoint)
	{
		var proxyStream = connectedClient.GetStream();

		var connectRequest = new HttpRequestHeader("CONNECT", new HttpUri(null, endPoint, null, null), HttpMessageHeader.HTTP11, [("Host", endPoint.ToString())]).Serialize();
		await proxyStream.WriteAsync(Encoding.UTF8.GetBytes(connectRequest));

		var connectResponse = HttpResponseHeader.Parse(HttpMessageHeader.ReadRawHeader(proxyStream));
		if (connectResponse.Code != 200)
			throw new ProxyDialException($"Proxy server return {connectResponse.Code} status code with reason: {connectResponse.Reason}", proxyStream.Socket.RemoteEndPoint);
	}

	private static async ValueTask SendHeaderToRemote(TcpClient client, HttpMessageHeader messageHeader)
	{
		var message = messageHeader.Serialize();
		await client.GetStream().WriteAsync(Encoding.UTF8.GetBytes(message));
	}

	private static async Task RelayTwoWay(Stream a, Stream b)
	{
		using var cts = new CancellationTokenSource();

		var t1 = Task.Run(async () =>
		{
			try { await a.CopyToAsync(b, cts.Token); }
			catch { }
		});

		var t2 = Task.Run(async () =>
		{
			try { await b.CopyToAsync(a, cts.Token); }
			catch { }
		});

		await Task.WhenAny(t1, t2);
		cts.Cancel();
		await Task.WhenAll(t1, t2);
	}

	private static void ConfigureClient(TcpClient client, NetClientConfiguration configuration)
	{
		client.SendTimeout = client.ReceiveTimeout = (int)configuration.Timeout.TotalMilliseconds;
	}

	private static ValueTask<TcpClient> ConnectClientAsync(TcpClient client, NetClientConfiguration configuration, HttpEndPoint endPoint) =>
		ConnectClientAsync(client, configuration, (client) => client.ConnectAsync(endPoint));

	private static ValueTask<TcpClient> ConnectClientAsync(TcpClient client, NetClientConfiguration configuration, IPEndPoint endPoint) =>
		ConnectClientAsync(client, configuration, (client) => client.ConnectAsync(endPoint));

	private static async ValueTask<TcpClient> ConnectClientAsync(TcpClient client, NetClientConfiguration configuration, Func<TcpClient, Task> connectDelegate)
	{
		int retries = configuration.MaxRequestCount;
		while (retries > 0)
		{
			try
			{
				await connectDelegate(client);
				break;
			}
			catch (SocketException)
			{
				retries--;
				await Task.Delay(configuration.RequestInternal);
			}
		}

		return client;
	}


	public class Options
	{
		public TimeSpan MasterClientTimeout { get; init; } = TimeSpan.FromSeconds(10);
	}

	private class ProxyDialException(string message, EndPoint? proxyServer) : Exception($"{message}. Proxy: {proxyServer}") { }

	private class ActiveInbound(ProxyInbound inboundInfo, Socket socket)
	{
		private Task<Socket>? _acceptTask;


		public ProxyInbound InboundInfo { get; } = inboundInfo;

		public Socket Socket { get; } = socket;

		public Task<Socket> AcceptTask => _acceptTask ?? throw new NullReferenceException();


		public void StartListen()
		{
			Socket.Listen();
		}

		public void AcceptNewClient()
		{
			_acceptTask = Socket.AcceptAsync();
		}

		public static ActiveInbound Create(ProxyInbound inbound)
		{
			var socket = new Socket(inbound.EndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			socket.Bind(inbound.EndPoint);
			return new ActiveInbound(inbound, socket);
		}
	}
}
