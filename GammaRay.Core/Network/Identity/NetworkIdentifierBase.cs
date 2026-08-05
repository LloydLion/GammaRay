using GammaRay.Core.Monitoring;
using Nito.AsyncEx;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace GammaRay.Core.Network.Identity;

public abstract class NetworkIdentifierBase : INetworkIdentifier, IDisposable
{
	private readonly static TimeSpan RefreshEventTimeout = TimeSpan.FromSeconds(3);
	private readonly static TimeSpan PingTimeout = TimeSpan.FromSeconds(3);
	private readonly static IPAddress InternetAddress = new([1, 1, 1, 1]);


	private readonly ITimer _timer;
	private readonly HashSet<Subscription> _subscribers = [];
	private readonly MonitoringSystem _monitoringSystem;
	private readonly TimeProvider _time;
	private readonly Ping _pingAgent = new();
	private SynchronizationContext? _synchronizationContext;
	private DateTime? _lastRefresh;
	private NetworkIdentity? _lastReachableIdentity;
	private NetworkIdentity? _identity;
	private int _isRefreshing = 0;


	protected NetworkIdentifierBase(MonitoringSystem monitoringSystem, TimeProvider time)
	{
		_monitoringSystem = monitoringSystem;
		_time = time;
		_timer = time.CreateTimer(RefreshEvent, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
	}


	public DateTime LastRefresh => _lastRefresh ?? throw ThrowNotInitialized();

	public NetworkIdentity? LastReachableIdentity => _lastRefresh is null ? throw ThrowNotInitialized() : _lastReachableIdentity;

	public NetworkIdentity? CurrentIdentity => _lastRefresh is null ? throw ThrowNotInitialized() : _identity;

	protected SynchronizationContext SynchronizationContext => _synchronizationContext ?? throw ThrowNotInitialized();


	public void Initialize()
	{
		_synchronizationContext = SynchronizationContext.Current ?? new();
		InitiateNetworkIdentityRefresh(sync: true);
		NetworkChange.NetworkAddressChanged += NetworkChanged;
	}

	public IDisposable SubscribeForChanges(Action<INetworkIdentifier> callback)
	{
		var subscription = new Subscription(this, callback);
		_subscribers.Add(subscription);
		return subscription;
	}

	public void Dispose()
	{
		_timer.Dispose();
		GC.SuppressFinalize(this);
	}

	public bool InitiateNetworkIdentityRefresh(bool sync = false)
	{
		if (Interlocked.Exchange(ref _isRefreshing, 1) == 1)
			return false;

		async Task callback()
		{
			_lastRefresh = _time.GetUtcNow().DateTime;
			TrackableProcedure procedure = TrackableProcedure.New("NetworkIdentityRefresh", _time, _monitoringSystem);

			try
			{
				using var report = new Report(procedure) { IdentifierName = GetType().Name };

				var newIdentity = FetchCurrentNetworkIdentity(procedure);

				var isInternetReachable = await PingInternet();

				_identity = isInternetReachable ? newIdentity : null;
				if (isInternetReachable) _lastReachableIdentity = newIdentity;

				foreach (var subscriber in _subscribers)
					subscriber.Call();

				report.NewNetworkIdentity = CurrentIdentity;
			}
			catch (Exception ex)
			{
				Debugger.BreakForUserUnhandledException(ex);
				procedure.SetFatalException(ex);
			}
			finally
			{
				procedure.Finish();
				_isRefreshing = 0;
			}

		}

		if (sync)
			SynchronizationContext.Send((_) => AsyncContext.Run(callback), null);
		else
			SynchronizationContext.Post(async (_) => await callback(), null);

		return true;
	}

	protected abstract NetworkIdentity FetchCurrentNetworkIdentity(TrackableProcedure procedure);

	protected static IPAddress TraceRouteToInternet()
	{
		using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		socket.Connect(InternetAddress, 53);
		return ((IPEndPoint)socket.LocalEndPoint!).Address;
	}

	protected static NetworkInterface GetInterfaceByIP(IPAddress ipAddress)
	{
		foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
			foreach (var interfaceAddress in networkInterface.GetIPProperties().UnicastAddresses)
				if (interfaceAddress.Address.Equals(ipAddress))
					return networkInterface;
		throw new Exception("No network interface found for IP " + ipAddress);
	}

	private async ValueTask<bool> PingInternet()
	{
		int successInRow = 0;
		for (int i = 0; i < 5; i++)
		{
			var reply = await _pingAgent.SendPingAsync(InternetAddress, PingTimeout);
			var success = reply.Status == IPStatus.Success;
			if (success)
				successInRow++;
			else successInRow = 0;

			if (successInRow == 3)
				return true;

			var remainingTries = 5 - i - 1;
			var requiredSuccessesInRow = 3 - successInRow;
			if (requiredSuccessesInRow > remainingTries)
				return false;
		}

		return false;
	}

	private void NetworkChanged(object? sender, EventArgs e)
	{
		_timer.Change(RefreshEventTimeout, Timeout.InfiniteTimeSpan);
	}

	private void RefreshEvent(object? state)
	{
		var started = InitiateNetworkIdentityRefresh();
		if (started == false)
			_timer.Change(RefreshEventTimeout, Timeout.InfiniteTimeSpan);
	}

	private static InvalidOperationException ThrowNotInitialized() => new($"Not initialized. Call {nameof(Initialize)}() method first");


	[SystemReportMetadata(nameof(INetworkIdentifier), nameof(NetworkIdentifierBase), "RefreshIdentity")]
	public class Report(TrackableProcedure? autoBindProcedure = null) : SystemReport(autoBindProcedure)
	{
		public ReportProperty<NetworkIdentity?> NewNetworkIdentity { get; set; }

		public ReportProperty<Exception> Exception { get; set; }

		public ReportProperty<string> IdentifierName { get; set; }
	}

	private class Subscription(
		NetworkIdentifierBase _owner,
		Action<INetworkIdentifier> _callback
	) : IDisposable
	{
		public void Dispose() => _owner._subscribers.Remove(this);

		public void Call()
		{
			try
			{
				_callback(_owner);
			}
			catch (Exception) { }
		}
	}
}
