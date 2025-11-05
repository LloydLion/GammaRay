#if BuildAsExecutable
using GammaRay.Core.Windows.Management;
using GammaRay.Core.Windows.Network;

namespace GammaRay.Core.Windows;

public static class Program
{
	public static void Main()
	{
		var id = new WindowsNetProfileBasedNetworkIdentifier(new PowerShellHost());

		Console.WriteLine(id.CurrentIdentity.SerializeToString());
	}
}
#endif