using GammaRay.Core.Network;
using Serilog;
using System.Net.Sockets;


namespace GammaRay.Core.Linux.Network;

public class InterfaceBasedNetworkIdentifier() : NetworkIdentifierBase(default, _logger)
{
	private static readonly ILogger _logger = Log.ForContext<InterfaceBasedNetworkIdentifier>();


	protected override NetworkIdentity FetchCurrentNetworkIdentity()
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
