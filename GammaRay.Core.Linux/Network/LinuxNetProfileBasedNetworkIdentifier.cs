using GammaRay.Core.Network;
using System.Runtime.InteropServices;


namespace GammaRay.Core.Linux.Network;

public class LinuxNetProfileBasedNetworkIdentifier() : NetworkIdentifierBase(OSPlatform.Linux)
{
	protected override NetworkIdentity FetchCurrentNetworkIdentity()
	{
		var internetInterfaceIP = TraceRouteToInternet();
		var internetInterface = GetInterfaceByIP(internetInterfaceIP);

		var mac = internetInterface.GetPhysicalAddress().ToString();
		var ip = internetInterface.GetIPProperties().UnicastAddresses
			.FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.Address.ToString();
		return new NetworkIdentity([internetInterface.Name, mac, ip]);
	}
}
