using Microsoft.Extensions.Options;
using Nito.AsyncEx;
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
						var finishedTask = (await Task.WhenAny(activeInbounds.Select(s => s.AcceptTask)));
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

		using var clientContext = new ProxyClientContext(client, this);
		clientContext.Stream.WriteTimeout = clientContext.Stream.ReadTimeout = (int)_options.MasterClientTimeout.TotalMilliseconds;

		Console.WriteLine($"New client from {inbound.InboundInfo.Name}: {clientContext}");

		try
		{
			bool shouldKeepConnection;
			int ordinalRequestNumber = 0;
			do
			{
				ordinalRequestNumber++;
				shouldKeepConnection = false;

				while (clientContext.Stream.DataAvailable == false)
					await Task.Delay(250);

				var rawHeader = HttpMessageHeader.ReadRawHeader(clientContext.Stream);
				if (rawHeader.Length == 0) return;
				var header = HttpRequestHeader.Parse(rawHeader);

				HttpEndPoint endpoint = header.Uri.EndPoint;

				var requestType = header.Method == "CONNECT" ? HttpProxyRequestType.Connect : HttpProxyRequestType.HTTP;

				var connection = header.Headers.TryGetSingle(ProxyConnectionHeader);

				var requestContext = new ProxyRequestContext(clientContext, header, endpoint, requestType, ordinalRequestNumber);

				Console.WriteLine($"New request: {requestContext}");

				var result = await _router.RouteRequestAsync(requestContext);

				Console.WriteLine($"{requestContext} === Routed to [{string.Join(", ", result.ClientConfigurations.Select(s => s.Name))}]");

				await HandleRequestAsync(requestContext, result);

				if (connection == "keep-alive" && clientContext.Socket.Connected)
					shouldKeepConnection = true;
			}
			while (shouldKeepConnection);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"{client} === ERROR {ex}");
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
			catch (ProxyDialException)
			{
				continue;
			}
		}

		if (usedConfiguration is null)
			throw new Exception($"Enable to connect to {context.EndPoint} or/and one of the upstream proxies");

		Console.WriteLine($"{context} === Connected using configuration: {usedConfiguration.Name}");

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
