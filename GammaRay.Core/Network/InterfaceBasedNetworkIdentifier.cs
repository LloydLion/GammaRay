using GammaRay.Core.Network;


namespace GammaRay.Core.Linux.Network;

public class InterfaceBasedNetworkIdentifier() : NetworkIdentifierBase(default)
{
	protected override NetworkIdentity FetchCurrentNetworkIdentity()
	{
		var internetInterfaceIP = TraceRouteToInternet();
		var internetInterface = GetInterfaceByIP(internetInterfaceIP);

		var mac = internetInterface.GetPhysicalAddress().ToString();
		var ip = internetInterface.GetIPProperties().UnicastAddresses
			.FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.Address.ToString();
		return new NetworkIdentity([internetInterface.Name, mac, ip ?? "NoIP"]);
	}
}
