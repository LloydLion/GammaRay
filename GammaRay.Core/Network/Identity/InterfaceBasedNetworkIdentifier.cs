using GammaRay.Core.Monitoring;
using System.Net.Sockets;


namespace GammaRay.Core.Network.Identity;

public class InterfaceBasedNetworkIdentifier(IMonitoringSystem monitoringSystem, TimeProvider time) : NetworkIdentifierBase(monitoringSystem, time)
{
	protected override NetworkIdentity FetchCurrentNetworkIdentity(MonitoringContext monitoringContext)
	{
		var internetInterfaceIP = TraceRouteToInternet();
		var internetInterface = GetInterfaceByIP(internetInterfaceIP);

		var mac = internetInterface.GetPhysicalAddress().ToString();
		var ip = internetInterface.GetIPProperties().UnicastAddresses
			.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString();
		var type = internetInterface.NetworkInterfaceType.ToString();

		return new NetworkIdentity([type, mac, ip ?? "NoIP"]);
	}
}
