using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GammaRay.Core.Proxy;

public class ProxyServer
{
	private const string ProxyConnectionHeader = "Proxy-Connection";
	private static readonly string ConnectionEstablishedMessageString =
		new HttpResponseHeader(200, "Connection established", HttpMessageHeader.HTTP11, new HttpHeadersCollection()).Serialize();
	private static readonly byte[] ConnectionEstablishedMessage =
		Encoding.UTF8.GetBytes(ConnectionEstablishedMessageString);


	private readonly Options _options;
	private readonly IProxyServerRouter _router;
	private Statistic _stats;


	public ProxyServer(IOptions<Options> options, IProxyServerRouter router)
	{
		_options = options.Value;
		_router = router;
	}


	public void Run()
	{
		var listener = new TcpListener(_options.ListenEndPoint);
		listener.Start();

		while (true)
		{
			var client = listener.AcceptTcpClient();
			_stats.NewClient();

			Console.WriteLine($"NEW CLIENT: IPEP={client.Client.RemoteEndPoint}, STATS=({_stats})");

			Task.Run(() => HandleClientAsync(client));
		}
	}


	private async Task HandleClientAsync(TcpClient client)
	{
		using var context = new ProxyContext(client, this);
		context.Stream.ReadTimeout = (int)_options.ReadTimeout.TotalMilliseconds;

		bool shouldKeepConnection = false;

		try
		{
			do
			{
				shouldKeepConnection = false;
				_stats.NewRequest();

				try
				{
					var rawHeader = HttpMessageHeader.ReadRawHeader(context.Stream);

					if (rawHeader.Length == 0)
						return;
					var header = HttpRequestHeader.Parse(rawHeader);

					HttpEndPoint endpoint = header.Uri.EndPoint;
					var connection = header.Headers.TryGetSingle(ProxyConnectionHeader);

					if (header.Method == "CONNECT")
					{
						var result = await _router.RouteConnectAsync(context, endpoint);
						Console.WriteLine($"CONNECT {endpoint} -> {result.GetType().Name}");
						if (result is DirectProxyRoutingResult)
							await HandleConnectDirect(context, endpoint);
						else if (result is UpstreamProxyRoutingResult upstream)
							await HandleConnectViaProxy(context, endpoint, upstream.UpstreamProxyServer);
					}
					else
					{
						var result = await _router.RouteHttpAsync(context, endpoint, header);
						Console.WriteLine($"HTTP {endpoint} -> {result.GetType().Name}");
						if (result is DirectProxyRoutingResult)
							await HandleHttpDirect(context, endpoint, header);
						else if (result is UpstreamProxyRoutingResult upstream)
							await HandleHttpViaProxy(context, endpoint, upstream.UpstreamProxyServer, header);
					}

					if (connection == "keep-alive" && context.Client.Connected)
					{
						shouldKeepConnection = true;
					}
				}
				finally
				{
					_stats.FinishRequest();
				}
			}
			while (shouldKeepConnection);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Client handling error: {ex}");
		}
		finally
		{
			_stats.FinishClient();
		}
	}

	static async Task HandleConnectDirect(ProxyContext context, HttpEndPoint endPoint)
	{
		using var server = new TcpClient();
		await server.ConnectAsync(endPoint);
		using var serverStream = server.GetStream();

		await context.Stream.WriteAsync(ConnectionEstablishedMessage);

		await RelayTwoWay(context.Stream, serverStream);
	}

	static async Task HandleConnectViaProxy(ProxyContext context, HttpEndPoint endPoint, IPEndPoint upstreamProxy)
	{
		using var proxy = new TcpClient();
		await proxy.ConnectAsync(upstreamProxy);
		using var proxyStream = proxy.GetStream();

		var connectRequest = new HttpRequestHeader("CONNECT", new HttpUri(null, endPoint, null, null), HttpMessageHeader.HTTP11, [("Host", endPoint.ToString())]).Serialize();
		await proxyStream.WriteAsync(Encoding.UTF8.GetBytes(connectRequest));

		var connectResponse = HttpResponseHeader.Parse(HttpMessageHeader.ReadRawHeader(proxyStream));
		if (connectResponse.Code != 200)
		{
			await context.Stream.WriteAsync(Encoding.UTF8.GetBytes(connectResponse.Serialize()));
			return;
		}

		await context.Stream.WriteAsync(ConnectionEstablishedMessage);

		await RelayTwoWay(context.Stream, proxyStream);
	}

	static async Task HandleHttpDirect(ProxyContext context, HttpEndPoint endPoint, HttpRequestHeader header)
	{
		using var server = new TcpClient();
		await server.ConnectAsync(endPoint);
		using var serverStream = server.GetStream();

		header.Headers.RemoveAll(ProxyConnectionHeader);
		var requestHeader = new HttpRequestHeader(header.Method, header.Uri, header.Version, header.Headers).Serialize();
		await serverStream.WriteAsync(Encoding.UTF8.GetBytes(requestHeader));

		await RelayTwoWay(serverStream, context.Stream);
	}

	static async Task HandleHttpViaProxy(ProxyContext context, HttpEndPoint endPoint, IPEndPoint upstreamProxy, HttpRequestHeader header)
	{
		using var proxy = new TcpClient();
		await proxy.ConnectAsync(endPoint);
		using var proxyStream = proxy.GetStream();

		await proxyStream.WriteAsync(Encoding.UTF8.GetBytes(header.Serialize()));

		await RelayTwoWay(context.Stream, proxyStream);
	}

	static async Task RelayTwoWay(Stream a, Stream b)
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


	public class Options
	{
		public required IPEndPoint ListenEndPoint { get; init; }

		public TimeSpan ReadTimeout { get; init; } = TimeSpan.FromSeconds(10);
	}

	public struct Statistic
	{
		public int TotalRequests;
		public int TotalClients;
		public int ActiveRequests;
		public int ActiveClients;

		public override string ToString()
		{
			return $"tr|tc: {TotalRequests}|{TotalClients}, ar|ac: {ActiveRequests}|{ActiveClients}";
		}

		public void NewClient()
		{
			Interlocked.Increment(ref TotalClients);
			Interlocked.Increment(ref ActiveClients);
		}

		public void NewRequest()
		{
			Interlocked.Increment(ref TotalRequests);
			Interlocked.Increment(ref ActiveRequests);
		}

		public void FinishClient()
		{
			Interlocked.Decrement(ref ActiveClients);
		}

		public void FinishRequest()
		{
			Interlocked.Decrement(ref ActiveRequests);
		}
	}
}