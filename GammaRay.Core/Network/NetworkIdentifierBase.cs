using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace GammaRay.Core.Network;

public abstract class NetworkIdentifierBase : INetworkIdentifier
{
	protected NetworkIdentifierBase(OSPlatform targetPlatform)
	{
		TargetPlatform = targetPlatform;
	}


	public OSPlatform TargetPlatform { get; }


	public abstract NetworkIdentity FetchCurrentNetworkIdentity();


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
}
