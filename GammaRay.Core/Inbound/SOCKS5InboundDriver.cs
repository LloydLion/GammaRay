using GammaRay.Core.Monitoring;
using GammaRay.Core.Network;
using GammaRay.Core.Network.Flow.Implementation;
using GammaRay.Core.Protocols.SOCKS5;
using GammaRay.Core.Utils;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GammaRay.Core.Inbound;

[RecommendedDriverName("socks")]
public sealed class SOCKS5InboundDriver(
	TimeProvider _time,
	MonitoringSystem _monitoringSystem
) : IInboundDriver
{
	private readonly TimeProvider _time = _time;
	private readonly MonitoringSystem _monitoringSystem = _monitoringSystem;


	public IInbound CreateInbound(IPEndPoint localEndPoint)
	{
		return new Inbound(localEndPoint, this);
	}


	private class Inbound(IPEndPoint _localEndPoint, SOCKS5InboundDriver _owner) : IInbound
	{
		private readonly ArrayPool<byte> _pool = ArrayPool<byte>.Create();
		private IncomingRequestCallback? _requestCallback;

		private readonly SocksAddressType _myAddressType = _localEndPoint.AddressFamily switch
		{
			AddressFamily.InterNetwork => SocksAddressType.IPVersion4,
			AddressFamily.InterNetworkV6 => SocksAddressType.IPVersion6,
			_ => throw new NotSupportedException($"Address family {_localEndPoint.AddressFamily} is not supported")
		};
		private readonly byte[] _myAddress = _localEndPoint.Address.GetAddressBytes();
		private readonly int _myPort = _localEndPoint.Port;


		public void OnNewRequest(IncomingRequestCallback callback) => _requestCallback = callback;

		public async Task Run(CancellationToken stopToken = default)
		{
			if (_requestCallback is null)
				throw new InvalidOperationException($"Request callback is required. Set it using {nameof(OnNewRequest)}");

			var socket = new Socket(_localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			socket.Bind(_localEndPoint);
			socket.Listen();

			var onlineClients = new HashSet<Task>();

			while (stopToken.IsCancellationRequested == false)
			{
				try
				{
					var clientSocket = await socket.AcceptAsync(stopToken);
					processClient(clientSocket);
				}
				catch (Exception) { }
			}

			await Task.WhenAll(onlineClients);


			async void processClient(Socket client)
			{
				var handleTask = HandleClient(client);
				onlineClients.Add(handleTask);
				await handleTask;
				onlineClients.Remove(handleTask);
			}
		}

		private async Task HandleClient(Socket client)
		{
			await Task.Yield();

			var messageBuffer = _pool.Rent(256);

			var now = _owner._time.GetUtcNow().UtcDateTime;
			using var procedure = TrackableProcedure.New("Connection", now, _owner._monitoringSystem);

			try
			{
				RequestContext requestContext;

				
				using (var report = new Report(procedure))
				{
					report.RemoteEndPoint = (IPEndPoint)client.RemoteEndPoint!;

					var clientHello = await SocksClientHelloMessage.ReadMessageFromSocketAsync(client, messageBuffer);

					var serverHello = new SocksServerHelloMessage(
						clientHello.SupportedAuthMethods.Span.Contains(SocksAuthMethod.NoAuth) == false ? SocksAuthMethod.Invalid : SocksAuthMethod.NoAuth
					);
					serverHello.Serialize(messageBuffer);
					await client.SendAsync(messageBuffer[..SocksServerHelloMessage.FixedBinLength]);

					if (serverHello.ChosenMethod != SocksAuthMethod.NoAuth)
						return;

					await client.ReceiveAsync(Array.Empty<byte>()); // wait for request

					var request = await SocksClientRequestMessage.ReadMessageFromSocketAsync(client, messageBuffer);
					var materializedAddress = MaterializeAddress(request); // buffer will be reused, so we need to materialize the address before it gets overwritten
					var endPoint = new WebEndPoint(materializedAddress, request.Port, TransportType.StreamBased);
					report.AddressType = request.AddressType;
					report.DestinationEndPoint = endPoint;

					var reply = new SocksServerReplyMessage(
						request.Command == SocksClientCommand.Connect ? SocksReplyCode.Succeeded : SocksReplyCode.CommandNotSupported,
						_myAddressType, _myAddress, _myPort
					);

					var written = reply.Serialize(messageBuffer);
					await client.SendAsync(messageBuffer[..written]);

					if (reply.Code != SocksReplyCode.Succeeded)
						return;

					var incomingFlow = new SocketBasedStreamDataFlow(client);
					requestContext = new RequestContext(endPoint, incomingFlow, now, procedure);
				}

				await _requestCallback!.Invoke(this, requestContext);
			}
			catch (Exception ex)
			{
				procedure.SetFatalException(ex);
			}
			finally
			{
				_pool.Return(messageBuffer);

				try { await client.DisconnectAsync(false); }
				catch (Exception) { }

				client.Dispose();
			}
		}


		private static WebHost MaterializeAddress(SocksClientRequestMessage request)
		{
			return new WebHost(request.AddressType switch
			{
				SocksAddressType.IPVersion4 => new IPAddress(request.Address.Span).ToString(),
				SocksAddressType.IPVersion6 => new IPAddress(request.Address.Span).ToString(),
				SocksAddressType.DomainName => Encoding.ASCII.GetString(request.Address.Span[1..]),
				_ => throw new NotSupportedException($"Unsupported address type {request.AddressType}")
			});
		}
	}

	[SystemReportMetadata(nameof(IInboundDriver), nameof(SOCKS5InboundDriver), "HandleRequest")]
	public class Report(TrackableProcedure? autoBind = null) : SystemReport(autoBind)
	{
		public ReportProperty<IPEndPoint> RemoteEndPoint { get; set; }

		public ReportProperty<SocksAddressType> AddressType { get; set; }

		public ReportProperty<WebEndPoint> DestinationEndPoint { get; set; }
	}
}
