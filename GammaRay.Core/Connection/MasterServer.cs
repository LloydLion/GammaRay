using GammaRay.Core.Connection.Inbound;
using GammaRay.Core.Connection.Observation;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Monitoring;
using GammaRay.Core.Network;
using GammaRay.Core.Network.Flow;
using GammaRay.Core.Routing;
using GammaRay.Core.Utils;
using System.Buffers;
using System.Net;

namespace GammaRay.Core.Connection;

public sealed class MasterServer(
	IRouter router,
	IChannelDriverRegistry channelDriverRegistry,
	MonitoringSystem monitoringSystem,
	TimeProvider time
) : IMasterServer, IDisposable
{
	private const int CaptureInterval = 10;


	private readonly IRouter _router = router;
	private readonly IChannelDriverRegistry _channelDriverRegistry = channelDriverRegistry;
	private readonly MonitoringSystem _monitoringSystem = monitoringSystem;
	private readonly TimeProvider _time = time;
	private readonly Dictionary<Guid, ClientConnection> _connections = [];
	private readonly ArrayPool<byte> _joinPool = ArrayPool<byte>.Create(ushort.MaxValue, 256);
	private bool _disposed;
	private ITimer? _captureTimer;
	private SynchronizationContext? _synchronization;


	public async Task Run(NamedInbound[] inbounds, CancellationToken cancellationToken)
	{
		_synchronization = SynchronizationContext.Current ?? new();
		_captureTimer = _time.CreateTimer((_) => _synchronization.Post(CaptureConnections, null),
			null, TimeSpan.FromSeconds(CaptureInterval), TimeSpan.FromSeconds(CaptureInterval));

		var tasks = new List<Task>(inbounds.Length);

		foreach (var inbound in inbounds)
		{
			var agent = new Agent(inbound, this);
			inbound.Instance.SetMaster(agent);

			tasks.Add(inbound.Instance.Run(cancellationToken));
		}

		await Task.WhenAll(tasks);
	}

	public void Dispose()
	{
		_disposed = true;
		_captureTimer?.Dispose();
	}

	private void CaptureConnections(object? _)
	{
		if (_disposed)
			return;

		foreach (var (_, connection) in _connections)
		{
			if (connection.WasEstablished == false)
				continue;

			var establishInfo = connection.EstablishInfo;

			var context = establishInfo.Observer.Context;
			var frame = new ConnectionObservationFrame(context.BytesReceived, context.BytesSent);
			establishInfo.Observer.ResetContext();

			establishInfo.ObservationRow.Push(frame);

			var isStale = CheckIfStale(connection);
			if (isStale != connection.IsStale)
			{
				connection.DefineAsStale(isStale);
				connection.Procedure.CommitReport(new ConnectionStaleReport(isStale));
			}

			TryReroute(connection);
		}
	}

	private static bool CheckIfStale(ClientConnection connection)
	{
		if (connection.WasEstablished == false)
			return false;
		var observationRow = connection.EstablishInfo.ObservationRow;
		if (observationRow.Buffer.IsFull == false)
			return false;

		foreach (var frame in observationRow.Buffer.InternalBuffer)
		{
			var txSpeed = frame.BytesSent / CaptureInterval;
			if (txSpeed > 2048)
				return false;

			var rxSpeed = frame.BytesReceived / CaptureInterval;
			if (rxSpeed > 2048)
				return false;
		}

		return true;
	}

	private void TryReroute(ClientConnection connection)
	{
		if (connection.WasEstablished == false || connection.IsStale == false)
			return;

		var request = connection.Request.Value;

		var routingRequest = new RoutingRequest(request.TargetEndPoint, connection.Procedure);

		var newDecision = _router.MakeRoutingDecision(routingRequest);

		if (connection.RoutingResult.Value.Channel != newDecision.Channel)
		{
			connection.Procedure.CommitReport(new ConnectionReroutedReport(newDecision));
			connection.Reroute(newDecision);
			request.IncomingConnection.ResetConnection();
		}
	}


	private class Agent(NamedInbound _inbound, MasterServer _owner) : IMasterServerInboundAgent
	{
		public ClientConnection CreateBlankConnection(IPEndPoint remoteEndPoint)
		{
			var now = _owner._time.GetUtcNow().UtcDateTime;
			var connection = new ClientConnection(new(remoteEndPoint, _inbound), _owner._monitoringSystem, Guid.NewGuid(), now);

			var report = new NewConnectionReport(remoteEndPoint, _inbound.Name);
		#if ENABLE_PID_GATHER
			report.PID = TcpProcessLookup.GetProcessIdByLocalPort(remoteEndPoint.Port);
		#endif
			connection.Procedure.CommitReport(report);

			_owner._connections.Add(connection.Id, connection);
			return connection;
		}

		public void HandleFatalError(ClientConnection connection, Exception exception)
		{
			connection.MarkAsErrored(exception);
			_owner._connections.Remove(connection.Id);
		}

		public async Task HandleRequest(ClientConnection connection, ClientConnectionRequest request)
		{
			byte[]? bufferAToB = null, bufferBToA = null;
			try
			{
				connection.AddRequest(request);
				connection.Procedure.CommitReport(new ConnectionRequestReport(request.TargetEndPoint));

				var routingRequest = new RoutingRequest(request.TargetEndPoint, connection.Procedure);

				var routingDecision = _owner._router.MakeRoutingDecision(routingRequest);

				connection.AddRoute(routingDecision);
				connection.Procedure.CommitReport(new ConnectionRoutedReport(routingDecision));

				var openingResult = await _owner._channelDriverRegistry
					.ProvideDriver(routingDecision.Channel.DriverName)
					.TryOpenChannelAsync(routingDecision, request.TargetEndPoint);

				if (openingResult.Type != ChannelOpeningResult.ResultType.Success)
					throw new Exception($"Enable to open channel {routingDecision}", openingResult.InternalException);

				await using (var openChannel = openingResult.OpenChannel)
				{
					var channelFlow = openChannel.GetFlow();
					var incomingConnection = request.IncomingConnection.GetFlow();

					var observer = new ConnectionObserver();

					bufferAToB = _owner._joinPool.Rent(ushort.MaxValue);
					bufferBToA = _owner._joinPool.Rent(ushort.MaxValue);

					var joinTask = incomingConnection.JoinAsync(channelFlow, bufferAToB, bufferBToA, observer);

					var establishTime = _owner._time.GetUtcNow().UtcDateTime;
					var establishInfo = new ClientConnectionEstablishInfo(observer, joinTask, openChannel, new(3), establishTime);

					connection.Establish(establishInfo);
					connection.Procedure.CommitReport(new ConnectionEstablishedReport());

					await joinTask;
				}

				if (connection.IsRerouted == false)
					connection.CloseByRemote();
			}
			catch (Exception ex)
			{
				connection.MarkAsErrored(ex);
			}
			finally
			{
				if (bufferBToA is not null)
					_owner._joinPool.Return(bufferBToA);
				if (bufferAToB is not null)
					_owner._joinPool.Return(bufferAToB);
				_owner._connections.Remove(connection.Id);
			}
		}
	}

	[SystemReportMetadata(nameof(IMasterServer), nameof(MasterServer), "NewConnection")]
	public class NewConnectionReport(ReportProperty<IPEndPoint> remoteEndPoint = default, ReportProperty<string> inbound = default) : SystemReport
	{
		public ReportProperty<IPEndPoint> RemoteEndPoint { get; set; } = remoteEndPoint;

		public ReportProperty<string> Inbound { get; set; } = inbound;

	#if ENABLE_PID_GATHER
		public ReportProperty<int> PID { get; set; }
	#endif
	}

	[SystemReportMetadata(nameof(IMasterServer), nameof(MasterServer), "ConnectionRequest")]
	public class ConnectionRequestReport(ReportProperty<WebEndPoint> destinationEndPoint = default) : SystemReport
	{
		public ReportProperty<WebEndPoint> DestinationEndPoint { get; set; } = destinationEndPoint;
	}

	[SystemReportMetadata(nameof(IMasterServer), nameof(MasterServer), "ConnectionRouted")]
	public class ConnectionRoutedReport(ReportProperty<NamedIAPChannel> routingResult = default) : SystemReport
	{
		public ReportProperty<NamedIAPChannel> RoutingResult { get; set; } = routingResult;
	}

	[SystemReportMetadata(nameof(IMasterServer), nameof(MasterServer), "ConnectionEstablished")]
	public class ConnectionEstablishedReport : SystemReport { }

	[SystemReportMetadata(nameof(IMasterServer), nameof(MasterServer), "ConnectionStale")]
	public class ConnectionStaleReport(ReportProperty<bool> isStale = default) : SystemReport
	{
		public ReportProperty<bool> IsStale { get; set; } = isStale;
	}

	[SystemReportMetadata(nameof(IMasterServer), nameof(MasterServer), "ConnectionRerouted")]
	public class ConnectionReroutedReport(ReportProperty<NamedIAPChannel> reroutingResult = default) : SystemReport
	{
		public ReportProperty<NamedIAPChannel> ReroutingResult { get; set; } = reroutingResult;
	}
}
