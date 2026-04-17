using GammaRay.Core.Monitoring;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace GammaRay.Core.Network.Identity;

public abstract class NetworkIdentifierBase : INetworkIdentifier, IDisposable
{
	private readonly static TimeSpan RefreshEventTimeout = TimeSpan.FromSeconds(3);
	private readonly static IPAddress InternetAddress = new([1, 1, 1, 1]);


	private readonly ITimer _timer;
	private readonly HashSet<Subscription> _subscribers = [];
	private readonly IMonitoringSystem _monitoringSystem;
	private readonly TimeProvider _time;
	private SynchronizationContext? _synchronizationContext;
	private DateTime? _lastRefresh;
	private NetworkIdentity? _identity;
	private int _isRefreshing = 0;


	protected NetworkIdentifierBase(IMonitoringSystem monitoringSystem, TimeProvider time)
	{
		_monitoringSystem = monitoringSystem;
		_time = time;
		_timer = time.CreateTimer(RefreshEvent, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
	}


	public DateTime LastRefresh => _lastRefresh ?? throw ThrowNotInitialized();

	public NetworkIdentity CurrentIdentity => _identity ?? throw ThrowNotInitialized();

	protected SynchronizationContext SynchronizationContext => _synchronizationContext ?? throw ThrowNotInitialized();


	public void Initialize()
	{
		_synchronizationContext = SynchronizationContext.Current;
		InitiateNetworkIdentityRefresh();
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

	public bool InitiateNetworkIdentityRefresh()
	{
		if (Interlocked.Exchange(ref _isRefreshing, 1) == 1)
			return false;

		using var context = new MonitoringContext("NetworkIdentityRefresh", _time, _monitoringSystem);
		using var report = context.NewReport<Report>();
		report.IdentifierName = GetType().Name;

		try
		{
			_identity = FetchCurrentNetworkIdentity(context);
			_lastRefresh = DateTime.UtcNow;

			foreach (var subscriber in _subscribers)
				subscriber.Call();

			report.NewNetworkIdentity = CurrentIdentity;
		}
		catch (Exception ex)
		{
			report.Exception = ex;
		}
		finally
		{
			_isRefreshing = 0;
		}

		return true;
	}

	protected abstract NetworkIdentity FetchCurrentNetworkIdentity(MonitoringContext monitoringContext);

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


	public class Report() : SystemReport(nameof(NetworkIdentifierBase))
	{
		public ReportProperty<NetworkIdentity> NewNetworkIdentity { get; set => SetProperty(ref field, value.Value); }

		public ReportProperty<Exception> Exception { get; set => SetProperty(ref field, value.Value); }

		public ReportProperty<string> IdentifierName { get; set => SetProperty(ref field, value.Value); }
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
