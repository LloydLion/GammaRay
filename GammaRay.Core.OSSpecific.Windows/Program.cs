#if BuildAsExecutable
using GammaRay.Core.Monitoring;
using GammaRay.Core.OSSpecific.Windows.Management;
using GammaRay.Core.OSSpecific.Windows.Network.Identity;

namespace GammaRay.Core.OSSpecific.Windows;

public static class Program
{
	public static void Main()
	{
		var monitoring = new MonitoringSystem([]);
		var id = new WindowsNetProfileBasedNetworkIdentifier(monitoring, TimeProvider.System, new PowerShellHost());
		id.Initialize();

		Console.WriteLine(id.CurrentIdentity?.SerializedForm ?? "NULL");
	}
}
#endif
