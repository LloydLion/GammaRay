using GammaRay.Core.Monitoring;
using Nito.AsyncEx;
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
	private readonly IMonitoringSystem _monitoringSystem;
	private readonly TimeProvider _time;
	private readonly Ping _pingAgent = new();
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
			MonitoringContext? context = null;
			Report? report = null;
			try
			{
				context = new MonitoringContext("NetworkIdentityRefresh", _time, _monitoringSystem);
				report = context.NewReport<Report>();
				report.IdentifierName = GetType().Name;

				var newIdentity = FetchCurrentNetworkIdentity(context);

				var isInternetReachable = await PingInternet();

				_identity = isInternetReachable ? newIdentity : null;
				_lastRefresh = _time.GetUtcNow().DateTime;

				foreach (var subscriber in _subscribers)
					subscriber.Call();

				report.NewNetworkIdentity = CurrentIdentity;
			}
			catch (Exception ex)
			{
				report?.Exception = ex;
			}
			finally
			{
				report?.Finish();
				context?.Close();
				_isRefreshing = 0;
			}

		}

		if (sync)
			SynchronizationContext.Send((_) => AsyncContext.Run(callback), null);
		else
			SynchronizationContext.Post(async (_) => await callback(), null);

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

	private async ValueTask<bool> PingInternet()
	{
		var reply = await _pingAgent.SendPingAsync(InternetAddress, PingTimeout);
		return reply.Status switch
		{
			IPStatus.Success => true,
			_ => false
		};
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
		public ReportProperty<NetworkIdentity?> NewNetworkIdentity { get; set => SetProperty(ref field, value.Value); }

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
