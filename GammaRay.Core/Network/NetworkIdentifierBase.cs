using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace GammaRay.Core.Network;

public abstract class NetworkIdentifierBase : INetworkIdentifier
{
	private const int RefreshEventTimeout = 3000;


	private readonly Timer _timer;
	private DateTime? _lastRefresh;
	private NetworkIdentity? _identity;
	private int _isRefreshing = 0;


	protected NetworkIdentifierBase(OSPlatform targetPlatform)
	{
		TargetPlatform = targetPlatform;
		_timer = new(RefreshEvent);
	}


	public OSPlatform TargetPlatform { get; }

	public DateTime LastRefresh { get { InitializeIfNeed(); return _lastRefresh.Value; } }

	public NetworkIdentity CurrentIdentity { get { InitializeIfNeed(); return _identity.Value; } }


	protected abstract NetworkIdentity FetchCurrentNetworkIdentity();

	protected IPAddress TraceRouteToInternet()
	{
		var address = IPAddress.Parse("1.1.1.1");
		using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
		{
			socket.Connect(address, 53);
			return ((IPEndPoint)socket.LocalEndPoint!).Address;
		}
	}

	protected NetworkInterface GetInterfaceByIP(IPAddress ipAddress)
	{
		foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
			foreach (var interfaceAddress in networkInterface.GetIPProperties().UnicastAddresses)
				if (interfaceAddress.Address.Equals(ipAddress))
					return networkInterface;
		throw new Exception("No network interface found for IP " + ipAddress);
	}

	[MemberNotNull(nameof(_identity), nameof(_lastRefresh))]
	private void InitializeIfNeed()
	{
		if (_identity.HasValue && _lastRefresh.HasValue)
			return;

		_identity = FetchCurrentNetworkIdentity();
		_lastRefresh = DateTime.UtcNow;

		Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine("NEW NETWORK " + _identity.Value.SerializeToString());
		Console.ResetColor();

		NetworkChange.NetworkAddressChanged += NetworkChanged;
	}

	private void NetworkChanged(object? sender, EventArgs e)
	{
		_timer.Change(RefreshEventTimeout, Timeout.Infinite);
	}

	private void RefreshEvent(object? state)
	{
		if (Interlocked.Exchange(ref _isRefreshing, 1) == 1)
		{
			_timer.Change(RefreshEventTimeout, Timeout.Infinite);
			return;
		}

		try
		{
			_identity = FetchCurrentNetworkIdentity();
			_lastRefresh = DateTime.UtcNow;

			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("NEW NETWORK " + _identity.Value.SerializeToString());
			Console.ResetColor();
		}
		catch (Exception ex)
		{
			Console.WriteLine($"FAILED TO IDENTITIFY NEW NETWORK: {ex}");
		}
		finally
		{
			_isRefreshing = 0;
		}
	}
}
