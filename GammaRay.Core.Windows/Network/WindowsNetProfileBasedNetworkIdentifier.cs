using GammaRay.Core.Network;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace GammaRay.Core.Windows.Network;


[SupportedOSPlatform("windows")]
public class WindowsNetProfileBasedNetworkIdentifier() : NetworkIdentifierBase(OSPlatform.Windows)
{
	protected override NetworkIdentity FetchCurrentNetworkIdentity()
	{
		var internetInterfaceIP = TraceRouteToInternet();
		var internetInterface = GetInterfaceByIP(internetInterfaceIP);
		var profileId = GetWindowsNetworkProfileForInterface(internetInterface);
		return new NetworkIdentity([profileId.ToString()]);
	}

	private string GetWindowsNetworkProfileForInterface(NetworkInterface networkInterface)
	{
		var interfaceIndex = networkInterface.GetIPProperties().GetIPv4Properties().Index;

		var scope = new ManagementScope(@"\\.\root\StandardCimv2");
		scope.Connect();
		var query = new ObjectQuery("SELECT * FROM MSFT_NetConnectionProfile");
		var searcher = new ManagementObjectSearcher(scope, query);

		foreach (ManagementObject obj in searcher.Get())
		{
			var index = (uint)obj["InterfaceIndex"];
			if (index == interfaceIndex)
			{
				return (string)obj["InstanceId"];
			}
		}

		throw new Exception($"No Windows network profile for \"{networkInterface.Name}\"");
	}

}
