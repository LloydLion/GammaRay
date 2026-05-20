using GammaRay.Core.Inbound;
using GammaRay.Core.Monitoring;
using GammaRay.Core.Network;
using GammaRay.Core.Routing;

namespace GammaRay.Client.TUI;

public sealed class ConnectionTrackingMonitoringSystem : IMonitoringSystem, IDisposable
{
	private readonly Action<ConnectionTrackingMonitoringSystem> _updateCallback;
	private readonly Dictionary<Guid, OnlineConnection> _connections = [];
	private readonly HashSet<Guid> _toDelete = [];
	private readonly ITimer _deleteTimer;
	private bool _deleteTimerScheduled = false;
	private readonly SynchronizationContext _synchronization;


	public ConnectionTrackingMonitoringSystem(Action<ConnectionTrackingMonitoringSystem> updateCallback, TimeProvider time)
	{
		_synchronization = SynchronizationContext.Current ?? new();
		_updateCallback = updateCallback;
		_deleteTimer = time.CreateTimer(DeleteTimerCallback, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
	}


	public IReadOnlyDictionary<Guid, OnlineConnection> Connections => _connections;


	public void NewContext(MonitoringContext context)
	{

	}

	public void CloseContext(MonitoringContext context)
	{
		if (_connections.TryGetValue(context.Id, out var connection))
		{
			connection.CurrentStatus = OnlineConnection.Status.Closed;
			if (_deleteTimerScheduled == false)
			{
				_deleteTimer.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
				_deleteTimerScheduled = true;
			}
		}

		Update();
	}

	public void NewReport(SystemReport report)
	{

	}

	public void FinishReport(SystemReport report)
	{
		if (report.MonitoringContext.Type != "Connection")
			return;

		switch (report)
		{
			case HTTPInboundDriver.Report httpReport:
				{
					var newConnection = new OnlineConnection(
						report.MonitoringContext,
						httpReport.RemoteEndPoint.Value,
						new WebEndPoint(httpReport.DestinationEndPoint.Value, TransportType.StreamBased),
						"HTTP"
					);

					_connections.Add(newConnection.Id, newConnection);
					Update();
					break;
				}

			case SOCKS5InboundDriver.Report socksReport:
				{
					var newConnection = new OnlineConnection(
						report.MonitoringContext,
						socksReport.RemoteEndPoint.Value,
						socksReport.DestinationEndPoint.Value,
						"SOCKS5"
					);

					_connections.Add(newConnection.Id, newConnection);
					Update();
					break;
				}

			case SmartRouter.Report smartRouterReport:
				{
					if (_connections.TryGetValue(smartRouterReport.MonitoringContext.Id, out var connection))
					{
						connection.RoutingResult = (smartRouterReport.ResultIAP.Value, smartRouterReport.ResultChannelName.Value);
						Update();
					}
					break;
				}
		}
	}

	public void SetReportProperty<TProperty>(SystemReport report, string propertyName, ReportProperty<TProperty> oldValue, TProperty newValue)
	{

	}

	private void Update()
	{
		_updateCallback(this);
	}

	private void DeleteTimerCallback(object? _) => _synchronization.Post(SynchronizedDeleteTimerCallback, null);

	private void SynchronizedDeleteTimerCallback(object? _)
	{
		bool shouldReschedule = false;

		foreach (var connection in _connections.Values)
			if (connection.CurrentStatus == OnlineConnection.Status.Closed)
			{
				connection.TTL -= 1;
				if (connection.TTL <= 0)
					_toDelete.Add(connection.Id);
				else shouldReschedule = true;
			}

		bool shouldUpdate = _toDelete.Any();
		foreach (var item in _toDelete)
			_connections.Remove(item);
		_toDelete.Clear();

		if (shouldUpdate)
			Update();

		if (shouldReschedule)
			_deleteTimer.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
		else _deleteTimerScheduled = false;
	}

	public void Dispose()
	{
		_deleteTimer.Dispose();
		GC.SuppressFinalize(this);
	}
}
